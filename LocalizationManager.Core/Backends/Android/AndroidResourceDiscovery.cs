// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Discovers Android resource files (strings.xml) in a directory.
/// Supports standard Android project structures.
/// </summary>
public class AndroidResourceDiscovery : IResourceDiscovery
{
    private readonly string _resourceFileName;
    private readonly string? _defaultLanguageCode;

    /// <summary>
    /// Creates a new Android resource discovery instance.
    /// </summary>
    /// <param name="resourceFileName">The resource file name (default: "strings.xml")</param>
    /// <param name="defaultLanguageCode">The default language code from configuration (e.g., "en").
    /// Used when there's no bare "values" folder to identify the source language.</param>
    public AndroidResourceDiscovery(string resourceFileName = "strings.xml", string? defaultLanguageCode = null)
    {
        _resourceFileName = resourceFileName;
        _defaultLanguageCode = defaultLanguageCode;
    }

    /// <inheritdoc />
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        var result = new List<LanguageInfo>();

        if (!Directory.Exists(searchPath))
            return result;

        // Find the res folder
        var resPath = FindResFolder(searchPath);
        if (resPath == null)
            return result;

        // Find all values* folders
        var valuesFolders = Directory.GetDirectories(resPath)
            .Where(d => AndroidCultureMapper.IsValidResourceFolder(Path.GetFileName(d)))
            .ToList();

        // Check if we have a bare "values" folder (traditional Android default)
        var hasValuesFolder = valuesFolders.Any(f =>
            Path.GetFileName(f).Equals("values", StringComparison.OrdinalIgnoreCase));

        foreach (var folder in valuesFolders)
        {
            var stringsFile = Path.Combine(folder, _resourceFileName);
            if (!File.Exists(stringsFile))
                continue;

            var folderName = Path.GetFileName(folder);
            var cultureCode = AndroidCultureMapper.FolderToCode(folderName);

            // Determine if this is the default language:
            // 1. If it's the bare "values" folder (empty culture code) → always default
            // 2. If no "values" folder exists and we have a DefaultLanguageCode from config,
            //    mark the matching language as default
            var isDefault = string.IsNullOrEmpty(cultureCode) ||
                (!hasValuesFolder && !string.IsNullOrEmpty(_defaultLanguageCode) &&
                 cultureCode.Equals(_defaultLanguageCode, StringComparison.OrdinalIgnoreCase));

            result.Add(new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(_resourceFileName),
                Code = cultureCode,
                Name = isDefault ? "Default" : GetCultureDisplayName(cultureCode),
                IsDefault = isDefault,
                FilePath = stringsFile
            });
        }

        // Sort: default language first, then alphabetically by code
        return result
            .OrderBy(l => l.IsDefault ? 0 : 1)
            .ThenBy(l => l.Code)
            .ToList();
    }

    /// <inheritdoc />
    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(DiscoverLanguages(searchPath));

    /// <summary>
    /// Finds the res folder in the search path.
    /// Supports multiple Android project structures.
    /// </summary>
    private string? FindResFolder(string searchPath)
    {
        // Check if path is already the res folder
        if (Path.GetFileName(searchPath).Equals("res", StringComparison.OrdinalIgnoreCase) &&
            HasValuesFolder(searchPath))
        {
            return searchPath;
        }

        // Check for direct res subfolder
        var resPath = Path.Combine(searchPath, "res");
        if (Directory.Exists(resPath) && HasValuesFolder(resPath))
            return resPath;

        // Check for app/src/main/res (standard Android project)
        var mainResPath = Path.Combine(searchPath, "app", "src", "main", "res");
        if (Directory.Exists(mainResPath) && HasValuesFolder(mainResPath))
            return mainResPath;

        // Check for src/main/res (module structure)
        var srcMainResPath = Path.Combine(searchPath, "src", "main", "res");
        if (Directory.Exists(srcMainResPath) && HasValuesFolder(srcMainResPath))
            return srcMainResPath;

        return null;
    }

    /// <summary>
    /// Checks if a path has any valid values folder (values or values-*).
    /// </summary>
    private bool HasValuesFolder(string path)
    {
        // Check for default values folder
        if (Directory.Exists(Path.Combine(path, "values")))
            return true;

        // Check for any values-* folder with strings.xml
        try
        {
            foreach (var dir in Directory.GetDirectories(path, "values*"))
            {
                if (File.Exists(Path.Combine(dir, _resourceFileName)))
                    return true;
            }
        }
        catch
        {
            // Ignore directory access errors
        }

        return false;
    }

    /// <summary>
    /// Gets a display-friendly name for a culture code.
    /// </summary>
    private static string GetCultureDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "Default";

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return $"{culture.NativeName} ({code})";
        }
        catch
        {
            return code.ToUpperInvariant();
        }
    }
}
