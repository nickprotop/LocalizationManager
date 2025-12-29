// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// Discovers PO files in a directory and extracts language information.
/// Supports GNU gettext folder structure and flat layouts.
/// </summary>
public class PoResourceDiscovery : IResourceDiscovery
{
    private readonly PoFormatConfiguration _config;
    private readonly string? _defaultLanguageCode;

    public PoResourceDiscovery(PoFormatConfiguration? config = null, string? defaultLanguageCode = null)
    {
        _config = config ?? new PoFormatConfiguration();
        _defaultLanguageCode = defaultLanguageCode;
    }

    /// <inheritdoc />
    public async Task<List<LanguageInfo>> DiscoverLanguagesAsync(string searchPath, CancellationToken ct = default)
    {
        return await Task.Run(() => DiscoverLanguages(searchPath), ct);
    }

    /// <inheritdoc />
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        if (!Directory.Exists(searchPath))
            return new List<LanguageInfo>();

        var result = new List<LanguageInfo>();

        // Find ALL PO and POT files recursively
        // Sort files to ensure consistent processing order:
        // - Shorter filenames first (en.po before messages.en.po)
        // - Alphabetical within same length
        var allPoFiles = Directory.GetFiles(searchPath, "*.po", SearchOption.AllDirectories)
            .OrderBy(f => Path.GetFileName(f).Length)
            .ThenBy(f => Path.GetFileName(f))
            .ToArray();
        var allPotFiles = Directory.GetFiles(searchPath, "*.pot", SearchOption.AllDirectories);

        // Process POT files first (they are templates/default)
        foreach (var potFile in allPotFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(potFile);
            result.Add(new LanguageInfo
            {
                Code = "",
                Name = "Template (default)",
                BaseName = baseName,
                FilePath = potFile,
                IsDefault = true
            });
        }

        // Process PO files
        foreach (var poFile in allPoFiles)
        {
            var langCode = ExtractLanguageFromPath(poFile, searchPath);
            if (string.IsNullOrEmpty(langCode))
                continue;

            var baseName = Path.GetFileNameWithoutExtension(poFile);
            result.Add(new LanguageInfo
            {
                Code = langCode,
                Name = GetLanguageDisplayName(langCode),
                BaseName = baseName,
                FilePath = poFile,
                IsDefault = false
            });
        }

