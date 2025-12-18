namespace LrmCloud.Shared.DTOs.TranslationMemory;

/// <summary>
/// Translation Memory match result.
/// </summary>
public class TmMatchDto
{
    /// <summary>
    /// The TM entry ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Original source text.
    /// </summary>
    public required string SourceText { get; set; }

    /// <summary>
    /// Translated text.
    /// </summary>
    public required string TranslatedText { get; set; }

    /// <summary>
    /// Source language code.
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Target language code.
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// Match percentage (100 = exact match, &lt;100 = fuzzy).
    /// </summary>
    public int MatchPercent { get; set; }

    /// <summary>
    /// Number of times this TM entry has been used.
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// Optional context information.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// When this TM entry was last used.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to find TM matches for a source text.
/// </summary>
public class TmLookupRequest
{
    /// <summary>
    /// Source text to find matches for.
    /// </summary>
    public required string SourceText { get; set; }

    /// <summary>
    /// Source language code.
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Target language code.
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// Minimum match percentage for fuzzy matches (default: 70).
    /// </summary>
    public int MinMatchPercent { get; set; } = 70;

    /// <summary>
    /// Maximum number of results to return (default: 5).
    /// </summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>
    /// Optional organization ID for shared TM lookup.
    /// </summary>
    public int? OrganizationId { get; set; }
}

/// <summary>
/// Response containing TM matches.
/// </summary>
public class TmLookupResponse
{
    /// <summary>
    /// List of matches, sorted by match percentage descending.
    /// </summary>
    public List<TmMatchDto> Matches { get; set; } = new();

    /// <summary>
    /// Whether an exact match (100%) was found.
    /// </summary>
    public bool HasExactMatch => Matches.Any(m => m.MatchPercent == 100);
}

/// <summary>
/// Request to store a translation in TM.
/// </summary>
public class TmStoreRequest
{
    /// <summary>
    /// Source text.
    /// </summary>
    public required string SourceText { get; set; }

    /// <summary>
    /// Translated text.
    /// </summary>
    public required string TranslatedText { get; set; }

    /// <summary>
    /// Source language code.
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Target language code.
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// Optional context (e.g., project name, key path).
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Optional organization ID for shared TM.
    /// </summary>
    public int? OrganizationId { get; set; }
}

/// <summary>
/// TM statistics for a user.
/// </summary>
public class TmStatsDto
{
    /// <summary>
    /// Total number of TM entries.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Total translations served from TM.
    /// </summary>
    public int TotalUseCount { get; set; }

    /// <summary>
    /// Breakdown by language pair.
    /// </summary>
    public List<TmLanguagePairStats> LanguagePairs { get; set; } = new();
}

/// <summary>
/// Stats for a specific language pair in TM.
/// </summary>
public class TmLanguagePairStats
{
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public int EntryCount { get; set; }
    public int UseCount { get; set; }
}
