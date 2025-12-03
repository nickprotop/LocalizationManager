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
using System.Text.Json;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// Discovers JSON localization files in a directory.
/// Supports both standard LRM format (basename.culture.json) and i18next format (culture.json).
/// </summary>
public class JsonResourceDiscovery : IResourceDiscovery
{
    private readonly JsonFormatConfiguration? _config;

    public JsonResourceDiscovery(JsonFormatConfiguration? config = null)
    {
        _config = config;
    }

    /// <inheritdoc />
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        var result = new List<LanguageInfo>();

        if (!Directory.Exists(searchPath))
            return result;

        // Load configuration from path if not provided
        var config = _config ?? LoadConfigFromPath(searchPath);
        var defaultLanguageCode = LoadDefaultLanguageFromPath(searchPath);

        var jsonFiles = Directory.GetFiles(searchPath, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!jsonFiles.Any())
            return result;

        // Determine if we're in i18next mode
        bool isI18next = config?.I18nextCompatible ?? false;

        if (isI18next)
        {
            // i18next mode: files are named as culture codes (en.json, fr.json)
            result = DiscoverI18nextFiles(jsonFiles, config?.BaseName ?? "strings", defaultLanguageCode);
        }
        else
        {
            // Standard mode: files are named basename.culture.json
            result = DiscoverStandardFiles(jsonFiles, defaultLanguageCode);
        }