        // Deduplicate by language code (keep first occurrence)
        var languageMap = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var lang in result)
        {
            var key = lang.IsDefault ? "__default__" : lang.Code;
            if (!languageMap.ContainsKey(key))
                languageMap[key] = lang;
        }

        // If no POT (no default), mark the configured source language as default
        if (!languageMap.ContainsKey("__default__") && !string.IsNullOrEmpty(_defaultLanguageCode))
        {
            if (languageMap.TryGetValue(_defaultLanguageCode, out var sourceLang))
            {
                sourceLang.IsDefault = true;
            }
        }

        // Sort: default language first, then alphabetically
        return languageMap.Values
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Code)
            .ToList();
    }

    /// <summary>
    /// Extracts language code from file path using multiple strategies:
    /// 1. GNU gettext: locale/{lang}/LC_MESSAGES/messages.po
    /// 2. Nested folders: translations/{lang}/app.po, i18n/{lang}/messages.po
    /// 3. Flat with lang in filename: en.po, messages.fr.po
    /// 4. Language header in PO file (fallback)
    /// </summary>
    private string ExtractLanguageFromPath(string filePath, string basePath)
    {
        var relativePath = Path.GetRelativePath(basePath, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Strategy 1: GNU gettext pattern - look for LC_MESSAGES
        // Pattern: locale/{lang}/LC_MESSAGES/messages.po or {lang}/LC_MESSAGES/messages.po
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("LC_MESSAGES", StringComparison.OrdinalIgnoreCase) && i > 0)
            {
                var potentialLang = parts[i - 1];
                if (IsLanguageCode(potentialLang))
                    return potentialLang;
            }
        }

        // Strategy 2: Nested language folders
        // Pattern: {folder}/{lang}/app.po, translations/{lang}/messages.po
        // The folder before the filename is potentially the language
        if (parts.Length >= 2)
        {
            var parentFolder = parts[parts.Length - 2];
            if (IsLanguageCode(parentFolder))
                return parentFolder;
        }

        // Strategy 3: Language in filename
        var langFromFilename = ExtractLanguageFromFileName(filePath);
        if (!string.IsNullOrEmpty(langFromFilename))
            return langFromFilename;

        // Strategy 4: Read Language header from PO file
        var langFromHeader = ReadLanguageFromPoHeader(filePath);
        if (!string.IsNullOrEmpty(langFromHeader) && IsLanguageCode(langFromHeader))
            return langFromHeader;

        return string.Empty;
    }

    /// <summary>
    /// Reads the Language header from a PO file.
    /// </summary>
    private string? ReadLanguageFromPoHeader(string filePath)
    {
        try
        {
            var lines = File.ReadLines(filePath).Take(30);
            foreach (var line in lines)
            {
                if (line.Contains("\"Language:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Language:\s*([a-zA-Z_-]+)");
                    if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                        return match.Groups[1].Value;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Detects whether the directory uses GNU or flat folder structure.
    /// </summary>
    public FolderStructure DetectFolderStructure(string path)
    {
        if (!Directory.Exists(path))
            return FolderStructure.Unknown;

        // Check for GNU gettext structure: locale/*/LC_MESSAGES/*.po
        var localeDir = Path.Combine(path, "locale");
        if (Directory.Exists(localeDir))
        {
            var lcMessagesDirs = Directory.GetDirectories(localeDir, "LC_MESSAGES", SearchOption.AllDirectories);
            if (lcMessagesDirs.Any(d => Directory.GetFiles(d, "*.po").Length > 0))
                return FolderStructure.Gnu;
        }

        // Check for po/ folder
        var poDir = Path.Combine(path, "po");
        if (Directory.Exists(poDir) && Directory.GetFiles(poDir, "*.po").Length > 0)
            return FolderStructure.Flat;

        // Check for locales/ folder
        var localesDir = Path.Combine(path, "locales");
        if (Directory.Exists(localesDir) && Directory.GetFiles(localesDir, "*.po").Length > 0)
            return FolderStructure.Flat;

        // Check for .po files directly in the path
        if (Directory.GetFiles(path, "*.po").Length > 0)
            return FolderStructure.Flat;

        return FolderStructure.Unknown;
    }

    /// <summary>
    /// Gets auto-discovered configuration based on the folder structure.
    /// </summary>
    public PoDiscoveryResult DiscoverConfiguration(string path)
    {
        var result = new PoDiscoveryResult
        {
            FolderStructure = DetectFolderStructure(path).ToString().ToLowerInvariant()
        };

        // Find POT file for domain detection
        var potFiles = Directory.GetFiles(path, "*.pot", SearchOption.AllDirectories);
        if (potFiles.Length > 0)
        {
            result.Domain = Path.GetFileNameWithoutExtension(potFiles[0]);
            result.HasPotTemplate = true;
        }
        else
        {
            // Try to infer domain from PO filenames
            var poFiles = Directory.GetFiles(path, "*.po", SearchOption.AllDirectories);
            if (poFiles.Length > 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(poFiles[0]);
                // If filename looks like a language code, domain is probably "messages"
                if (IsLanguageCode(fileName))
                    result.Domain = "messages";
                else
                    result.Domain = fileName;
            }
        }

        // Discover languages
        var languages = DiscoverLanguages(path);
        result.Languages = languages.Select(l => l.Code).ToList();

        // Try to detect default language from POT or header
        if (potFiles.Length > 0)
        {
            result.DefaultLanguage = DetectDefaultLanguageFromPot(potFiles[0]);
        }

        return result;
    }

    private string ExtractLanguageFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Common patterns:
        // 1. {lang}.po (e.g., en.po, fr.po, pt-BR.po)
        // 2. {domain}.{lang}.po (e.g., messages.en.po)
        // 3. {lang}_{domain}.po (e.g., en_messages.po)

        // Try to detect if filename is a language code directly
        if (IsLanguageCode(fileName))
            return fileName;

        // Check for domain.lang pattern
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot > 0)
        {
            var potentialLang = fileName.Substring(lastDot + 1);
            if (IsLanguageCode(potentialLang))
                return potentialLang;
        }

        // Check for lang_domain pattern
        var underscoreIndex = fileName.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var potentialLang = fileName.Substring(0, underscoreIndex);
            if (IsLanguageCode(potentialLang))
                return potentialLang;
        }

        // No valid language code found - return empty to skip this file
        // Files like "plurals.po", "messages.po" without language suffix are not language files
        return string.Empty;
    }

    private bool IsLanguageCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        // 2-letter codes (en, fr, de)
        if (code.Length == 2 && code.All(char.IsLetter))
            return true;

        // Regional codes (en-US, pt-BR, zh-CN)
        if ((code.Length == 5 || code.Length == 6) &&
            (code.Contains('-') || code.Contains('_')))
        {
            var parts = code.Split('-', '_');
            if (parts.Length == 2 &&
                parts[0].Length == 2 && parts[0].All(char.IsLetter) &&
                parts[1].Length >= 2 && parts[1].All(char.IsLetterOrDigit))
                return true;
        }

        // Try parsing as culture - but reject custom/user-created cultures
        // CultureInfo.GetCultureInfo accepts any string and creates a custom culture
        // Real cultures have LCID != 4096 (LOCALE_CUSTOM_UNSPECIFIED)
        try
        {
            var culture = CultureInfo.GetCultureInfo(code.Replace('_', '-'));
            // Reject user custom cultures (LCID 4096 = LOCALE_CUSTOM_UNSPECIFIED)
            return culture.LCID != 4096;
        }
        catch
        {
            return false;
        }
    }

    private string? DetectDefaultLanguageFromPot(string potPath)
    {
        // Try to read Language header from POT file
        try
        {
            var lines = File.ReadLines(potPath).Take(50);
            foreach (var line in lines)
            {
                if (line.Contains("\"Language:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Language:\s*([a-zA-Z_-]+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "en"; // Default assumption
    }

    private string GetLanguageDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "Default";

        try
        {
            var normalizedCode = code.Replace('_', '-');
            var culture = CultureInfo.GetCultureInfo(normalizedCode);
            return $"{culture.DisplayName} ({code})";
        }
        catch
        {
            return code;
        }
    }
}

/// <summary>
/// PO folder structure types.
/// </summary>
public enum FolderStructure
{
    /// <summary>
    /// Unknown structure.
    /// </summary>
    Unknown,

    /// <summary>
    /// GNU gettext structure: locale/{lang}/LC_MESSAGES/{domain}.po
    /// </summary>
    Gnu,

    /// <summary>
    /// Flat structure: {lang}.po or po/{lang}.po
    /// </summary>
    Flat
}

/// <summary>
/// Result of auto-discovering PO configuration.
/// </summary>
public class PoDiscoveryResult
{
    /// <summary>
    /// Detected folder structure (gnu or flat).
    /// </summary>
    public string FolderStructure { get; set; } = "flat";

    /// <summary>
    /// Detected domain name.
    /// </summary>
    public string Domain { get; set; } = "messages";

    /// <summary>
    /// Whether a POT template was found.
    /// </summary>
    public bool HasPotTemplate { get; set; }

    /// <summary>
    /// Detected default language code.
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// Discovered language codes.
    /// </summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Detected charset (from header parsing).
    /// </summary>
    public string Charset { get; set; } = "UTF-8";
}
