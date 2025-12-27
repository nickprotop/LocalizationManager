using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Android;
using LocalizationManager.Core.Backends.iOS;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using StringsdictParser = LocalizationManager.Core.Backends.iOS.StringsdictParser;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for importing/parsing translation files from GitHub.
/// Inverse of FileExportService - parses file content into entries.
/// </summary>
public class FileImportService : IFileImportService
{
    private readonly ILogger<FileImportService> _logger;

    public FileImportService(ILogger<FileImportService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ParsedResourceFile ParseFile(
        string format,
        string filePath,
        string content,
        string defaultLanguage)
    {
        var (languageCode, isDefault) = DetectLanguageFromPath(format, filePath, defaultLanguage);
        var reader = GetReader(format);

        var languageInfo = new LanguageInfo
        {
            Code = languageCode,
            IsDefault = isDefault,
            Name = languageCode
        };

        ResourceFile resourceFile;
        using (var stringReader = new StringReader(content))
        {
            resourceFile = reader.Read(stringReader, languageInfo);
        }

        var entries = new List<GitHubEntry>();

        foreach (var entry in resourceFile.Entries)
        {
            if (entry.IsPlural && entry.PluralForms?.Count > 0)
            {
                // For plural entries, compute a single hash for all plural forms combined
                var pluralHash = EntryHasher.ComputePluralHash(entry.PluralForms, entry.Comment);

                // Create an entry for each plural form, all sharing the same combined hash
                foreach (var (pluralForm, value) in entry.PluralForms)
                {
                    entries.Add(new GitHubEntry
                    {
                        Key = entry.Key,
                        LanguageCode = languageCode,
                        PluralForm = pluralForm,
                        Value = value,
                        Comment = entry.Comment,
                        IsPlural = true,
                        PluralForms = entry.PluralForms,
                        Hash = pluralHash
                    });
                }
            }
            else
            {
                // Simple string entry
                var hash = EntryHasher.ComputeHash(entry.Value ?? "", entry.Comment);
                entries.Add(new GitHubEntry
                {
                    Key = entry.Key,
                    LanguageCode = languageCode,
                    PluralForm = "",
                    Value = entry.Value,
                    Comment = entry.Comment,
                    IsPlural = false,
                    Hash = hash
                });
            }
        }

        _logger.LogDebug(
            "Parsed {EntryCount} entries from {FilePath} (language: {Language}, isDefault: {IsDefault})",
            entries.Count, filePath, languageCode, isDefault);

        return new ParsedResourceFile
        {
            FilePath = filePath,
            LanguageCode = languageCode,
            IsDefault = isDefault,
            Entries = entries
        };
    }

    /// <inheritdoc />
    public Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> ParseFiles(
        string format,
        Dictionary<string, string> files,
        string defaultLanguage)
    {
        var result = new Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry>();

        // For iOS, handle .stringsdict files specially to parse plurals
        if (format.Equals("ios", StringComparison.OrdinalIgnoreCase))
        {
            ParseIosFilesWithPlurals(files, defaultLanguage, result);
        }
        else
        {
            foreach (var (filePath, content) in files)
            {
                try
                {
                    var parsed = ParseFile(format, filePath, content, defaultLanguage);

                    foreach (var entry in parsed.Entries)
                    {
                        var key = (entry.Key, entry.LanguageCode, entry.PluralForm);
                        result[key] = entry;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse file {FilePath}, skipping", filePath);
                }
            }
        }

        _logger.LogInformation(
            "Parsed {TotalEntries} entries from {FileCount} files",
            result.Count, files.Count);

        return result;
    }

    /// <summary>
    /// Parses iOS files including .stringsdict plurals.
    /// </summary>
    private void ParseIosFilesWithPlurals(
        Dictionary<string, string> files,
        string defaultLanguage,
        Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> result)
    {
        var stringsdictParser = new StringsdictParser();

        // First, parse all .strings files
        foreach (var (filePath, content) in files)
        {
            if (!filePath.EndsWith(".strings", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var parsed = ParseFile("ios", filePath, content, defaultLanguage);
                foreach (var entry in parsed.Entries)
                {
                    var key = (entry.Key, entry.LanguageCode, entry.PluralForm);
                    result[key] = entry;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse iOS .strings file {FilePath}, skipping", filePath);
            }
        }

        // Then, parse all .stringsdict files and merge plurals
        foreach (var (filePath, content) in files)
        {
            if (!filePath.EndsWith(".stringsdict", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var dirName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
                var (languageCode, isDefault) = DetectIosLanguage(dirName, defaultLanguage);
                // Normalize language code to lowercase for consistency
                languageCode = languageCode.ToLowerInvariant();

                var pluralEntries = stringsdictParser.Parse(content);

                foreach (var entry in pluralEntries)
                {
                    // Compute a single hash for all plural forms combined
                    var pluralHash = EntryHasher.ComputePluralHash(entry.PluralForms, null);

                    // Create entries for each plural form, all sharing the same combined hash
                    foreach (var (pluralForm, value) in entry.PluralForms)
                    {
                        var key = (entry.Key, languageCode, pluralForm);

                        result[key] = new GitHubEntry
                        {
                            Key = entry.Key,
                            LanguageCode = languageCode,
                            PluralForm = pluralForm,
                            Value = value,
                            Comment = null,
                            IsPlural = true,
                            PluralForms = entry.PluralForms,
                            Hash = pluralHash
                        };
                    }

                    // Mark any existing singular entry for this key as plural
                    var singularKey = (entry.Key, languageCode, "");
                    if (result.TryGetValue(singularKey, out var existingEntry))
                    {
                        // Remove the singular entry since we now have plural forms
                        result.Remove(singularKey);
                    }
                }

                _logger.LogDebug(
                    "Parsed {PluralCount} plural entries from iOS .stringsdict file {FilePath}",
                    pluralEntries.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse iOS .stringsdict file {FilePath}, skipping", filePath);
            }
        }
    }

    /// <inheritdoc />
    public (string languageCode, bool isDefault) DetectLanguageFromPath(
        string format,
        string filePath,
        string defaultLanguage)
    {
        var fileName = Path.GetFileName(filePath);
        var dirName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");

        return format.ToLowerInvariant() switch
        {
            "resx" => DetectResxLanguage(fileName, defaultLanguage),
            "json" => DetectJsonLanguage(fileName, defaultLanguage),
            "i18next" => DetectI18nextLanguage(fileName, defaultLanguage),
            "android" => DetectAndroidLanguage(dirName, defaultLanguage),
            "ios" => DetectIosLanguage(dirName, defaultLanguage),
            _ => throw new NotSupportedException($"Format '{format}' is not supported for import")
        };
    }

    /// <summary>
    /// Normalizes a language code to lowercase for consistent comparison.
    /// </summary>
    private static string NormalizeLanguageCode(string languageCode)
    {
        return languageCode.ToLowerInvariant();
    }

    /// <summary>
    /// Detects language from RESX filename.
    /// Resources.resx → default, Resources.fr.resx → fr
    /// </summary>
    private static (string languageCode, bool isDefault) DetectResxLanguage(string fileName, string defaultLanguage)
    {
        // Normalize default language for comparison
        var normalizedDefault = NormalizeLanguageCode(defaultLanguage);

        // Resources.resx → default
        // Resources.fr.resx → fr
        var parts = fileName.Split('.');
        if (parts.Length >= 3)
        {
            // Resources.fr.resx → parts[1] = fr
            var lang = NormalizeLanguageCode(parts[^2]); // Second to last, normalized
            return (lang, lang.Equals(normalizedDefault, StringComparison.Ordinal));
        }
        // Resources.resx (only name.ext) → default language
        return (normalizedDefault, true);
    }

    /// <summary>
    /// Detects language from JSON filename.
    /// strings.json → default, strings.fr.json → fr
    /// </summary>
    private static (string languageCode, bool isDefault) DetectJsonLanguage(string fileName, string defaultLanguage)
    {
        // Normalize default language for comparison
        var normalizedDefault = NormalizeLanguageCode(defaultLanguage);

        // Same logic as RESX
        // strings.json → default
        // strings.fr.json → fr
        var parts = fileName.Split('.');
        if (parts.Length >= 3)
        {
            var lang = NormalizeLanguageCode(parts[^2]);
            return (lang, lang.Equals(normalizedDefault, StringComparison.Ordinal));
        }
        return (normalizedDefault, true);
    }

    /// <summary>
    /// Detects language from i18next filename.
    /// en.json → en, fr.json → fr
    /// Compares with project default language to determine isDefault.
    /// </summary>
    private static (string languageCode, bool isDefault) DetectI18nextLanguage(string fileName, string defaultLanguage)
    {
        // Normalize default language for comparison
        var normalizedDefault = NormalizeLanguageCode(defaultLanguage);

        // en.json → en (normalized)
        var lang = NormalizeLanguageCode(Path.GetFileNameWithoutExtension(fileName));
        var isDefault = lang.Equals(normalizedDefault, StringComparison.Ordinal);
        return (lang, isDefault);
    }

    /// <summary>
    /// Detects language from Android folder name.
    /// values → default, values-es → es, values-zh-rCN → zh-CN
    /// </summary>
    private static (string languageCode, bool isDefault) DetectAndroidLanguage(string dirName, string defaultLanguage)
    {
        // Normalize default language for comparison
        var normalizedDefault = NormalizeLanguageCode(defaultLanguage);

        if (string.IsNullOrEmpty(dirName))
            return (normalizedDefault, true);

        if (dirName.Equals("values", StringComparison.OrdinalIgnoreCase))
            return (normalizedDefault, true);

        if (dirName.StartsWith("values-", StringComparison.OrdinalIgnoreCase))
        {
            var lang = AndroidCultureMapper.FolderToCode(dirName);
            // If FolderToCode returns empty string, it's the default
            if (string.IsNullOrEmpty(lang))
                return (normalizedDefault, true);
            var normalizedLang = NormalizeLanguageCode(lang);
            return (normalizedLang, normalizedLang.Equals(normalizedDefault, StringComparison.Ordinal));
        }

        return (normalizedDefault, true);
    }

    /// <summary>
    /// Detects language from iOS lproj folder name.
    /// en.lproj → en, Base.lproj → default
    /// </summary>
    private static (string languageCode, bool isDefault) DetectIosLanguage(string dirName, string defaultLanguage)
    {
        // Normalize default language for comparison
        var normalizedDefault = NormalizeLanguageCode(defaultLanguage);

        if (string.IsNullOrEmpty(dirName))
            return (normalizedDefault, true);

        if (dirName.Equals("Base.lproj", StringComparison.OrdinalIgnoreCase))
            return (normalizedDefault, true);

        if (dirName.EndsWith(".lproj", StringComparison.OrdinalIgnoreCase))
        {
            var lang = IosCultureMapper.LprojToCode(dirName, defaultLanguage);
            if (string.IsNullOrEmpty(lang))
                return (normalizedDefault, true);
            var normalizedLang = NormalizeLanguageCode(lang);
            return (normalizedLang, normalizedLang.Equals(normalizedDefault, StringComparison.Ordinal));
        }

        return (normalizedDefault, true);
    }

    /// <summary>
    /// Gets the appropriate reader for the format.
    /// </summary>
    private static IResourceReader GetReader(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "resx" => new ResxResourceReader(),
            "json" => new JsonResourceReader(new JsonFormatConfiguration { UseNestedKeys = false }),
            "i18next" => new JsonResourceReader(new JsonFormatConfiguration { I18nextCompatible = true }),
            "android" => new AndroidResourceReader(),
            "ios" => new IosResourceReader(),
            _ => throw new NotSupportedException($"Format '{format}' is not supported for import")
        };
    }
}