        // Sort: default language first, then alphabetically by code
        return result
            .OrderBy(l => l.IsDefault ? 0 : 1)
            .ThenBy(l => l.Code)
            .ToList();
    }

    /// <summary>
    /// Loads JSON format configuration from lrm.json in the resource path.
    /// </summary>
    private JsonFormatConfiguration? LoadConfigFromPath(string searchPath)
    {
        try
        {
            var (config, _) = Configuration.ConfigurationManager.LoadConfiguration(null, searchPath);
            return config?.Json;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads the default language code from lrm.json in the resource path.
    /// </summary>
    private string? LoadDefaultLanguageFromPath(string searchPath)
    {
        try
        {
            var (config, _) = Configuration.ConfigurationManager.LoadConfiguration(null, searchPath);
            return config?.DefaultLanguageCode;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Discovers files in standard LRM format (basename.culture.json).
    /// </summary>
    private List<LanguageInfo> DiscoverStandardFiles(List<string> jsonFiles, string? configDefaultLanguage)
    {
        var result = new List<LanguageInfo>();

        // Group by base name
        var groups = jsonFiles
            .Select(f => ParseStandardFileName(f))
            .Where(x => x != null)
            .GroupBy(x => x!.Value.BaseName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var filesInGroup = group.ToList();

            foreach (var file in filesInGroup)
            {
                // In standard format, empty culture code means default
                var isDefault = string.IsNullOrEmpty(file!.Value.CultureCode);
                var cultureCode = file.Value.CultureCode;
                var displayName = isDefault ? "Default" : GetCultureDisplayName(cultureCode);

                result.Add(new LanguageInfo
                {
                    BaseName = file.Value.BaseName,
                    Code = cultureCode,
                    Name = displayName,
                    IsDefault = isDefault,
                    FilePath = file.Value.FilePath
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Discovers files in i18next format (culture.json).
    /// Uses smart detection to determine the default language.
    /// </summary>
    private List<LanguageInfo> DiscoverI18nextFiles(List<string> jsonFiles, string baseName, string? configDefaultLanguage)
    {
        var candidates = new List<(string FilePath, string CultureCode, int KeyCount, bool HasMetaDefault)>();

        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            // Check if filename is a valid culture code
            if (IsValidCultureCode(fileName))
            {
                var (keyCount, hasMetaDefault) = AnalyzeJsonFile(file);
                candidates.Add((file, fileName, keyCount, hasMetaDefault));
            }
        }

        if (!candidates.Any())
            return new List<LanguageInfo>();

        // Determine which file is the default using priority-based detection
        var defaultCultureCode = DetermineDefaultLanguage(candidates, configDefaultLanguage);

        var result = new List<LanguageInfo>();
        foreach (var (filePath, cultureCode, _, _) in candidates)
        {
            var isDefault = cultureCode.Equals(defaultCultureCode, StringComparison.OrdinalIgnoreCase);

            result.Add(new LanguageInfo
            {
                BaseName = baseName,
                Code = isDefault ? "" : cultureCode,
                Name = isDefault ? "Default" : GetCultureDisplayName(cultureCode),
                IsDefault = isDefault,
                FilePath = filePath
            });
        }

        return result;
    }

    /// <summary>
    /// Determines the default language using priority-based detection.
    /// Priority: 1) Config, 2) _meta.isDefault, 3) Most keys, 4) English, 5) First alphabetically
    /// </summary>
    private string DetermineDefaultLanguage(
        List<(string FilePath, string CultureCode, int KeyCount, bool HasMetaDefault)> candidates,
        string? configDefaultLanguage)
    {
        // Priority 1: Explicit configuration
        if (!string.IsNullOrEmpty(configDefaultLanguage))
        {
            var configMatch = candidates.FirstOrDefault(c =>
                c.CultureCode.Equals(configDefaultLanguage, StringComparison.OrdinalIgnoreCase));
            if (configMatch != default)
                return configMatch.CultureCode;
        }

        // Priority 2: File with _meta.isDefault: true
        var metaDefault = candidates.FirstOrDefault(c => c.HasMetaDefault);
        if (metaDefault != default)
            return metaDefault.CultureCode;

        // Priority 3: File with most keys (source language usually has all keys)
        var sortedByKeys = candidates.OrderByDescending(c => c.KeyCount).ToList();
        if (sortedByKeys.Count >= 2 && sortedByKeys[0].KeyCount > sortedByKeys[1].KeyCount)
        {
            return sortedByKeys[0].CultureCode;
        }

        // Priority 4: Common English defaults
        var englishCodes = new[] { "en", "en-US", "en-GB" };
        foreach (var englishCode in englishCodes)
        {
            var englishMatch = candidates.FirstOrDefault(c =>
                c.CultureCode.Equals(englishCode, StringComparison.OrdinalIgnoreCase));
            if (englishMatch != default)
                return englishMatch.CultureCode;
        }

        // Priority 5: First alphabetically
        return candidates.OrderBy(c => c.CultureCode).First().CultureCode;
    }

    /// <summary>
    /// Analyzes a JSON file to count keys and check for _meta.isDefault.
    /// </summary>
    private (int KeyCount, bool HasMetaDefault) AnalyzeJsonFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(content);

            int keyCount = 0;
            bool hasMetaDefault = false;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "_meta" && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    // Check for isDefault in _meta
                    if (prop.Value.TryGetProperty("isDefault", out var isDefaultProp) &&
                        isDefaultProp.ValueKind == JsonValueKind.True)
                    {
                        hasMetaDefault = true;
                    }
                }
                else if (!prop.Name.StartsWith("_"))
                {
                    // Count non-meta keys
                    keyCount += CountKeysRecursive(prop.Value);
                }
            }

            return (keyCount, hasMetaDefault);
        }
        catch
        {
            return (0, false);
        }
    }

    /// <summary>
    /// Recursively counts keys in a JSON element.
    /// </summary>
    private int CountKeysRecursive(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return 1;

        if (element.ValueKind == JsonValueKind.Object)
        {
            int count = 0;
            foreach (var prop in element.EnumerateObject())
            {
                if (!prop.Name.StartsWith("_"))
                    count += CountKeysRecursive(prop.Value);
            }
            return count;
        }

        return 0;
    }

    /// <summary>
    /// Parses a standard format filename (basename.culture.json or basename.json).
    /// </summary>
    private (string BaseName, string CultureCode, string FilePath)? ParseStandardFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Pattern: {baseName}.{cultureCode}.json or {baseName}.json
        var parts = fileName.Split('.');

        if (parts.Length == 1)
        {
            // No culture code: strings.json (default language)
            return (parts[0], "", filePath);
        }

        // Try to find a valid culture code from the end of parts
        // This handles: strings.en, strings.en-US, strings.zh-Hans, strings.zh-Hans-CN
        for (int i = parts.Length - 1; i >= 1; i--)
        {
            // Try progressively longer culture codes from the end
            var potentialCulture = string.Join("-", parts.Skip(i));
            if (IsValidCultureCode(potentialCulture))
            {
                var baseName = string.Join(".", parts.Take(i));
                return (baseName, potentialCulture, filePath);
            }
        }

        // No valid culture found - treat entire filename as base name (default language)
        return (fileName, "", filePath);
    }

    /// <summary>
    /// Validates whether a string is a valid .NET culture code.
    /// </summary>
    private bool IsValidCultureCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return culture != null && !string.IsNullOrEmpty(culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
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

    /// <inheritdoc />
    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(DiscoverLanguages(searchPath));
}
