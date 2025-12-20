// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Discovers iOS resource files (.strings and .stringsdict) in a directory.
/// Supports standard iOS project structures with .lproj folders.
/// </summary>
public class IosResourceDiscovery : IResourceDiscovery
{
    private readonly string _stringsFileName;
    private readonly string? _developmentLanguage;

    /// <summary>
    /// Creates a new iOS resource discovery instance.
    /// </summary>
    /// <param name="stringsFileName">The strings file name (default: "Localizable.strings")</param>
    /// <param name="developmentLanguage">The development language for Base.lproj resolution</param>
    public IosResourceDiscovery(
        string stringsFileName = "Localizable.strings",
        string? developmentLanguage = null)
    {
        _stringsFileName = stringsFileName;
        _developmentLanguage = developmentLanguage;
    }

    /// <inheritdoc />
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        var result = new List<LanguageInfo>();

        if (!Directory.Exists(searchPath))
            return result;

        // Find all .lproj folders
        var lprojFolders = FindLprojFolders(searchPath);

        // Try to detect development language
        var developmentLanguage = _developmentLanguage ?? DetectDevelopmentLanguage(searchPath, lprojFolders);

        foreach (var folder in lprojFolders)
        {
            var stringsFile = Path.Combine(folder, _stringsFileName);
            var stringsdictFile = Path.Combine(folder, Path.ChangeExtension(_stringsFileName, ".stringsdict"));

            // Must have at least one of the files
            if (!File.Exists(stringsFile) && !File.Exists(stringsdictFile))
                continue;

            var folderName = Path.GetFileName(folder);
            var cultureCode = IosCultureMapper.LprojToCode(folderName, developmentLanguage);
            var isDefault = IosCultureMapper.IsBaseLproj(folderName) ||
                           (!string.IsNullOrEmpty(developmentLanguage) &&
                            cultureCode.Equals(developmentLanguage, StringComparison.OrdinalIgnoreCase));

            result.Add(new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(_stringsFileName),
                Code = isDefault ? "" : cultureCode,
                Name = isDefault ? "Default" : GetCultureDisplayName(cultureCode),
                IsDefault = isDefault,
                FilePath = File.Exists(stringsFile) ? stringsFile : stringsdictFile
            });
        }

        // If no explicit default found, use English or first
        if (!result.Any(l => l.IsDefault) && result.Any())
        {
            var englishOrFirst = result.FirstOrDefault(l =>
                l.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) ?? result.First();

            var index = result.IndexOf(englishOrFirst);
            englishOrFirst.IsDefault = true;
            englishOrFirst.Code = "";
            englishOrFirst.Name = "Default";
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
    /// Finds all .lproj folders in the search path.
    /// </summary>
    private List<string> FindLprojFolders(string searchPath)
    {
        var folders = new List<string>();

        // Direct .lproj folders
        folders.AddRange(Directory.GetDirectories(searchPath, "*.lproj"));

        // Check Resources subfolder
        var resourcesPath = Path.Combine(searchPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            folders.AddRange(Directory.GetDirectories(resourcesPath, "*.lproj"));
        }

        // Check common iOS project structure paths
        var projectPaths = new[]
        {
            searchPath,
            Path.Combine(searchPath, "Sources"),
            Path.Combine(searchPath, "Resources"),
        };

        foreach (var path in projectPaths)
        {
            if (Directory.Exists(path))
            {
                folders.AddRange(Directory.GetDirectories(path, "*.lproj"));
            }
        }

        return folders.Distinct().ToList();
    }

    /// <summary>
    /// Tries to detect the development language from project files.
    /// </summary>
    private string? DetectDevelopmentLanguage(string searchPath, List<string> lprojFolders)
    {
        // Try to read from lrm.json config
        try
        {
            var (config, _) = Configuration.ConfigurationManager.LoadConfiguration(null, searchPath);
            if (!string.IsNullOrEmpty(config?.DefaultLanguageCode))
                return config.DefaultLanguageCode;
        }
        catch
        {
            // Ignore config loading errors
        }

        // If there's a Base.lproj, check which language has matching content
        var baseLproj = lprojFolders.FirstOrDefault(f =>
            Path.GetFileName(f).Equals("Base.lproj", StringComparison.OrdinalIgnoreCase));

        if (baseLproj != null)
        {
            // Try to find an English folder first
            var englishFolder = lprojFolders.FirstOrDefault(f =>
                Path.GetFileName(f).StartsWith("en", StringComparison.OrdinalIgnoreCase));

            if (englishFolder != null)
                return IosCultureMapper.LprojToCode(Path.GetFileName(englishFolder), null);
        }

        return null;
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

    /// <summary>
    /// Gets the path to the .stringsdict file for a language.
    /// </summary>
    public static string GetStringsdictPath(LanguageInfo language)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("Language file path is required", nameof(language));

        return Path.ChangeExtension(language.FilePath, ".stringsdict");
    }
}
