// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Core.Scanning;

/// <summary>
/// Helper class to resolve scanner configuration from multiple sources with priority.
/// Priority: 1) Command line args, 2) Config file, 3) Defaults
/// </summary>
public static class ScannerConfiguration
{
    /// <summary>
    /// Default resource class names to detect if not configured.
    /// </summary>
    private static readonly List<string> DefaultResourceClassNames = new()
    {
        "Resources",
        "Strings",
        "AppResources"
    };

    /// <summary>
    /// Default localization method names to detect if not configured.
    /// </summary>
    private static readonly List<string> DefaultLocalizationMethods = new()
    {
        "GetString",
        "GetLocalizedString",
        "Translate",
        "L",
        "T"
    };

    /// <summary>
    /// Gets the resource class names to use for scanning.
    /// Priority: 1) Command line, 2) Config file, 3) Defaults
    /// </summary>
    /// <param name="fromCommandLine">Comma-separated class names from command line</param>
    /// <param name="config">Configuration model from file</param>
    /// <returns>List of resource class names to detect</returns>
    public static List<string> GetResourceClassNames(
        string? fromCommandLine,
        ConfigurationModel? config)
    {
        // Priority 1: Command line argument
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
        {
            return fromCommandLine
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        // Priority 2: Config file
        if (config?.Scanning?.ResourceClassNames?.Any() == true)
        {
            return config.Scanning.ResourceClassNames;
        }

        // Priority 3: Defaults
        return new List<string>(DefaultResourceClassNames);
    }

    /// <summary>
    /// Gets the localization method names to use for scanning.
    /// Priority: 1) Command line, 2) Config file, 3) Defaults
    /// </summary>
    /// <param name="fromCommandLine">Comma-separated method names from command line</param>
    /// <param name="config">Configuration model from file</param>
    /// <returns>List of localization method names to detect</returns>
    public static List<string> GetLocalizationMethods(
        string? fromCommandLine,
        ConfigurationModel? config)
    {
        // Priority 1: Command line argument
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
        {
            return fromCommandLine
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        // Priority 2: Config file
        if (config?.Scanning?.LocalizationMethods?.Any() == true)
        {
            return config.Scanning.LocalizationMethods;
        }

        // Priority 3: Defaults
        return new List<string>(DefaultLocalizationMethods);
    }
}
