namespace LrmCloud.Web.Models;

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

    /// <summary>
    /// Translations keyed by language code
    /// </summary>
    public Dictionary<string, TranslationCell> Translations { get; set; } = new();

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
    /// Sets or updates translation for a specific language
    /// </summary>
    public void SetTranslation(string languageCode, string? value, int? translationId = null)
    {
        if (Translations.TryGetValue(languageCode, out var cell))
        {
            cell.Value = value;
            cell.IsDirty = true;
        }
        else
        {
            Translations[languageCode] = new TranslationCell
            {
                LanguageCode = languageCode,
                Value = value,
                TranslationId = translationId,
                IsDirty = true
            };
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
