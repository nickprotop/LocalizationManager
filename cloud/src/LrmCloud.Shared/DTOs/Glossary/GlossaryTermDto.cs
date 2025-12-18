namespace LrmCloud.Shared.DTOs.Glossary;

/// <summary>
/// DTO for a glossary term with all its translations.
/// </summary>
public class GlossaryTermDto
{
    public int Id { get; set; }

    /// <summary>
    /// Project ID if this is a project-level term (null for organization-level).
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// Organization ID if this is an organization-level term (null for project-level).
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// The source term (e.g., "Dashboard").
    /// </summary>
    public required string SourceTerm { get; set; }

    /// <summary>
    /// Source language code (e.g., "en").
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Optional description of the term's context.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether matching is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Translations of this term in different languages.
    /// Key = language code, Value = translated term.
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>
    /// Scope indicator: "project" or "organization".
    /// </summary>
    public string Scope => ProjectId.HasValue ? "project" : "organization";

    /// <summary>
    /// ID of user who created this term.
    /// </summary>
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Display name of user who created this term.
    /// </summary>
    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Simplified DTO for glossary entries (term + single translation).
/// Used in translation flow.
/// </summary>
public class GlossaryEntryDto
{
    /// <summary>
    /// The source term.
    /// </summary>
    public required string SourceTerm { get; set; }

    /// <summary>
    /// The translated term for the target language.
    /// </summary>
    public required string TranslatedTerm { get; set; }

    /// <summary>
    /// Whether matching is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; }
}
