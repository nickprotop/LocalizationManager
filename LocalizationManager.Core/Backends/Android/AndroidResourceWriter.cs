// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Writes ResourceFile objects to Android strings.xml format.
/// Supports strings, plurals, and string-arrays.
/// </summary>
public partial class AndroidResourceWriter : IResourceWriter
{
    /// <summary>
    /// Marker for non-translatable strings.
    /// </summary>
    private const string TranslatableMarker = "[translatable=false]";

    /// <summary>
    /// Marker prefix for string-array entries.
    /// </summary>
    private const string StringArrayMarker = "[string-array:";

    private readonly string _resourceFileName;

    /// <summary>
    /// Creates a new Android resource writer.
    /// </summary>
    /// <param name="resourceFileName">The resource file name (default: "strings.xml")</param>
    public AndroidResourceWriter(string resourceFileName = "strings.xml")
    {
        _resourceFileName = resourceFileName;
    }

    /// <inheritdoc />
    public void Write(ResourceFile file)
    {
        if (string.IsNullOrEmpty(file.Language.FilePath))
            throw new ArgumentException("Language file path is required");

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("resources")
        );

        var root = doc.Root!;

        // Group string-array entries
        var arrayGroups = file.Entries
            .Where(e => e.Comment?.Contains(StringArrayMarker) == true)
            .GroupBy(e => ExtractArrayName(e.Key))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key!, g => g.OrderBy(e => ExtractArrayIndex(e.Key)).ToList());

        var writtenArrays = new HashSet<string>();

        foreach (var entry in file.Entries)
        {
            // Check if this is a string-array entry
            if (entry.Comment?.Contains(StringArrayMarker) == true)
            {
                var arrayName = ExtractArrayName(entry.Key);
                if (!string.IsNullOrEmpty(arrayName) &&
                    !writtenArrays.Contains(arrayName) &&
                    arrayGroups.TryGetValue(arrayName, out var items))
                {
                    WriteStringArrayElement(root, arrayName, items);
                    writtenArrays.Add(arrayName);
                }
                continue;
            }

            // Add comment if present (exclude markers)
            var comment = ExtractUserComment(entry.Comment);
            if (!string.IsNullOrEmpty(comment))
            {
                root.Add(new XComment($" {comment} "));
            }

            if (entry.IsPlural && entry.PluralForms?.Count > 0)
            {
                WritePluralsElement(root, entry);
            }
            else
            {
                WriteStringElement(root, entry);
            }
        }

        // Save with proper formatting
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(file.Language.FilePath, settings);
        doc.Save(writer);
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
        // Determine the res folder
        var resPath = FindOrCreateResFolder(targetPath);

        // Create the values folder
        var folderName = AndroidCultureMapper.CodeToFolder(cultureCode);
        var folderPath = Path.Combine(resPath, folderName);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, _resourceFileName);

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
        if (!string.IsNullOrEmpty(language.FilePath) && File.Exists(language.FilePath))
        {
            File.Delete(language.FilePath);

            // Also delete the folder if empty
            var folder = Path.GetDirectoryName(language.FilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) &&
                !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a string element.
    /// </summary>
    private void WriteStringElement(XElement root, ResourceEntry entry)
    {
        var element = new XElement("string",
            new XAttribute("name", entry.Key),
            new XText(EscapeAndroidString(entry.Value ?? "")));

        // Add translatable="false" if marked
        if (entry.Comment?.Contains(TranslatableMarker) == true)
        {
            element.Add(new XAttribute("translatable", "false"));
        }

        root.Add(element);
    }

    /// <summary>
    /// Writes a plurals element.
    /// </summary>
    private void WritePluralsElement(XElement root, ResourceEntry entry)
    {
        var element = new XElement("plurals",
            new XAttribute("name", entry.Key));

        if (entry.PluralForms != null)
        {
            // Write in CLDR order
            var categories = new[] { "zero", "one", "two", "few", "many", "other" };
            foreach (var category in categories)
            {
                if (entry.PluralForms.TryGetValue(category, out var value))
                {
                    element.Add(new XElement("item",
                        new XAttribute("quantity", category),
                        new XText(EscapeAndroidString(value))));
                }
            }
        }

        root.Add(element);
    }

    /// <summary>
    /// Writes a string-array element.
    /// </summary>
    private void WriteStringArrayElement(XElement root, string arrayName, List<ResourceEntry> items)
    {
        // Extract user comment from first item
        var firstComment = items.FirstOrDefault()?.Comment;
        var userComment = ExtractUserComment(firstComment?.Replace($"{StringArrayMarker}{arrayName}]", ""));
        if (!string.IsNullOrEmpty(userComment))
        {
            root.Add(new XComment($" {userComment} "));
        }

        var element = new XElement("string-array",
            new XAttribute("name", arrayName));

        foreach (var item in items)
        {
            element.Add(new XElement("item", new XText(EscapeAndroidString(item.Value ?? ""))));
        }

        root.Add(element);
    }

    /// <summary>
    /// Extracts the array name from a key like "colors[0]".
    /// </summary>
    private static string? ExtractArrayName(string key)
    {
        var match = ArrayKeyPattern().Match(key);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts the array index from a key like "colors[0]".
    /// </summary>
    private static int ExtractArrayIndex(string key)
    {
        var match = ArrayKeyPattern().Match(key);
        if (match.Success && int.TryParse(match.Groups[2].Value, out var index))
            return index;
        return 0;
    }

    /// <summary>
    /// Extracts user comment, removing markers.
    /// </summary>
    private static string? ExtractUserComment(string? comment)
    {
        if (string.IsNullOrEmpty(comment))
            return null;

        var result = comment;

        // Remove translatable marker
        result = result.Replace(TranslatableMarker, "").Trim();

        // Remove string-array markers
        var arrayMatch = StringArrayMarkerPattern().Match(result);
        if (arrayMatch.Success)
        {
            result = result.Replace(arrayMatch.Value, "").Trim();
        }

        // Clean up separator
        result = result.Trim(' ', '|').Trim();

        return string.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Escapes a string for Android XML format.
    /// </summary>
    private static string EscapeAndroidString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("@", "\\@")
            .Replace("?", "\\?");
    }

    /// <summary>
    /// Finds or creates the res folder.
    /// </summary>
    private static string FindOrCreateResFolder(string targetPath)
    {
        // If path is already the res folder
        if (Path.GetFileName(targetPath).Equals("res", StringComparison.OrdinalIgnoreCase))
            return targetPath;

        // Check for existing res folder
        var resPath = Path.Combine(targetPath, "res");
        if (Directory.Exists(resPath))
            return resPath;

        // Check for app/src/main/res
        var mainResPath = Path.Combine(targetPath, "app", "src", "main", "res");
        if (Directory.Exists(mainResPath))
            return mainResPath;

        // Create res folder
        Directory.CreateDirectory(resPath);
        return resPath;
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
            var culture = System.Globalization.CultureInfo.GetCultureInfo(code);
            return $"{culture.NativeName} ({code})";
        }
        catch
        {
            return code.ToUpperInvariant();
        }
    }

    [GeneratedRegex(@"^(.+)\[(\d+)\]$")]
    private static partial Regex ArrayKeyPattern();

    [GeneratedRegex(@"\[string-array:[^\]]+\]")]
    private static partial Regex StringArrayMarkerPattern();
}
