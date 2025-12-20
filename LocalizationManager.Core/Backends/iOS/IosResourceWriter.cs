// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Writes ResourceFile objects to iOS .strings and .stringsdict format.
/// Simple strings go to .strings, plurals go to .stringsdict.
/// </summary>
public class IosResourceWriter : IResourceWriter
{
    private readonly StringsFileParser _stringsParser = new();
    private readonly StringsdictParser _stringsdictParser = new();
    private readonly string _stringsFileName;

    /// <summary>
    /// Creates a new iOS resource writer.
    /// </summary>
    /// <param name="stringsFileName">The strings file name (default: "Localizable.strings")</param>
    public IosResourceWriter(string stringsFileName = "Localizable.strings")
    {
        _stringsFileName = stringsFileName;
    }

    /// <inheritdoc />
    public void Write(ResourceFile file)
    {
        if (string.IsNullOrEmpty(file.Language.FilePath))
            throw new ArgumentException("Language file path is required");

        // Separate simple strings from plurals
        var simpleStrings = file.Entries.Where(e => !e.IsPlural).ToList();
        var pluralStrings = file.Entries.Where(e => e.IsPlural && e.PluralForms?.Count > 0).ToList();

        // Determine file paths
        var stringsPath = file.Language.FilePath;
        if (stringsPath.EndsWith(".stringsdict", StringComparison.OrdinalIgnoreCase))
        {
            stringsPath = Path.ChangeExtension(stringsPath, ".strings");
        }
        var stringsdictPath = Path.ChangeExtension(stringsPath, ".stringsdict");

        // Write .strings file
        if (simpleStrings.Any())
        {
            var stringsEntries = simpleStrings.Select(e =>
                new StringsFileParser.StringsEntry(e.Key, e.Value ?? "", e.Comment));

            var content = _stringsParser.Serialize(stringsEntries);
            File.WriteAllText(stringsPath, content);
        }
        else if (!pluralStrings.Any())
        {
            // Create empty .strings file if no content at all (for new language files)
            File.WriteAllText(stringsPath, "");
        }
        else if (File.Exists(stringsPath))
        {
            // Remove empty strings file if there are plurals
            File.Delete(stringsPath);
        }

        // Write .stringsdict file
        if (pluralStrings.Any())
        {
            var stringsdictEntries = pluralStrings.Select(e =>
                new StringsdictParser.StringsdictEntry(
                    e.Key,
                    StringsdictParser.CreateFormatKey(),
                    DetectValueType(e.PluralForms!),
                    e.PluralForms!));

            var content = _stringsdictParser.Serialize(stringsdictEntries);
            File.WriteAllText(stringsdictPath, content);
        }
        else if (File.Exists(stringsdictPath))
        {
            // Remove empty stringsdict file
            File.Delete(stringsdictPath);
        }
    }

    /// <inheritdoc />
    public Task WriteAsync(ResourceFile file, CancellationToken ct = default)
    {
        Write(file);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateLanguageFileAsync(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true,
        CancellationToken ct = default)
    {
        // Create the .lproj folder
        var folderName = IosCultureMapper.CodeToLproj(cultureCode);
        var folderPath = Path.Combine(targetPath, folderName);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, _stringsFileName);

        // Create entries (copy from source or empty)
        var entries = new List<ResourceEntry>();
        if (copyEntries && sourceFile != null)
        {
            entries = sourceFile.Entries.Select(e => new ResourceEntry
            {
                Key = e.Key,
                Value = "",
                Comment = e.Comment,
                IsPlural = e.IsPlural,
                PluralForms = e.IsPlural ? new Dictionary<string, string>() : null
            }).ToList();
        }

        var newFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = GetCultureDisplayName(cultureCode),
                IsDefault = false,
                FilePath = filePath
            },
            Entries = entries
        };

        Write(newFile);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            return Task.CompletedTask;

        var stringsPath = language.FilePath;
        if (stringsPath.EndsWith(".stringsdict", StringComparison.OrdinalIgnoreCase))
        {
            stringsPath = Path.ChangeExtension(stringsPath, ".strings");
        }
        var stringsdictPath = Path.ChangeExtension(stringsPath, ".stringsdict");

        // Delete both files
        if (File.Exists(stringsPath))
            File.Delete(stringsPath);
        if (File.Exists(stringsdictPath))
            File.Delete(stringsdictPath);

        // Delete the folder if empty
        var folder = Path.GetDirectoryName(stringsPath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) &&
            !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Detects the value type from plural forms (d for integers, f for floats, @ for objects).
    /// </summary>
    private static string DetectValueType(Dictionary<string, string> pluralForms)
    {
        // Check any plural form for format specifiers
        var anyValue = pluralForms.Values.FirstOrDefault() ?? "";

        if (anyValue.Contains("%@"))
            return "@";
        if (anyValue.Contains("%f") || anyValue.Contains("%F"))
            return "f";
        if (anyValue.Contains("%s"))
            return "s";

        // Default to integer
        return "d";
    }

    /// <summary>
    /// Gets a display name for a culture code.
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
