// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// PO implementation of resource validator.
/// Validates PO files for missing translations, plural form mismatches,
/// printf format consistency, and other PO-specific rules.
/// </summary>
public class PoResourceValidator : IResourceValidator
{
    private readonly ResourceValidator _inner = new();
    private readonly PoResourceDiscovery _discovery;
    private readonly PoResourceReader _reader;

    public PoResourceValidator(PoFormatConfiguration? config = null)
    {
        _discovery = new PoResourceDiscovery(config);
        _reader = new PoResourceReader(config);
    }

    /// <inheritdoc />
    public ValidationResult Validate(string searchPath)
    {
        var languages = _discovery.DiscoverLanguages(searchPath);
        var files = languages.Select(l => _reader.Read(l)).ToList();
        var result = _inner.Validate(files);

        // Add PO-specific validation
        foreach (var file in files)
        {
            ValidatePoSpecific(file, result);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(Validate(searchPath));

    /// <inheritdoc />
    public Task<ValidationResult> ValidateFileAsync(ResourceFile file, CancellationToken ct = default)
    {
        var result = _inner.Validate(new List<ResourceFile> { file });
        ValidatePoSpecific(file, result);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Performs PO-specific validation on a file.
    /// </summary>
    private void ValidatePoSpecific(ResourceFile file, ValidationResult result)
    {
        var languageCode = file.Language.Code;
        var expectedPluralCount = PoPluralMapper.GetPluralCount(languageCode);

        foreach (var entry in file.Entries)
        {
            // Validate plural form count - report as empty values for missing forms
            if (entry.IsPlural && entry.PluralForms != null)
            {
                var expectedCategories = PoPluralMapper.GetCategoriesForLanguage(languageCode);
                foreach (var category in expectedCategories)
                {
                    if (!entry.PluralForms.TryGetValue(category, out var value) || string.IsNullOrEmpty(value))
                    {
                        // Report missing plural forms as empty values
                        if (!result.EmptyValues.ContainsKey(languageCode))
                            result.EmptyValues[languageCode] = new List<string>();

                        var pluralKey = $"{entry.Key}[{category}]";
                        if (!result.EmptyValues[languageCode].Contains(pluralKey))
                            result.EmptyValues[languageCode].Add(pluralKey);
                    }
                }
            }

            // Validate printf-style format specifiers match between source and translation
            if (!string.IsNullOrEmpty(entry.Value))
            {
                // Use the key as the source text for PO format (msgid is typically used as key)
                var keyPlaceholders = ExtractPrintfPlaceholders(entry.Key);
                var valuePlaceholders = ExtractPrintfPlaceholders(entry.Value);

                if (keyPlaceholders.Count > 0 && !PlaceholdersMatch(keyPlaceholders, valuePlaceholders))
                {
                    if (!result.PlaceholderMismatches.ContainsKey(languageCode))
                        result.PlaceholderMismatches[languageCode] = new Dictionary<string, string>();

                    result.PlaceholderMismatches[languageCode][entry.Key] =
                        $"Printf format mismatch: source has [{string.Join(", ", keyPlaceholders)}], " +
                        $"translation has [{string.Join(", ", valuePlaceholders)}]";
                }
            }
        }
    }

    /// <summary>
    /// Extracts printf-style placeholders from a string.
    /// Supports: %s, %d, %i, %f, %u, %x, %o, %c, %%, %ld, %lu, etc.
    /// </summary>
    private static List<string> ExtractPrintfPlaceholders(string text)
    {
        var placeholders = new List<string>();
        if (string.IsNullOrEmpty(text))
            return placeholders;

        // Match printf placeholders: %[flags][width][.precision][length]specifier
        var regex = new System.Text.RegularExpressions.Regex(
            @"%(?:[-+0 #])?(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|h|l|ll|L|z|j|t)?[diouxXeEfFgGaAcspn%]");

        foreach (System.Text.RegularExpressions.Match match in regex.Matches(text))
        {
            if (match.Value != "%%") // Skip escaped percent signs
                placeholders.Add(match.Value);
        }

        return placeholders;
    }

    /// <summary>
    /// Checks if two placeholder lists match (same count and types).
    /// Order may differ for positional arguments.
    /// </summary>
    private static bool PlaceholdersMatch(List<string> source, List<string> target)
    {
        if (source.Count != target.Count)
            return false;

        // Sort and compare - placeholders can be reordered in translations
        var sortedSource = source.OrderBy(p => p).ToList();
        var sortedTarget = target.OrderBy(p => p).ToList();

        for (int i = 0; i < sortedSource.Count; i++)
        {
            // Extract just the specifier type for comparison (ignore width/precision/flags)
            var sourceType = GetPlaceholderType(sortedSource[i]);
            var targetType = GetPlaceholderType(sortedTarget[i]);

            if (sourceType != targetType)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts the type specifier from a printf placeholder.
    /// </summary>
    private static char GetPlaceholderType(string placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
            return '\0';

        return placeholder[placeholder.Length - 1];
    }
}
