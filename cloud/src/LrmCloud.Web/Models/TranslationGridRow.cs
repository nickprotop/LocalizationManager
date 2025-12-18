namespace LrmCloud.Web.Models;

/// <summary>
/// Standard CLDR plural categories.
/// </summary>
public static class PluralForms
{
    public const string Zero = "zero";
    public const string One = "one";
    public const string Two = "two";
    public const string Few = "few";
    public const string Many = "many";
    public const string Other = "other";

    /// <summary>
    /// Gets all plural form categories in display order.
    /// </summary>
    public static readonly string[] All = [One, Two, Few, Many, Other, Zero];

    /// <summary>
    /// Gets the most common plural forms (used by most languages).
    /// </summary>
    public static readonly string[] Common = [One, Other];
}

/// <summary>
/// View model for a row in the translation grid.
/// Combines the resource key with its translations for all languages.
/// </summary>
public class TranslationGridRow
{
    public int KeyId { get; set; }
    public required string KeyName { get; set; }
    public string? KeyPath { get; set; }
    public bool IsPlural { get; set; }
    public string? Comment { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Tracking original values for dirty detection
    public string? OriginalComment { get; set; }
    public bool OriginalIsPlural { get; set; }

    /// <summary>
    /// Whether the key metadata (Comment, IsPlural) has changed.
    /// </summary>
    public bool IsKeyMetadataDirty => Comment != OriginalComment || IsPlural != OriginalIsPlural;

    /// <summary>
    /// Translations keyed by "{languageCode}" for non-plural keys,
    /// or "{languageCode}:{pluralForm}" for plural keys.
    /// </summary>
    public Dictionary<string, TranslationCell> Translations { get; set; } = new();

    /// <summary>
    /// Gets the dictionary key for a translation cell.
    /// </summary>
    public static string GetKey(string languageCode, string? pluralForm = null)
        => string.IsNullOrEmpty(pluralForm) ? languageCode : $"{languageCode}:{pluralForm}";

    /// <summary>
    /// Gets translation cells for a specific language (all plural forms if plural key).
    /// </summary>
    public IEnumerable<TranslationCell> GetTranslationsForLanguage(string languageCode)
    {
        if (!IsPlural)
        {
            if (Translations.TryGetValue(languageCode, out var cell))
                yield return cell;
            yield break;
        }

        foreach (var pluralForm in Models.PluralForms.All)
        {
            var key = GetKey(languageCode, pluralForm);
            if (Translations.TryGetValue(key, out var cell))
                yield return cell;
        }
    }

    /// <summary>
    /// Gets a specific translation cell.
    /// </summary>
    public TranslationCell? GetCell(string languageCode, string? pluralForm = null)
    {
        var key = GetKey(languageCode, pluralForm);
        return Translations.GetValueOrDefault(key);
    }

    /// <summary>
    /// Gets the translation status for this key across all languages
    /// </summary>
    public TranslationStatus Status
    {
        get
        {
            if (Translations.Count == 0)
                return TranslationStatus.Missing;

            var hasEmpty = Translations.Values.Any(t => string.IsNullOrEmpty(t.Value));
            var hasValue = Translations.Values.Any(t => !string.IsNullOrEmpty(t.Value));

            if (!hasValue)
                return TranslationStatus.Missing;
            if (hasEmpty)
                return TranslationStatus.Partial;
            return TranslationStatus.Complete;
        }
    }

    /// <summary>
    /// Gets translation value for a specific language
    /// </summary>
    public string? GetTranslation(string languageCode)
    {
        return Translations.TryGetValue(languageCode, out var cell) ? cell.Value : null;
    }

    /// <summary>
    /// Sets or updates translation for a specific language.
    /// </summary>
    public void SetTranslation(string languageCode, string? value, int? translationId = null, string? pluralForm = null)
    {
        var key = GetKey(languageCode, pluralForm);
        if (Translations.TryGetValue(key, out var cell))
        {
            cell.Value = value;
            cell.IsDirty = true;
        }
        else
        {
            Translations[key] = new TranslationCell
            {
                LanguageCode = languageCode,
                Value = value,
                TranslationId = translationId,
                PluralForm = pluralForm ?? "",
                IsDirty = true
            };
        }
    }

    /// <summary>
    /// Creates a deep clone of this row for isolated editing.
    /// </summary>
    public TranslationGridRow Clone()
    {
        var clone = new TranslationGridRow
        {
            KeyId = KeyId,
            KeyName = KeyName,
            KeyPath = KeyPath,
            IsPlural = IsPlural,
            Comment = Comment,
            Version = Version,
            UpdatedAt = UpdatedAt,
            OriginalComment = OriginalComment,
            OriginalIsPlural = OriginalIsPlural,
            Translations = new Dictionary<string, TranslationCell>()
        };

        foreach (var kvp in Translations)
        {
            clone.Translations[kvp.Key] = new TranslationCell
            {
                TranslationId = kvp.Value.TranslationId,
                LanguageCode = kvp.Value.LanguageCode,
                Value = kvp.Value.Value,
                OriginalValue = kvp.Value.OriginalValue,
                PluralForm = kvp.Value.PluralForm,
                Status = kvp.Value.Status,
                Version = kvp.Value.Version,
                IsDirty = kvp.Value.IsDirty
            };
        }

        return clone;
    }

    /// <summary>
    /// Copies values from another row into this row (for applying edits from a clone).
    /// </summary>
    public void ApplyFrom(TranslationGridRow source)
    {
        Comment = source.Comment;
        IsPlural = source.IsPlural;
        // Note: OriginalComment and OriginalIsPlural are preserved from this row,
        // not copied from source, so dirty detection works correctly

        // Copy translations
        foreach (var kvp in source.Translations)
        {
            if (Translations.TryGetValue(kvp.Key, out var existingCell))
            {
                existingCell.Value = kvp.Value.Value;
                existingCell.IsDirty = kvp.Value.IsDirty;
            }
            else
            {
                Translations[kvp.Key] = new TranslationCell
                {
                    TranslationId = kvp.Value.TranslationId,
                    LanguageCode = kvp.Value.LanguageCode,
                    Value = kvp.Value.Value,
                    OriginalValue = kvp.Value.OriginalValue,
                    PluralForm = kvp.Value.PluralForm,
                    Status = kvp.Value.Status,
                    Version = kvp.Value.Version,
                    IsDirty = kvp.Value.IsDirty
                };
            }
        }

        // Remove translations that were removed in source
        var keysToRemove = Translations.Keys.Where(k => !source.Translations.ContainsKey(k)).ToList();
        foreach (var key in keysToRemove)
        {
            Translations.Remove(key);
        }
    }
}

/// <summary>
/// Represents a single translation cell in the grid
/// </summary>
public class TranslationCell
{
    public int? TranslationId { get; set; }
    public required string LanguageCode { get; set; }
    public string? Value { get; set; }
    public string? OriginalValue { get; set; }
    public string PluralForm { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int Version { get; set; }

    /// <summary>
    /// Whether this cell has unsaved changes
    /// </summary>
    public bool IsDirty { get; set; }
}

/// <summary>
/// Translation status for a resource key
/// </summary>
public enum TranslationStatus
{
    Complete,
    Partial,
    Missing
}
