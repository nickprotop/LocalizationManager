// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// Discovers XLIFF files in a directory and extracts language information.
/// </summary>
public class XliffResourceDiscovery : IResourceDiscovery
{
    private readonly XliffFormatConfiguration _config;
    private readonly XliffVersionDetector _detector = new();

    public XliffResourceDiscovery(XliffFormatConfiguration? config = null)
    {
        _config = config ?? new XliffFormatConfiguration();
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

        var languageMap = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        // Find all XLIFF files, excluding backup/metadata directories
        var xliffFiles = Directory.GetFiles(searchPath, "*.xliff", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(searchPath, "*.xlf", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.lrm{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.AltDirectorySeparatorChar}.lrm{Path.AltDirectorySeparatorChar}"))
            .ToList();

        foreach (var filePath in xliffFiles)
        {
            // For bilingual XLIFF, we get both source and target languages
            var languages = ExtractLanguageInfos(filePath);
            foreach (var languageInfo in languages)
            {
                // Deduplicate by language code - keep first occurrence (or prefer default)
                if (!languageMap.TryGetValue(languageInfo.Code, out var existing))
                {
                    languageMap[languageInfo.Code] = languageInfo;
                }
                else if (languageInfo.IsDefault && !existing.IsDefault)
                {
                    // Prefer the default language file
                    languageMap[languageInfo.Code] = languageInfo;
                }
            }
        }

        // Sort: default language first, then alphabetically
        return languageMap.Values
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Code)
            .ToList();
    }

    /// <summary>
    /// Extracts language information from an XLIFF file.
    /// For bilingual files, returns both source (default) and target languages.
    /// </summary>
    private List<LanguageInfo> ExtractLanguageInfos(string filePath)
    {
        var result = new List<LanguageInfo>();

        try
        {
            var settings = XliffVersionDetector.CreateSafeXmlReaderSettings();
            using var stream = File.OpenRead(filePath);
            using var reader = XmlReader.Create(stream, settings);
            var doc = XDocument.Load(reader);
            var root = doc.Root;

            if (root == null)
                return result;

            var ns = root.GetDefaultNamespace();
            var version = _detector.DetectVersion(filePath);

            string? sourceLanguage = null;
            string? targetLanguage = null;

            if (version == "2.0")
            {
                sourceLanguage = root.Attribute("srcLang")?.Value;
                targetLanguage = root.Attribute("trgLang")?.Value;
            }
            else
            {
                // XLIFF 1.2
                var fileElement = root.Elements(ns + "file").FirstOrDefault();
                sourceLanguage = fileElement?.Attribute("source-language")?.Value;
                targetLanguage = fileElement?.Attribute("target-language")?.Value;
            }

            var baseName = ExtractBaseName(filePath, targetLanguage ?? sourceLanguage ?? "");

            // For bilingual files, add source language as default
            if (!string.IsNullOrEmpty(sourceLanguage))
            {
                result.Add(new LanguageInfo
                {
                    Code = sourceLanguage,
                    Name = GetLanguageDisplayName(sourceLanguage),
                    BaseName = baseName,
                    FilePath = filePath,
                    IsDefault = true
                });
            }

            // Add target language if different from source
            if (!string.IsNullOrEmpty(targetLanguage) &&
                !targetLanguage.Equals(sourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new LanguageInfo
                {
                    Code = targetLanguage,
                    Name = GetLanguageDisplayName(targetLanguage),
                    BaseName = baseName,
                    FilePath = filePath,
                    IsDefault = false
                });
            }

            // Fallback: try to extract from filename if no languages found
            if (result.Count == 0)
            {
                var langCode = ExtractLanguageFromFileName(filePath);
                if (!string.IsNullOrEmpty(langCode))
                {
                    result.Add(new LanguageInfo
                    {
                        Code = langCode,
                        Name = GetLanguageDisplayName(langCode),
                        BaseName = baseName,
                        FilePath = filePath,
                        IsDefault = false
                    });
                }
            }
        }
        catch
        {
            // If we can't parse the file, try to extract info from filename
            var langCode = ExtractLanguageFromFileName(filePath);
            if (!string.IsNullOrEmpty(langCode))
            {
                var baseName = ExtractBaseName(filePath, langCode);
                result.Add(new LanguageInfo
                {
                    Code = langCode,
                    Name = GetLanguageDisplayName(langCode),
                    BaseName = baseName,
                    FilePath = filePath,
                    IsDefault = false
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts language information from an XLIFF file (single language, for compatibility).
    /// </summary>
    private LanguageInfo? ExtractLanguageInfo(string filePath)
    {
        var infos = ExtractLanguageInfos(filePath);
        // Return target language if available, else source
        return infos.FirstOrDefault(l => !l.IsDefault) ?? infos.FirstOrDefault();
    }

    /// <summary>
    /// Extracts language code from filename.
    /// Supports patterns like: file.en.xliff, file_en.xliff, en.xliff
    /// </summary>
    private string? ExtractLanguageFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Pattern: file.lang.xliff (remove double extension)
        if (fileName.Contains('.'))
        {
            var lastDot = fileName.LastIndexOf('.');
            var potentialLang = fileName.Substring(lastDot + 1);
            if (IsLanguageCode(potentialLang))
                return potentialLang;
        }

        // Pattern: file_lang.xliff
        if (fileName.Contains('_'))
        {
            var lastUnderscore = fileName.LastIndexOf('_');
            var potentialLang = fileName.Substring(lastUnderscore + 1);
            if (IsLanguageCode(potentialLang))
                return potentialLang;
        }

        // Pattern: file-lang.xliff
        if (fileName.Contains('-'))
        {
            // Be careful with regional codes like en-US
            var parts = fileName.Split('-');
            if (parts.Length >= 2)
            {
                var lastPart = parts[parts.Length - 1];
                if (IsLanguageCode(lastPart))
                    return lastPart;

                // Check for regional code
                var potentialRegional = $"{parts[parts.Length - 2]}-{lastPart}";
                if (IsLanguageCode(potentialRegional))
                    return potentialRegional;
            }
        }

        // Check if entire filename is a language code
        if (IsLanguageCode(fileName))
            return fileName;

        return null;
    }

    /// <summary>
    /// Extracts the base name from a file path.
    /// </summary>
    private string ExtractBaseName(string filePath, string languageCode)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Remove language code suffix
        if (fileName.EndsWith($".{languageCode}", StringComparison.OrdinalIgnoreCase))
            return fileName.Substring(0, fileName.Length - languageCode.Length - 1);

        if (fileName.EndsWith($"_{languageCode}", StringComparison.OrdinalIgnoreCase))
            return fileName.Substring(0, fileName.Length - languageCode.Length - 1);

        if (fileName.EndsWith($"-{languageCode}", StringComparison.OrdinalIgnoreCase))
            return fileName.Substring(0, fileName.Length - languageCode.Length - 1);

        if (fileName.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            return "resources";

        return fileName;
    }

    /// <summary>
    /// Checks if a string is a valid language code.
    /// </summary>
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
            return culture.LCID != 4096;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets auto-discovered configuration based on the files.
    /// </summary>
    public XliffDiscoveryResult DiscoverConfiguration(string path)
    {
        var result = new XliffDiscoveryResult();

        // Find XLIFF files, excluding backup/metadata directories
        var xliffFiles = Directory.GetFiles(path, "*.xliff", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.lrm{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.AltDirectorySeparatorChar}.lrm{Path.AltDirectorySeparatorChar}"))
            .ToList();
        var xlfFiles = Directory.GetFiles(path, "*.xlf", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.lrm{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.AltDirectorySeparatorChar}.lrm{Path.AltDirectorySeparatorChar}"))
            .ToList();

        result.FileExtension = xliffFiles.Count >= xlfFiles.Count ? ".xliff" : ".xlf";

        var allFiles = xliffFiles.Concat(xlfFiles).ToList();
        if (allFiles.Count == 0)
            return result;

        // Detect version from first file
        var firstFile = allFiles.FirstOrDefault();
        if (firstFile != null)
        {
            result.Version = _detector.DetectVersion(firstFile);
            var langInfo = ExtractLanguageInfo(firstFile);
            if (langInfo != null)
            {
                result.SourceLanguage = langInfo.IsDefault ? langInfo.Code : "en";
            }
        }

        // Collect all languages
        var languages = allFiles
            .Select(ExtractLanguageInfo)
            .Where(l => l != null)
            .Select(l => l!.Code)
            .Distinct()
            .ToList();
        result.Languages = languages;

        // Check if bilingual (files have both source and target)
        if (firstFile != null)
        {
            result.Bilingual = CheckIfBilingual(firstFile);
        }

        return result;
    }

    /// <summary>
    /// Checks if a file is bilingual (has both source and target elements with content).
    /// </summary>
    private bool CheckIfBilingual(string filePath)
    {
        try
        {
            var settings = XliffVersionDetector.CreateSafeXmlReaderSettings();
            using var stream = File.OpenRead(filePath);
            using var reader = XmlReader.Create(stream, settings);

            var hasSource = false;
            var hasTarget = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "source")
                        hasSource = true;
                    else if (reader.LocalName == "target")
                        hasTarget = true;

                    if (hasSource && hasTarget)
                        return true;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
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
/// Result of auto-discovering XLIFF configuration.
/// </summary>
public class XliffDiscoveryResult
{
    /// <summary>
    /// Detected XLIFF version (1.2 or 2.0).
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    /// Detected file extension (.xliff or .xlf).
    /// </summary>
    public string FileExtension { get; set; } = ".xliff";

    /// <summary>
    /// Detected source language code.
    /// </summary>
    public string? SourceLanguage { get; set; }

    /// <summary>
    /// Whether files are bilingual (contain both source and target).
    /// </summary>
    public bool Bilingual { get; set; }

    /// <summary>
    /// Discovered language codes.
    /// </summary>
    public List<string> Languages { get; set; } = new();
}
