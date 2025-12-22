// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Validates compatibility between local project and cloud project before sync operations.
/// </summary>
public class CloudSyncValidator
{
    /// <summary>
    /// Result of cloud sync validation.
    /// </summary>
    public class SyncValidationResult
    {
        public bool CanSync => !Errors.Any();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
    }

    private readonly string _projectDirectory;

    public CloudSyncValidator(string projectDirectory)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
    }

    /// <summary>
    /// Validates that local project is compatible with remote project for push operation.
    /// </summary>
    public SyncValidationResult ValidateForPush(ConfigurationModel localConfig, CloudProject remoteProject)
    {
        var result = new SyncValidationResult();

        ValidateFormatCompatibility(localConfig, remoteProject, result);
        ValidateDefaultLanguage(localConfig, remoteProject, result);
        ValidateLocalFilesMatchConfig(localConfig, result);

        return result;
    }

    /// <summary>
    /// Validates that local project is compatible with remote project for pull operation.
    /// </summary>
    public SyncValidationResult ValidateForPull(ConfigurationModel? localConfig, CloudProject remoteProject)
    {
        var result = new SyncValidationResult();

        // If no local config exists, we're pulling to initialize - that's fine
        if (localConfig == null)
        {
            return result;
        }

        ValidateFormatCompatibility(localConfig, remoteProject, result);
        ValidateDefaultLanguage(localConfig, remoteProject, result);

        return result;
    }

    /// <summary>
    /// Validates that local files match the format expected by the remote project.
    /// Used when linking an existing local project to a cloud project.
    /// </summary>
    public SyncValidationResult ValidateForLink(CloudProject remoteProject)
    {
        var result = new SyncValidationResult();

        // Check what files exist locally
        var (hasResxFiles, hasJsonFiles, hasAndroidFiles, hasIosFiles) = DetectLocalResourceFiles();

        if (!hasResxFiles && !hasJsonFiles && !hasAndroidFiles && !hasIosFiles)
        {
            // No resource files yet - that's fine
            return result;
        }

        var remoteFormat = remoteProject.Format?.ToLowerInvariant() ?? "json";
        var detectedFormat = DetectLocalFormat();

        // Normalize formats for comparison (json and i18next are compatible - both use JSON files)
        var normalizedRemote = NormalizeFormat(remoteFormat);
        var normalizedDetected = detectedFormat != null ? NormalizeFormat(detectedFormat) : null;

        if (normalizedDetected != null && normalizedDetected != normalizedRemote)
        {
            result.AddError($"Cannot link to cloud project with format '{remoteFormat}' - local project uses '{detectedFormat}'.");
            result.AddError($"Create a new cloud project with format '{detectedFormat}' instead.");
        }
        else if (detectedFormat == null && (hasResxFiles || hasJsonFiles || hasAndroidFiles || hasIosFiles))
        {
            result.AddWarning("Multiple resource formats detected locally. Consider consolidating to a single format.");
        }

        return result;
    }

    /// <summary>
    /// Detects the format of local resource files.
    /// Returns the detected format or null if no files found.
    /// </summary>
    public string? DetectLocalFormat()
    {
        var (hasResxFiles, hasJsonFiles, hasAndroidFiles, hasIosFiles) = DetectLocalResourceFiles();

        // Check for mobile formats first (more specific patterns)
        if (hasAndroidFiles && !hasResxFiles && !hasJsonFiles && !hasIosFiles)
            return "android";
        if (hasIosFiles && !hasResxFiles && !hasJsonFiles && !hasAndroidFiles)
            return "ios";
        if (hasResxFiles && !hasJsonFiles && !hasAndroidFiles && !hasIosFiles)
            return "resx";
        if (hasJsonFiles && !hasResxFiles && !hasAndroidFiles && !hasIosFiles)
            return "json";

        // Multiple formats detected - can't determine
        return null;
    }

    private void ValidateFormatCompatibility(ConfigurationModel localConfig, CloudProject remoteProject, SyncValidationResult result)
    {
        var localFormat = localConfig.ResourceFormat?.ToLowerInvariant();
        var remoteFormat = remoteProject.Format?.ToLowerInvariant();

        if (string.IsNullOrEmpty(localFormat))
        {
            // Try to auto-detect from files
            localFormat = DetectLocalFormat();
            if (localFormat == null)
            {
                result.AddWarning("Local format not specified in lrm.json and could not be auto-detected.");
                return;
            }
        }

        if (string.IsNullOrEmpty(remoteFormat))
        {
            result.AddWarning("Remote project format is not set.");
            return;
        }

        // Normalize formats for comparison (json and i18next are compatible)
        if (NormalizeFormat(localFormat) != NormalizeFormat(remoteFormat))
        {
            result.AddError($"Format mismatch: local project uses '{localFormat}' but cloud project expects '{remoteFormat}'.");
            result.AddError($"To sync, either:");
            result.AddError($"  1. Convert local files: lrm convert --to {remoteFormat}");
            result.AddError($"  2. Create a new cloud project with format '{localFormat}'");
        }
    }

    private void ValidateDefaultLanguage(ConfigurationModel localConfig, CloudProject remoteProject, SyncValidationResult result)
    {
        var localDefault = localConfig.DefaultLanguageCode?.ToLowerInvariant();
        var remoteDefault = remoteProject.DefaultLanguage?.ToLowerInvariant();

        if (string.IsNullOrEmpty(localDefault) || string.IsNullOrEmpty(remoteDefault))
        {
            return; // Can't compare
        }

        if (localDefault != remoteDefault)
        {
            result.AddError($"Default language mismatch: local lrm.json specifies '{localConfig.DefaultLanguageCode}', but cloud project uses '{remoteProject.DefaultLanguage}'.");
            result.AddError("Syncing with mismatched default languages would corrupt translation data.");
            result.AddError("To fix: update DefaultLanguageCode in lrm.json to match the cloud project.");
            result.AddError("Use --force to override this check (not recommended).");
        }
    }

    private void ValidateLocalFilesMatchConfig(ConfigurationModel localConfig, SyncValidationResult result)
    {
        var configFormat = localConfig.ResourceFormat?.ToLowerInvariant();
        if (string.IsNullOrEmpty(configFormat))
        {
            return; // No format specified, skip this check
        }

        var (hasResxFiles, hasJsonFiles, hasAndroidFiles, hasIosFiles) = DetectLocalResourceFiles();

        // Map format to expected files
        var hasExpectedFiles = configFormat switch
        {
            "resx" => hasResxFiles,
            "json" or "i18next" => hasJsonFiles,
            "android" => hasAndroidFiles,
            "ios" => hasIosFiles,
            _ => true // Unknown format, skip check
        };

        if (!hasExpectedFiles)
        {
            var detectedFormat = DetectLocalFormat();
            if (detectedFormat != null)
            {
                result.AddError($"lrm.json specifies format '{configFormat}' but local files appear to be '{detectedFormat}'.");
                result.AddError($"Either update lrm.json to use format '{detectedFormat}', or ensure correct resource files exist.");
            }
        }
    }

    /// <summary>
    /// Normalizes format names for compatibility comparison.
    /// JSON and i18next are compatible (both use JSON files with different naming conventions).
    /// </summary>
    private static string NormalizeFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" or "jsonlocalization" or "i18next" => "json",
            _ => format.ToLowerInvariant()
        };
    }

    private (bool hasResx, bool hasJson, bool hasAndroid, bool hasIos) DetectLocalResourceFiles()
    {
        // Check in the project directory itself
        var resxFiles = Directory.GetFiles(_projectDirectory, "*.resx", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".lrm") && !f.Contains("bin") && !f.Contains("obj"))
            .ToArray();

        var jsonFiles = Directory.GetFiles(_projectDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".lrm") && !f.Contains("bin") && !f.Contains("obj"))
            .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Check for Android: res/values*/strings.xml pattern
        var androidFiles = Directory.GetFiles(_projectDirectory, "strings.xml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .Where(f => f.Contains(Path.DirectorySeparatorChar + "values") ||
                        f.Contains(Path.DirectorySeparatorChar + "values-"))
            .ToArray();

        // Check for iOS: *.lproj/*.strings pattern
        var iosFiles = Directory.GetFiles(_projectDirectory, "*.strings", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .Where(f => Path.GetDirectoryName(f)?.EndsWith(".lproj") == true)
            .ToArray();

        return (resxFiles.Length > 0, jsonFiles.Length > 0, androidFiles.Length > 0, iosFiles.Length > 0);
    }
}
