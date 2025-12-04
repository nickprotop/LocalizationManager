// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text.Json;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Exceptions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// Reads JSON localization files and parses them into ResourceFile objects.
/// Supports both flat and nested key structures, comments, and plural forms.
/// </summary>
public class JsonResourceReader : IResourceReader
{
    private readonly JsonFormatConfiguration _config;

    public JsonResourceReader(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
    }

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        if (!File.Exists(language.FilePath))
        {
            throw new ResourceNotFoundException(
                $"JSON resource file not found: {language.FilePath}",
                language.FilePath);
        }

        try
        {
            var content = File.ReadAllText(language.FilePath);
            var entries = ParseJson(content);

            return new ResourceFile
            {
                Language = language,
                Entries = entries
            };
        }
        catch (JsonException ex)
        {
            throw new ResourceParseException(
                $"Failed to parse JSON file: {language.FilePath}. {ex.Message}",
                language.FilePath,
                lineNumber: (int?)ex.LineNumber,
                position: (int?)ex.BytePositionInLine,
                inner: ex);
        }
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));

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
                    // Simple string value
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
                    // Null value - treat as empty string
                    entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = ""
                    });
                    break;

                // Numbers and booleans - convert to string
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

                // Arrays - serialize to JSON string
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
    /// 2. A plural form entry (_plural or i18next _one/_other)
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
            var pluralForms = ExtractPluralForms(element, isLrmStyle: true);
            var comment = element.TryGetProperty("_comment", out var commentElement)
                ? commentElement.GetString()
                : null;
            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = pluralForms.GetValueOrDefault("other") ?? pluralForms.Values.FirstOrDefault(),
                Comment = comment,
                IsPlural = true,
                PluralForms = pluralForms
            });
            return;
        }

        // Check for i18next-style plurals (keys ending with _one, _other, etc.)
        if (_config.I18nextCompatible && HasI18nextPluralSiblings(element))
        {
            var pluralForms = ExtractPluralForms(element, isLrmStyle: false);
            entries.Add(new ResourceEntry
            {
                Key = key,
                Value = pluralForms.GetValueOrDefault("other") ?? pluralForms.Values.FirstOrDefault(),
                IsPlural = true,
                PluralForms = pluralForms
            });
            return;
        }

        // Not a special object - recurse into nested structure
        ParseElement(element, key, entries);
    }

    /// <summary>
    /// Checks if an object has i18next-style plural form keys.
    /// </summary>
    private bool HasI18nextPluralSiblings(JsonElement element)
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

        // Need at least 2 plural forms to consider it a plural entry
        return foundForms >= 2;
    }

    /// <summary>
    /// Extracts plural forms from a JSON element into a dictionary.
    /// </summary>
    private Dictionary<string, string> ExtractPluralForms(JsonElement element, bool isLrmStyle)
    {
        var pluralForms = new Dictionary<string, string>();

        foreach (var prop in element.EnumerateObject())
        {
            // Skip internal properties (except we need to check _plural for LRM style)
            if (prop.Name.StartsWith("_") && prop.Name != "_plural")
                continue;

            // For LRM style, get values from under _plural
            // For i18next style, get values directly
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                pluralForms[prop.Name] = prop.Value.GetString() ?? "";
            }
            else if (isLrmStyle && prop.Name == "_plural" && prop.Value.ValueKind == JsonValueKind.Object)
            {
                // LRM style: {"_plural": {"one": "...", "other": "..."}}
                foreach (var pluralProp in prop.Value.EnumerateObject())
                {
                    if (pluralProp.Value.ValueKind == JsonValueKind.String)
                    {
                        pluralForms[pluralProp.Name] = pluralProp.Value.GetString() ?? "";
                    }
                }
            }
        }

        return pluralForms;
    }
}
