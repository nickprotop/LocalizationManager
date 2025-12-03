// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.JsonLocalization.Core.Models;

namespace LocalizationManager.JsonLocalization.Core;

/// <summary>
/// Reads JSON localization content and parses it into ResourceFile objects.
/// Supports both flat and nested key structures, comments, and plural forms.
/// </summary>
public class JsonResourceReader
{
    private readonly JsonFormatConfiguration _config;

    /// <summary>
    /// Creates a new JSON resource reader with optional configuration.
    /// </summary>
    public JsonResourceReader(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
    }

    /// <summary>
    /// Parses JSON content from a stream into a ResourceFile.
    /// </summary>
    public ResourceFile Read(Stream stream, LanguageInfo language)
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return Parse(content, language);
    }

    /// <summary>
    /// Parses JSON content string into a ResourceFile.
    /// </summary>
    public ResourceFile Parse(string content, LanguageInfo language)
    {
        var entries = ParseJson(content);
        return new ResourceFile
        {
            Language = language,
            Entries = entries
        };
    }

    /// <summary>
    /// Parses JSON content into a list of resource entries.
    /// </summary>
    private List<ResourceEntry> ParseJson(string content)
    {
        var entries = new List<ResourceEntry>();

        using var doc = JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        ParseElement(doc.RootElement, "", entries);

        return entries;
    }

    /// <summary>
    /// Recursively parses a JSON element and extracts resource entries.
    /// </summary>
    private void ParseElement(JsonElement element, string prefix, List<ResourceEntry> entries)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in element.EnumerateObject())
        {
            // Skip meta/internal properties (those starting with _)
            if (prop.Name.StartsWith("_"))
                continue;

            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = prop.Value.GetString() ?? ""
                    });
                    break;

                case JsonValueKind.Object:
                    ParseObjectValue(prop.Value, key, entries);
                    break;

                case JsonValueKind.Null:
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = ""
                    });
                    break;

                case JsonValueKind.Number:
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = prop.Value.GetRawText()
                    });
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = prop.Value.GetBoolean().ToString().ToLowerInvariant()
                    });
                    break;

                case JsonValueKind.Array:
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = prop.Value.GetRawText(),
                        Comment = "[array]"
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Parses an object value, which can be:
    /// 1. A value with metadata (_value, _comment)
    /// 2. A plural form entry (_plural or i18next style)
    /// 3. A nested object to recurse into
    /// </summary>
    private void ParseObjectValue(JsonElement element, string key, List<ResourceEntry> entries)
    {
        // Check for _value (explicit value with optional metadata)
        if (element.TryGetProperty("_value", out var valueElement))
        {
            var value = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString() ?? ""
                : valueElement.GetRawText();

            var comment = element.TryGetProperty("_comment", out var commentElement)
                ? commentElement.GetString()
                : null;

            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = value,
                Comment = comment
            });
            return;
        }

        // Check for _plural (LRM-style plurals)
        if (element.TryGetProperty("_plural", out _))
        {
            var pluralValue = SerializePluralValue(element, isLrmStyle: true);
            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = pluralValue,
                Comment = "[plural]"
            });
            return;
        }

        // Check for CLDR-style plurals (one, other, few, many, zero, two)
        // This works regardless of i18next compatibility mode
        if (HasCLDRPluralForms(element))
        {
            var pluralValue = SerializePluralValue(element, isLrmStyle: false);
            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = pluralValue,
                Comment = "[plural]"
            });
            return;
        }

        // Not a special object - recurse into nested structure
        ParseElement(element, key, entries);
    }

    /// <summary>
    /// Checks if an object has CLDR-style plural form keys (one, other, zero, two, few, many).
    /// </summary>
    private bool HasCLDRPluralForms(JsonElement element)
    {
        var pluralSuffixes = new[] { "one", "other", "zero", "two", "few", "many" };
        var foundForms = 0;

        foreach (var prop in element.EnumerateObject())
        {
            if (pluralSuffixes.Contains(prop.Name, StringComparer.OrdinalIgnoreCase) &&
                prop.Value.ValueKind == JsonValueKind.String)
            {
                foundForms++;
            }
        }

        return foundForms >= 2;
    }

    /// <summary>
    /// Serializes plural forms to a JSON string for storage.
    /// </summary>
    private string SerializePluralValue(JsonElement element, bool isLrmStyle)
    {
        var pluralForms = new Dictionary<string, string>();

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.StartsWith("_"))
                continue;

            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                pluralForms[prop.Name] = prop.Value.GetString() ?? "";
            }
            else if (isLrmStyle && prop.Name == "_plural" && prop.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var pluralProp in prop.Value.EnumerateObject())
                {
                    if (pluralProp.Value.ValueKind == JsonValueKind.String)
                    {
                        pluralForms[pluralProp.Name] = pluralProp.Value.GetString() ?? "";
                    }
                }
            }
        }

        return JsonSerializer.Serialize(pluralForms);
    }
}
