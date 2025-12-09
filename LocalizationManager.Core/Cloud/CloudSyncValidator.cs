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
        var (hasResxFiles, hasJsonFiles) = DetectLocalResourceFiles();

        if (!hasResxFiles && !hasJsonFiles)
        {
            // No resource files yet - that's fine
            return result;
        }

        var remoteFormat = remoteProject.Format?.ToLowerInvariant() ?? "json";

        if (remoteFormat == "resx" && !hasResxFiles)
        {
            result.AddError($"Cannot link to cloud project with format 'resx' - no .resx files found locally.");
            if (hasJsonFiles)
            {
                result.AddError("Your local project has .json files. Use 'lrm convert' to migrate, or create a new cloud project with format 'json'.");
            }
        }
        else if (remoteFormat == "json" && !hasJsonFiles)
        {
            result.AddError($"Cannot link to cloud project with format 'json' - no .json files found locally.");
            if (hasResxFiles)
            {
                result.AddError("Your local project has .resx files. Use 'lrm convert' to migrate, or create a new cloud project with format 'resx'.");
            }
        }

        return result;
    }

    /// <summary>
    /// Detects the format of local resource files.
    /// Returns the detected format or null if no files found.
    /// </summary>
    public string? DetectLocalFormat()
    {
        var (hasResxFiles, hasJsonFiles) = DetectLocalResourceFiles();

        if (hasResxFiles && !hasJsonFiles)
            return "resx";
        if (hasJsonFiles && !hasResxFiles)
            return "json";
        if (hasResxFiles && hasJsonFiles)
            return null; // Mixed - can't determine

        return null; // No files
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

        if (localFormat != remoteFormat)
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
            result.AddWarning($"Default language mismatch: local is '{localConfig.DefaultLanguageCode}', remote is '{remoteProject.DefaultLanguage}'.");
            result.AddWarning("This may cause issues with which translations are considered 'source' strings.");
        }
    }

    private void ValidateLocalFilesMatchConfig(ConfigurationModel localConfig, SyncValidationResult result)
    {
        var configFormat = localConfig.ResourceFormat?.ToLowerInvariant();
        if (string.IsNullOrEmpty(configFormat))
        {
            return; // No format specified, skip this check
        }

        var (hasResxFiles, hasJsonFiles) = DetectLocalResourceFiles();

        if (configFormat == "resx")
        {
            if (!hasResxFiles && hasJsonFiles)
            {
                result.AddError("lrm.json specifies format 'resx' but only .json files found.");
                result.AddError("Either update lrm.json to use format 'json', or convert files with 'lrm convert --to resx'.");
            }
        }
        else if (configFormat == "json")
        {
            if (!hasJsonFiles && hasResxFiles)
            {
                result.AddError("lrm.json specifies format 'json' but only .resx files found.");
                result.AddError("Either update lrm.json to use format 'resx', or convert files with 'lrm convert --to json'.");
            }
        }
    }

    private (bool hasResx, bool hasJson) DetectLocalResourceFiles()
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

        return (resxFiles.Length > 0, jsonFiles.Length > 0);
    }
}
