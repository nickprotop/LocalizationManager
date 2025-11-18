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

using LocalizationManager.Core.Models;
using LocalizationManager.Core.Validation;

namespace LocalizationManager.Core;

/// <summary>
/// Validates resource files for common issues like missing keys, duplicates, and empty values.
/// </summary>
public class ResourceValidator
{
    /// <summary>
    /// Validates a collection of resource files.
    /// </summary>
    /// <param name="resourceFiles">Resource files to validate.</param>
    /// <returns>Validation result with all detected issues.</returns>
    public ValidationResult Validate(List<ResourceFile> resourceFiles)
    {
        return Validate(resourceFiles, PlaceholderType.All);
    }

    /// <summary>
    /// Validates a collection of resource files with specific placeholder types.
    /// </summary>
    /// <param name="resourceFiles">Resource files to validate.</param>
    /// <param name="enabledPlaceholderTypes">Placeholder types to validate.</param>
    /// <returns>Validation result with all detected issues.</returns>
    public ValidationResult Validate(List<ResourceFile> resourceFiles, PlaceholderType enabledPlaceholderTypes)
    {
        var result = new ValidationResult();

        if (!resourceFiles.Any())
        {
            return result;
        }

        // Find the default language (should be first if properly sorted)
        var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null)
        {
            return result; // No default language found
        }

        var defaultKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Validate each non-default language
        foreach (var resourceFile in resourceFiles.Where(rf => !rf.Language.IsDefault))
        {
            var langCode = resourceFile.Language.Code;
            var langKeys = resourceFile.Entries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find missing keys (in default but not in translation)
            var missingKeys = defaultKeys.Except(langKeys).ToList();
            if (missingKeys.Any())
            {
                result.MissingKeys[langCode] = missingKeys;
            }

            // Find extra keys (in translation but not in default)
            var extraKeys = langKeys.Except(defaultKeys).ToList();
            if (extraKeys.Any())
            {
                result.ExtraKeys[langCode] = extraKeys;
            }

            // Find empty values
            var emptyKeys = resourceFile.Entries
                .Where(e => e.IsEmpty)
                .Select(e => e.Key)
                .ToList();
            if (emptyKeys.Any())
            {
                result.EmptyValues[langCode] = emptyKeys;
            }

            // Find duplicate keys (case-insensitive per ResX specification)
            var duplicateKeys = resourceFile.Entries
                .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateKeys.Any())
            {
                result.DuplicateKeys[langCode] = duplicateKeys;
            }

            // Validate placeholders
            var placeholderErrors = new Dictionary<string, string>();
            if (enabledPlaceholderTypes != PlaceholderType.None)
            {
                foreach (var entry in resourceFile.Entries)
                {
                    var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
                    if (defaultEntry != null && !string.IsNullOrEmpty(defaultEntry.Value) && !string.IsNullOrEmpty(entry.Value))
                    {
                        var validationResult = PlaceholderValidator.Validate(defaultEntry.Value, entry.Value, enabledPlaceholderTypes);
                        if (!validationResult.IsValid)
                        {
                            placeholderErrors[entry.Key] = validationResult.GetSummary();
                        }
                    }
                }
            }
            if (placeholderErrors.Any())
            {
                result.PlaceholderMismatches[langCode] = placeholderErrors;
            }
        }

        // Also check default language for duplicates and empty values (case-insensitive per ResX specification)
        var defaultDuplicates = defaultFile.Entries
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (defaultDuplicates.Any())
        {
            result.DuplicateKeys[defaultFile.Language.Code] = defaultDuplicates;
        }

        var defaultEmpty = defaultFile.Entries
            .Where(e => e.IsEmpty)
            .Select(e => e.Key)
            .ToList();
        if (defaultEmpty.Any())
        {
            result.EmptyValues[defaultFile.Language.Code] = defaultEmpty;
        }

        return result;
    }
}
