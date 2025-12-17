using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Request to translate resource keys.
/// </summary>
public class TranslateRequestDto
{
    /// <summary>
    /// Keys to translate. If empty, translates all keys.
    /// </summary>
    public List<string> Keys { get; set; } = new();

    /// <summary>
    /// Optional source texts for each key. When provided, these values are used instead of
    /// the saved database values. This allows translating unsaved edits from the UI.
    /// Key = key name (for non-plural) or "keyName:pluralForm" (for plural), Value = source text to translate.
    /// </summary>
    public Dictionary<string, string>? SourceTexts { get; set; }

    /// <summary>
    /// Target language codes to translate to.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one target language is required")]
    public List<string> TargetLanguages { get; set; } = new();

    /// <summary>
    /// Source language code. Defaults to project's default language.
    /// </summary>
    public string? SourceLanguage { get; set; }

    /// <summary>
    /// Translation provider to use. If null, uses best available.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Only translate keys that are missing translations.
    /// </summary>
    public bool OnlyMissing { get; set; } = true;

    /// <summary>
    /// Overwrite existing translations even if they exist.
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Optional context to improve translation quality (for AI providers).
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Whether to preserve placeholders like {0}, {{name}}, etc.
    /// </summary>
    public bool PreservePlaceholders { get; set; } = true;

    /// <summary>
    /// Whether to save translations directly to the database.
    /// When false, translations are returned but not persisted (preview mode for UI).
    /// Defaults to true for CLI compatibility.
    /// </summary>
    public bool SaveToDatabase { get; set; } = true;

    /// <summary>
    /// Optional metadata about keys being translated. When provided, overrides
    /// database values for key properties like IsPlural. This allows translating
    /// keys with unsaved UI changes.
    /// Key = resource key name, Value = metadata.
    /// </summary>
    public Dictionary<string, KeyTranslationMetadata>? KeyMetadata { get; set; }
}

/// <summary>
/// Metadata about a key being translated.
/// </summary>
public class KeyTranslationMetadata
{
    /// <summary>
    /// Whether this key is plural. When true, translates all plural forms.
    /// Overrides the database value when provided.
    /// </summary>
    public bool IsPlural { get; set; }
}

/// <summary>
/// Request to translate a single text (for preview/testing).
/// </summary>
public class TranslateSingleRequestDto
{
    [Required]
    public string Text { get; set; } = string.Empty;

    [Required]
    public string SourceLanguage { get; set; } = string.Empty;

    [Required]
    public string TargetLanguage { get; set; } = string.Empty;

    public string? Provider { get; set; }

    public string? Context { get; set; }
}
