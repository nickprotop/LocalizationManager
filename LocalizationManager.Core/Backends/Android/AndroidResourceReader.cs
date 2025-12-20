// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Reads Android strings.xml files and parses them into ResourceFile objects.
/// Supports strings, plurals, and string-arrays.
/// </summary>
public class AndroidResourceReader : IResourceReader
{
    /// <summary>
    /// Marker for non-translatable strings.
    /// </summary>
    private const string TranslatableMarker = "[translatable=false]";

    /// <summary>
    /// Marker prefix for string-array entries.
    /// </summary>
    private const string StringArrayMarker = "[string-array:";

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("Language file path is required", nameof(language));

        var content = File.ReadAllText(language.FilePath);
        using var reader = new StringReader(content);
        return Read(reader, language);
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
    {
        var content = reader.ReadToEnd();
        var entries = new List<ResourceEntry>();

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var xmlReader = XmlReader.Create(new StringReader(content), settings);
            var doc = XDocument.Load(xmlReader);
            var root = doc.Root;

            if (root?.Name != "resources")
            {
                return new ResourceFile { Language = metadata, Entries = entries };
            }

            string? pendingComment = null;

            foreach (var node in root.Nodes())
            {
                if (node is XComment comment)
                {
                    pendingComment = comment.Value.Trim();
                    continue;
                }

                if (node is XElement element)
                {
                    var parsedEntries = ParseElement(element, pendingComment);
                    entries.AddRange(parsedEntries);
                    pendingComment = null;
                }
            }
        }
        catch (XmlException)
        {
            // Invalid XML, return empty entries
        }

        return new ResourceFile { Language = metadata, Entries = entries };
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
        => Task.FromResult(Read(reader, metadata));

    /// <summary>
    /// Parses an XML element into resource entries.
    /// </summary>
    private IEnumerable<ResourceEntry> ParseElement(XElement element, string? pendingComment)
    {
        switch (element.Name.LocalName)
        {
            case "string":
                var stringEntry = ParseStringElement(element, pendingComment);
                if (stringEntry != null)
                    yield return stringEntry;
                break;

            case "plurals":
                var pluralEntry = ParsePluralsElement(element, pendingComment);
                if (pluralEntry != null)
                    yield return pluralEntry;
                break;

            case "string-array":
                foreach (var arrayEntry in ParseStringArrayElement(element, pendingComment))
                {
                    yield return arrayEntry;
                }
                break;
        }
    }

    /// <summary>
    /// Parses a string element.
    /// </summary>
    private ResourceEntry? ParseStringElement(XElement element, string? pendingComment)
    {
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            return null;

        var value = UnescapeAndroidString(GetElementText(element));
        var translatable = element.Attribute("translatable")?.Value;

        string? comment = pendingComment;

        // Add translatable marker if needed
        if (translatable?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
        {
            comment = string.IsNullOrEmpty(comment)
                ? TranslatableMarker
                : $"{comment} | {TranslatableMarker}";
        }

        return new ResourceEntry
        {
            Key = name,
            Value = value,
            Comment = comment
        };
    }

    /// <summary>
    /// Parses a plurals element.
    /// </summary>
    private ResourceEntry? ParsePluralsElement(XElement element, string? pendingComment)
    {
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            return null;

        var pluralForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in element.Elements("item"))
        {
            var quantity = item.Attribute("quantity")?.Value;
            if (!string.IsNullOrEmpty(quantity))
            {
                pluralForms[quantity] = UnescapeAndroidString(GetElementText(item));
            }
        }

        // Get the "other" form as the default value, or the first available
        var defaultValue = pluralForms.GetValueOrDefault("other") ??
                          pluralForms.Values.FirstOrDefault() ?? "";

        return new ResourceEntry
        {
            Key = name,
            Value = defaultValue,
            Comment = pendingComment,
            IsPlural = true,
            PluralForms = pluralForms
        };
    }

    /// <summary>
    /// Parses a string-array element into multiple entries.
    /// </summary>
    private IEnumerable<ResourceEntry> ParseStringArrayElement(XElement element, string? pendingComment)
    {
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            yield break;

        var items = element.Elements("item").ToList();
        var marker = $"{StringArrayMarker}{name}]";

        for (int i = 0; i < items.Count; i++)
        {
            var itemComment = i == 0 ? $"{pendingComment} | {marker}".TrimStart(' ', '|') : marker;

            yield return new ResourceEntry
            {
                Key = $"{name}[{i}]",
                Value = UnescapeAndroidString(GetElementText(items[i])),
                Comment = itemComment.Trim()
            };
        }
    }

    /// <summary>
    /// Gets the text content of an element, handling CDATA and mixed content.
    /// </summary>
    private static string GetElementText(XElement element)
    {
        // Handle CDATA
        var cdata = element.Nodes().OfType<XCData>().FirstOrDefault();
        if (cdata != null)
            return cdata.Value;

        // Get the inner XML for elements with nested markup (like <b> tags)
        if (element.HasElements)
        {
            using var reader = element.CreateReader();
            reader.MoveToContent();
            return reader.ReadInnerXml();
        }

        return element.Value;
    }

    /// <summary>
    /// Unescapes Android string format.
    /// </summary>
    private static string UnescapeAndroidString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\'", "'")
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\@", "@")
            .Replace("\\?", "?");
    }
}
