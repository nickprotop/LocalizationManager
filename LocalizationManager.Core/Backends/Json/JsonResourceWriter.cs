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

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// Writes ResourceFile objects to JSON localization files.
/// Supports nested keys, comments, plural forms, and metadata.
/// </summary>
public class JsonResourceWriter : IResourceWriter
{
    private readonly JsonFormatConfiguration _config;
    private readonly bool _isI18nextMode;

    public JsonResourceWriter(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
        _isI18nextMode = _config.I18nextCompatible;
    }

    /// <inheritdoc />
    public void Write(ResourceFile file)
    {
        var json = BuildJsonString(file);
        File.WriteAllText(file.Language.FilePath, json);
    }

    /// <summary>
    /// Builds the JSON string from the resource file without writing to disk.
    /// </summary>
    private string BuildJsonString(ResourceFile file)
    {
        var root = new Dictionary<string, object>();

        // Add meta if configured
        if (_config.IncludeMeta)
        {
            var meta = new Dictionary<string, object>
            {
                ["version"] = "1.0",
                ["generator"] = "LocalizationManager",
                ["updatedAt"] = DateTime.UtcNow.ToString("O")
            };

            // Add culture info (use actual code or empty for default)
            if (!string.IsNullOrEmpty(file.Language.Code))
            {
                meta["culture"] = file.Language.Code;
            }

            // For i18next mode, add isDefault flag if this is the default language
            if (_isI18nextMode && file.Language.IsDefault)
            {
                meta["isDefault"] = true;
            }

            root["_meta"] = meta;
        }

        // Process entries (preserve original order)
        foreach (var entry in file.Entries)
        {
            // In i18next mode, expand plurals to flat keys with suffixes
            if (_isI18nextMode && entry.IsPlural && entry.PluralForms != null && entry.PluralForms.Count > 0)
            {
                WriteI18nextPluralEntries(root, entry);
            }
            else
            {
                var value = CreateEntryValue(entry);

                if (_config.UseNestedKeys && entry.Key.Contains('.'))
                {
                    SetNestedValue(root, entry.Key.Split('.'), value);
                }
                else
                {
                    root[entry.Key] = value;
                }
            }
        }

        return JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
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
        // Determine filename based on mode
        string fileName;
        if (_isI18nextMode)
        {
            // i18next mode: {culture}.json (e.g., en.json, fr.json)
            fileName = $"{cultureCode}.json";
        }
        else
        {
            // Standard mode: {baseName}.{culture}.json (e.g., strings.fr.json)
            var cultureSuffix = string.IsNullOrEmpty(cultureCode)
                ? ""
                : $".{cultureCode}";
            fileName = $"{baseName}{cultureSuffix}.json";
        }

        var filePath = Path.Combine(targetPath, fileName);

        // Create entries (empty values, preserve comments)
        var entries = copyEntries && sourceFile != null
            ? sourceFile.Entries.Select(e => new ResourceEntry
            {
                Key = e.Key,
                Value = "",
                Comment = e.Comment
            }).ToList()
            : new List<ResourceEntry>();

        var newFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = GetCultureDisplayName(cultureCode),
                IsDefault = string.IsNullOrEmpty(cultureCode),
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
        if (File.Exists(language.FilePath))
            File.Delete(language.FilePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string SerializeToString(ResourceFile file)
    {
        return BuildJsonString(file);
    }

    /// <summary>
    /// Creates the appropriate value representation for a resource entry.
    /// </summary>
    private object CreateEntryValue(ResourceEntry entry)
    {
        // Check if plural (use IsPlural flag)
        if (entry.IsPlural && entry.PluralForms != null && entry.PluralForms.Count > 0)
        {
            return CreatePluralValue(entry);
        }

        // Check if array (stored as JSON string) - legacy support
        if (entry.Comment == "[array]" && entry.Value?.StartsWith("[") == true)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(entry.Value);
            }
            catch
            {
                return entry.Value ?? "";
            }
        }

        // Check if has comment and comments should be preserved
        if (_config.PreserveComments && !string.IsNullOrEmpty(entry.Comment))
        {
            return new Dictionary<string, string?>
            {
                ["_value"] = entry.Value,
                ["_comment"] = entry.Comment
            };
        }

        return entry.Value ?? "";
    }

    /// <summary>
    /// Writes i18next-style plural entries as flat keys with suffixes (e.g., key_one, key_other).
    /// </summary>
    private void WriteI18nextPluralEntries(Dictionary<string, object> root, ResourceEntry entry)
    {
        if (entry.PluralForms == null) return;

        foreach (var form in entry.PluralForms)
        {
            // Create flat key with suffix: {key}_{form} (e.g., items_one, items_other)
            var flatKey = $"{entry.Key}_{form.Key}";
            root[flatKey] = form.Value;
        }

        // Write comment as a separate key if configured
        if (_config.PreserveComments && !string.IsNullOrEmpty(entry.Comment))
        {
            root[$"_{entry.Key}_comment"] = entry.Comment;
        }
    }

    /// <summary>
    /// Creates the plural value object from the entry's PluralForms dictionary.
    /// Used for non-i18next modes with nested CLDR plural structure.
    /// </summary>
    private Dictionary<string, object> CreatePluralValue(ResourceEntry entry)
    {
        var result = new Dictionary<string, object> { ["_plural"] = true };

        if (entry.PluralForms != null)
        {
            foreach (var kv in entry.PluralForms)
            {
                result[kv.Key] = kv.Value;
            }
        }

        // Include comment if present
        if (_config.PreserveComments && !string.IsNullOrEmpty(entry.Comment))
        {
            result["_comment"] = entry.Comment;
        }

        return result;
    }

    /// <summary>
    /// Sets a value at a nested path in the dictionary.
    /// </summary>
    private void SetNestedValue(Dictionary<string, object> root, string[] path, object value)
    {
        var current = root;

        for (int i = 0; i < path.Length - 1; i++)
        {
            if (!current.TryGetValue(path[i], out var next) || next is not Dictionary<string, object>)
            {
                next = new Dictionary<string, object>();
                current[path[i]] = next;
            }
            current = (Dictionary<string, object>)next;
        }

        current[path[^1]] = value;
    }

    /// <summary>
    /// Gets a display-friendly name for a culture code.
    /// </summary>
    private string GetCultureDisplayName(string code)
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
            return code.ToUpper();
        }
    }
}
