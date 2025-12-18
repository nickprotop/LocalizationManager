namespace LrmCloud.Shared.DTOs.Glossary;

/// <summary>
/// Response containing glossary terms list.
/// </summary>
public class GlossaryListResponse
{
    /// <summary>
    /// All glossary terms (including inherited org terms if applicable).
    /// </summary>
    public List<GlossaryTermDto> Terms { get; set; } = new();

    /// <summary>
    /// Languages used in the glossary.
    /// </summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Total count of terms.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Count of project-level terms (only for project endpoint).
    /// </summary>
    public int ProjectTermsCount { get; set; }

    /// <summary>
    /// Count of inherited organization terms (only for project endpoint).
    /// </summary>
    public int InheritedTermsCount { get; set; }
}

/// <summary>
/// Result of glossary validation on translated text.
/// </summary>
public class GlossaryValidationResult
{
    /// <summary>
    /// Whether all glossary terms were applied correctly.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of glossary violations found.
    /// </summary>
    public List<GlossaryViolation> Violations { get; set; } = new();
}

/// <summary>
/// A single glossary violation.
/// </summary>
public class GlossaryViolation
{
    /// <summary>
    /// The expected term from glossary.
    /// </summary>
    public required string ExpectedTerm { get; set; }

    /// <summary>
    /// What was found in the translation instead.
    /// </summary>
    public string? FoundTerm { get; set; }

    /// <summary>
    /// Type of violation: "missing", "incorrect", "case_mismatch".
    /// </summary>
    public required string ViolationType { get; set; }

    /// <summary>
    /// Position in the translated text where violation was found.
    /// </summary>
    public int? Position { get; set; }
}

/// <summary>
/// Summary of glossary usage in a translation.
/// </summary>
public class GlossaryUsageSummary
{
    /// <summary>
    /// Whether glossary was applied to the translation.
    /// </summary>
    public bool GlossaryApplied { get; set; }

    /// <summary>
    /// Number of glossary terms that matched the source text.
    /// </summary>
    public int TermsMatched { get; set; }

    /// <summary>
    /// The matched terms and their expected translations.
    /// </summary>
    public List<GlossaryEntryDto> MatchedEntries { get; set; } = new();

    /// <summary>
    /// Message for the user (no backend details exposed).
    /// </summary>
    public string? Message { get; set; }
}
