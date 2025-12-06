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
using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// Detected JSON localization format.
/// </summary>
public enum DetectedJsonFormat
{
    /// <summary>Unable to determine format (empty directory or no valid files).</summary>
    Unknown,

    /// <summary>Standard LRM JSON format (basename.culture.json, {0} interpolation).</summary>
    Standard,

    /// <summary>i18next JSON format (culture.json, {{name}} interpolation, _one/_other plurals).</summary>
    I18next
}

/// <summary>
/// Auto-detects whether JSON localization files use i18next conventions or standard LRM format.
/// Uses scoring-based heuristics to analyze file naming and content patterns.
/// </summary>
public class JsonFormatDetector
{
    private static readonly Regex I18nextInterpolation = new(@"\{\{[^}]+\}\}", RegexOptions.Compiled);
    private static readonly Regex DotNetInterpolation = new(@"\{\d+\}", RegexOptions.Compiled);
    private static readonly Regex I18nextNesting = new(@"\$t\([^)]+\)", RegexOptions.Compiled);
    private static readonly string[] PluralSuffixes = { "_zero", "_one", "_two", "_few", "_many", "_other" };

    /// <summary>
    /// Detects the JSON format used in the specified directory.
    /// </summary>
    /// <param name="path">Directory containing JSON localization files.</param>
    /// <returns>Detected format based on file naming and content analysis.</returns>
    public DetectedJsonFormat Detect(string path)
    {
        if (!Directory.Exists(path))
            return DetectedJsonFormat.Unknown;

        var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!jsonFiles.Any())
            return DetectedJsonFormat.Unknown;

        int i18nextScore = 0;
        int standardScore = 0;

        // Check file naming convention
        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            // Pure culture code file (en.json, fr-FR.json) - strong i18next signal
            if (IsValidCultureCode(fileName))
            {
                i18nextScore += 2;
            }
            // basename.culture.json pattern - strong standard signal
            else if (fileName.Contains('.'))
            {
                var lastPart = fileName.Split('.').Last();
                if (IsValidCultureCode(lastPart))
                {
                    standardScore += 2;
                }
            }
        }

        // Sample content from first few files
        foreach (var file in jsonFiles.Take(3))
        {
            try
            {
                var content = File.ReadAllText(file);
                var (i18n, std) = AnalyzeContent(content);
                i18nextScore += i18n;
                standardScore += std;
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        // Determine winner - require minimum score of 3 for confident detection
        if (i18nextScore > standardScore && i18nextScore >= 3)
            return DetectedJsonFormat.I18next;
        if (standardScore > i18nextScore && standardScore >= 3)
            return DetectedJsonFormat.Standard;

        // Default to standard if unclear (backward compatibility)
        return DetectedJsonFormat.Standard;
    }

    /// <summary>
    /// Analyzes JSON content for format-specific patterns.
    /// </summary>
    private (int i18next, int standard) AnalyzeContent(string content)
    {
        int i18next = 0;
        int standard = 0;

        // Check interpolation patterns in raw content
        if (I18nextInterpolation.IsMatch(content))
            i18next += 2;
        if (DotNetInterpolation.IsMatch(content))
            standard += 2;

        // Check for $t() nesting references
        if (I18nextNesting.IsMatch(content))
            i18next += 2;

        // Parse JSON and analyze keys
        try
        {
            using var doc = JsonDocument.Parse(content);
            AnalyzeJsonElement(doc.RootElement, ref i18next, ref standard);
        }
        catch
        {
            // Invalid JSON, skip key analysis
        }

        return (i18next, standard);
    }

    /// <summary>
    /// Recursively analyzes JSON element for format-specific patterns.
    /// </summary>
    private void AnalyzeJsonElement(JsonElement element, ref int i18next, ref int standard)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in element.EnumerateObject())
        {
            // Skip meta/comment properties
            if (prop.Name.StartsWith("_"))
                continue;

            // Check for plural suffix keys (strong i18next signal)
            if (PluralSuffixes.Any(suffix => prop.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                i18next += 3;
            }

            // Check for namespace separator (i18next convention)
            if (prop.Name.Contains(':'))
            {
                i18next += 1;
            }

            // Check for dot notation in keys (could be either, slight preference for standard)
            if (prop.Name.Contains('.') && !prop.Name.Contains(':'))
            {
                standard += 1;
            }

            // Recurse into nested objects
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                AnalyzeJsonElement(prop.Value, ref i18next, ref standard);
            }
        }
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
}
