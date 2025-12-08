// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Validates configuration settings and ensures consistency with the project state.
/// </summary>
public class ConfigurationValidator
{
    /// <summary>
    /// Validation result containing errors and warnings.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
    }

    private readonly string _projectDirectory;

    public ConfigurationValidator(string projectDirectory)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
    }

    /// <summary>
    /// Validates the entire configuration model.
    /// </summary>
    public ValidationResult Validate(ConfigurationModel config)
    {
        var result = new ValidationResult();

        ValidateResourceFormat(config, result);
        ValidateCloudConfiguration(config, result);
        ValidateTranslationConfiguration(config, result);
        ValidateValidationConfiguration(config, result);

        return result;
    }

    /// <summary>
    /// Validates that the resource format matches actual resource files on disk.
    /// </summary>
    public ValidationResult ValidateResourceFormat(ConfigurationModel config, ValidationResult? result = null)
    {
        result ??= new ValidationResult();

        if (string.IsNullOrWhiteSpace(config.ResourceFormat))
        {
            result.AddWarning("ResourceFormat not specified. Auto-detection will be used.");
            return result;
        }

        var format = config.ResourceFormat.ToLowerInvariant();
        if (format != "resx" && format != "json")
        {
            result.AddError($"Invalid ResourceFormat: '{config.ResourceFormat}'. Must be 'resx' or 'json'.");
            return result;
        }

        // Check if resource files exist
        var resourcePath = Path.Combine(_projectDirectory, "Resources");
        if (!Directory.Exists(resourcePath))
        {
            // No Resources directory - this is valid for new projects
            result.AddWarning("Resources directory not found. No resource files to validate.");
            return result;
        }

        // Find resource files
        var resxFiles = Directory.GetFiles(resourcePath, "*.resx", SearchOption.AllDirectories);
        var jsonFiles = Directory.GetFiles(resourcePath, "*.json", SearchOption.AllDirectories);

        if (resxFiles.Length == 0 && jsonFiles.Length == 0)
        {
            result.AddWarning("No resource files found in Resources directory.");
            return result;
        }

        // Validate format consistency
        if (format == "resx")
        {
            if (resxFiles.Length == 0)
            {
                result.AddError("ResourceFormat is 'resx' but no .resx files found.");
            }

            if (jsonFiles.Length > 0)
            {
                result.AddWarning($"ResourceFormat is 'resx' but {jsonFiles.Length} .json files found. Consider migration.");
            }
        }
        else if (format == "json")
        {
            if (jsonFiles.Length == 0)
            {
                result.AddError("ResourceFormat is 'json' but no .json files found.");
            }

            if (resxFiles.Length > 0)
            {
                result.AddWarning($"ResourceFormat is 'json' but {resxFiles.Length} .resx files found. Consider migration.");
            }
        }

        return result;
    }

    /// <summary>
    /// Validates cloud synchronization configuration.
    /// </summary>
    public ValidationResult ValidateCloudConfiguration(ConfigurationModel config, ValidationResult? result = null)
    {
        result ??= new ValidationResult();

        if (config.Cloud == null)
        {
            return result; // Cloud sync not configured - that's valid
        }

        // If cloud sync is enabled, remote URL must be set
        if (config.Cloud.Enabled && string.IsNullOrWhiteSpace(config.Cloud.Remote))
        {
            result.AddError("Cloud synchronization is enabled but Remote URL is not set.");
            return result;
        }

        // Validate remote URL format if set
        if (!string.IsNullOrWhiteSpace(config.Cloud.Remote))
        {
            if (!RemoteUrlParser.TryParse(config.Cloud.Remote, out var remoteUrl))
            {
                result.AddError($"Invalid Remote URL format: '{config.Cloud.Remote}'. Expected: https://host/org/project or https://host/@username/project");
            }
            else
            {
                // Validate HTTPS for non-localhost
                if (!remoteUrl!.UseHttps && !IsLocalhost(remoteUrl.Host))
                {
                    result.AddWarning($"Remote URL uses HTTP instead of HTTPS. This is insecure for non-localhost connections.");
                }
            }
        }

        // Validate sync configuration
        if (config.Cloud.Sync != null)
        {
            var mode = config.Cloud.Sync.Mode?.ToLowerInvariant();
            if (mode != null && mode != "manual" && mode != "auto")
            {
                result.AddError($"Invalid Sync.Mode: '{config.Cloud.Sync.Mode}'. Must be 'manual' or 'auto'.");
            }

            if (config.Cloud.Sync.Paths == null || config.Cloud.Sync.Paths.Count == 0)
            {
                result.AddWarning("Sync.Paths is empty. No files will be synchronized.");
            }
        }

        return result;
    }

    /// <summary>
    /// Validates translation configuration.
    /// </summary>
    public ValidationResult ValidateTranslationConfiguration(ConfigurationModel config, ValidationResult? result = null)
    {
        result ??= new ValidationResult();

        if (config.Translation == null)
        {
            return result; // Translation not configured - that's valid
        }

        var validProviders = new[] { "google", "deepl", "libretranslate", "ollama", "openai", "claude", "azureopenai", "azuretranslator", "lingva", "mymemory" };
        var defaultProvider = config.Translation.DefaultProvider?.ToLowerInvariant();

        if (defaultProvider != null && !validProviders.Contains(defaultProvider))
        {
            result.AddError($"Invalid DefaultProvider: '{config.Translation.DefaultProvider}'. Supported: {string.Join(", ", validProviders)}.");
        }

        if (config.Translation.MaxRetries < 0)
        {
            result.AddError("Translation.MaxRetries cannot be negative.");
        }

        if (config.Translation.TimeoutSeconds <= 0)
        {
            result.AddError("Translation.TimeoutSeconds must be greater than 0.");
        }

        if (config.Translation.BatchSize <= 0)
        {
            result.AddError("Translation.BatchSize must be greater than 0.");
        }

        return result;
    }

    /// <summary>
    /// Validates validation configuration.
    /// </summary>
    public ValidationResult ValidateValidationConfiguration(ConfigurationModel config, ValidationResult? result = null)
    {
        result ??= new ValidationResult();

        if (config.Validation?.PlaceholderTypes == null)
        {
            return result;
        }

        var validTypes = new[] { "dotnet", "printf", "icu", "template", "all" };
        foreach (var type in config.Validation.PlaceholderTypes)
        {
            var lowerType = type?.ToLowerInvariant();
            if (lowerType != null && !validTypes.Contains(lowerType))
            {
                result.AddError($"Invalid PlaceholderType: '{type}'. Supported: {string.Join(", ", validTypes)}.");
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if format change is safe (all resource files match the new format).
    /// </summary>
    public ValidationResult ValidateFormatChange(ConfigurationModel currentConfig, string newFormat)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(newFormat))
        {
            result.AddError("New format cannot be empty.");
            return result;
        }

        var format = newFormat.ToLowerInvariant();
        if (format != "resx" && format != "json")
        {
            result.AddError($"Invalid format: '{newFormat}'. Must be 'resx' or 'json'.");
            return result;
        }

        // If no current format, change is safe
        if (string.IsNullOrWhiteSpace(currentConfig.ResourceFormat))
        {
            return result;
        }

        // If same format, no change needed
        if (currentConfig.ResourceFormat.Equals(newFormat, StringComparison.OrdinalIgnoreCase))
        {
            result.AddWarning("New format is the same as current format.");
            return result;
        }

        // Check if resource files exist in the new format
        var resourcePath = Path.Combine(_projectDirectory, "Resources");
        if (!Directory.Exists(resourcePath))
        {
            return result; // No resources yet, safe to change
        }

        var currentFiles = format == "resx"
            ? Directory.GetFiles(resourcePath, "*.resx", SearchOption.AllDirectories)
            : Directory.GetFiles(resourcePath, "*.json", SearchOption.AllDirectories);

        if (currentFiles.Length == 0)
        {
            result.AddError($"Cannot change format to '{newFormat}' - no {newFormat} files found. Please migrate resources first using 'lrm convert' command.");
        }

        return result;
    }

    /// <summary>
    /// Checks if a hostname is localhost.
    /// </summary>
    private static bool IsLocalhost(string host)
    {
        var lower = host.ToLowerInvariant();
        return lower == "localhost" || lower == "127.0.0.1" || lower == "::1";
    }
}
