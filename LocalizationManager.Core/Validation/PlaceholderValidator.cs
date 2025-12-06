// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Validation;

/// <summary>
/// Validates that placeholders in translations match the source text.
/// </summary>
public static class PlaceholderValidator
{
    /// <summary>
    /// Validates that translation placeholders match source placeholders.
    /// </summary>
    /// <param name="sourceText">The source text.</param>
    /// <param name="translationText">The translation text.</param>
    /// <returns>Validation result with any errors found.</returns>
    public static PlaceholderValidationResult Validate(string? sourceText, string? translationText)
    {
        return Validate(sourceText, translationText, PlaceholderType.All);
    }

    /// <summary>
    /// Validates that translation placeholders match source placeholders for specific placeholder types.
    /// </summary>
    /// <param name="sourceText">The source text.</param>
    /// <param name="translationText">The translation text.</param>
    /// <param name="enabledTypes">The placeholder types to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public static PlaceholderValidationResult Validate(string? sourceText, string? translationText, PlaceholderType enabledTypes)
    {
        var result = new PlaceholderValidationResult
        {
            IsValid = true,
            Errors = new List<string>()
        };

        // Skip validation if either is empty or if no types are enabled
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translationText) || enabledTypes == PlaceholderType.None)
        {
            return result;
        }

        var sourcePlaceholders = PlaceholderDetector.DetectPlaceholders(sourceText, enabledTypes);
        var translationPlaceholders = PlaceholderDetector.DetectPlaceholders(translationText, enabledTypes);

        // Get normalized identifiers for comparison
        var sourceIdentifiers = sourcePlaceholders
            .Select(PlaceholderDetector.GetNormalizedIdentifier)
            .OrderBy(x => x)
            .ToList();

        var translationIdentifiers = translationPlaceholders
            .Select(PlaceholderDetector.GetNormalizedIdentifier)
            .OrderBy(x => x)
            .ToList();

        // Check for missing placeholders in translation
        var missing = sourceIdentifiers.Except(translationIdentifiers).ToList();
        if (missing.Any())
        {
            result.IsValid = false;
            foreach (var id in missing)
            {
                var placeholder = sourcePlaceholders.First(p =>
                    PlaceholderDetector.GetNormalizedIdentifier(p) == id);
                result.Errors.Add($"Missing placeholder: {placeholder.Original}");
                result.MissingPlaceholders.Add(placeholder);
            }
        }

        // Check for extra placeholders in translation
        var extra = translationIdentifiers.Except(sourceIdentifiers).ToList();
        if (extra.Any())
        {
            result.IsValid = false;
            foreach (var id in extra)
            {
                var placeholder = translationPlaceholders.First(p =>
                    PlaceholderDetector.GetNormalizedIdentifier(p) == id);
                result.Errors.Add($"Extra placeholder not in source: {placeholder.Original}");
                result.ExtraPlaceholders.Add(placeholder);
            }
        }

        // Check placeholder count matches (catches duplicates)
        if (sourcePlaceholders.Count != translationPlaceholders.Count && result.IsValid)
        {
            result.IsValid = false;
            result.Errors.Add($"Placeholder count mismatch: source has {sourcePlaceholders.Count}, translation has {translationPlaceholders.Count}");
        }

        // Validate placeholder types match
        foreach (var sourcePh in sourcePlaceholders)
        {
            var sourceId = PlaceholderDetector.GetNormalizedIdentifier(sourcePh);
            var matchingTranslation = translationPlaceholders
                .FirstOrDefault(t => PlaceholderDetector.GetNormalizedIdentifier(t) == sourceId);

            if (matchingTranslation != null && sourcePh.Type != matchingTranslation.Type)
            {
                result.IsValid = false;
                result.Errors.Add($"Placeholder type mismatch for '{sourceId}': source is {sourcePh.Type}, translation is {matchingTranslation.Type}");
                result.TypeMismatches.Add((sourcePh, matchingTranslation));
            }
        }

        return result;
    }

    /// <summary>
    /// Validates placeholders for a batch of translation pairs.
    /// </summary>
    /// <param name="translations">Dictionary of key -> (source, translation) pairs.</param>
    /// <returns>Dictionary of validation results by key.</returns>
    public static Dictionary<string, PlaceholderValidationResult> ValidateBatch(
        Dictionary<string, (string source, string translation)> translations)
    {
        var results = new Dictionary<string, PlaceholderValidationResult>();

        foreach (var kvp in translations)
        {
            results[kvp.Key] = Validate(kvp.Value.source, kvp.Value.translation);
        }

        return results;
    }
}

/// <summary>
/// Result of placeholder validation.
/// </summary>
public class PlaceholderValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Placeholders that are in source but missing from translation.
    /// </summary>
    public List<Placeholder> MissingPlaceholders { get; set; } = new();

    /// <summary>
    /// Placeholders that are in translation but not in source.
    /// </summary>
    public List<Placeholder> ExtraPlaceholders { get; set; } = new();

    /// <summary>
    /// Placeholders that exist in both but have different types.
    /// </summary>
    public List<(Placeholder Source, Placeholder Translation)> TypeMismatches { get; set; } = new();

    /// <summary>
    /// Gets a summary of the validation result.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
        {
            return "All placeholders valid";
        }

        return string.Join("; ", Errors);
    }
}
