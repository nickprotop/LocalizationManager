namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Response from a translation request.
/// </summary>
public class TranslateResponseDto
{
    /// <summary>
    /// Whether the translation was successful overall.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of keys successfully translated.
    /// </summary>
    public int TranslatedCount { get; set; }

    /// <summary>
    /// Number of keys that failed to translate.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Number of keys skipped (already had translations).
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Total characters translated (for usage tracking).
    /// </summary>
    public long CharactersTranslated { get; set; }

    /// <summary>
    /// Provider used for translation.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Individual translation results.
    /// </summary>
    public List<TranslationResultDto> Results { get; set; } = new();

    /// <summary>
    /// Errors that occurred during translation.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Time taken for translation in milliseconds.
    /// </summary>
    public long ElapsedMs { get; set; }
}

/// <summary>
/// Result of translating a single key.
/// </summary>
public class TranslationResultDto
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Target language code.
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Plural form (e.g., "one", "other"). Empty for non-plural keys.
    /// </summary>
    public string PluralForm { get; set; } = string.Empty;

    /// <summary>
    /// Original source text.
    /// </summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// Translated text.
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this specific translation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if translation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether the translation was from cache.
    /// </summary>
    public bool FromCache { get; set; }
}

/// <summary>
/// Response from a single text translation.
/// </summary>
public class TranslateSingleResponseDto
{
    public bool Success { get; set; }
    public string TranslatedText { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool FromCache { get; set; }
}
