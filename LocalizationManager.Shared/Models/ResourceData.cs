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

namespace LocalizationManager.Shared.Models;

/// <summary>
/// Represents resource data in memory for processing in flows.
/// </summary>
public class ResourceData
{
    /// <summary>
    /// The default language code (e.g., "en").
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// Dictionary of language code to resource entries.
    /// Key: Language code (e.g., "en", "el", "fr")
    /// Value: Dictionary of key-value pairs for that language
    /// </summary>
    public Dictionary<string, Dictionary<string, FlowResourceEntry>> Languages { get; set; } = new();

    /// <summary>
    /// Adds or updates a key-value pair for a specific language.
    /// </summary>
    public void SetValue(string language, string key, string value, string? comment = null)
    {
        if (!Languages.ContainsKey(language))
        {
            Languages[language] = new Dictionary<string, FlowResourceEntry>();
        }

        Languages[language][key] = new FlowResourceEntry
        {
            Key = key,
            Value = value,
            Comment = comment
        };
    }

    /// <summary>
    /// Gets all keys across all languages.
    /// </summary>
    public HashSet<string> GetAllKeys()
    {
        var keys = new HashSet<string>();
        foreach (var lang in Languages.Values)
        {
            foreach (var key in lang.Keys)
            {
                keys.Add(key);
            }
        }
        return keys;
    }

    /// <summary>
    /// Gets the value for a specific key in a specific language.
    /// </summary>
    public string? GetValue(string language, string key)
    {
        if (Languages.TryGetValue(language, out var langData))
        {
            if (langData.TryGetValue(key, out var entry))
            {
                return entry.Value;
            }
        }
        return null;
    }
}

/// <summary>
/// Represents a single resource entry for flow processing.
/// </summary>
public class FlowResourceEntry
{
    /// <summary>
    /// The resource key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The resource value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional comment for the resource entry.
    /// </summary>
    public string? Comment { get; set; }
}
