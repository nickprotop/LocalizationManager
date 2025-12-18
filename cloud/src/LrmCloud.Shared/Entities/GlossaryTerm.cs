using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// A glossary term that should be translated consistently.
/// Can belong to either a project (project-specific) or an organization (shared across all org projects).
/// Example: "Dashboard" should always be "Tableau de bord" in French.
/// </summary>
[Table("glossary_terms")]
public class GlossaryTerm
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Project this glossary term belongs to (if project-level).
    /// Either ProjectId OR OrganizationId must be set, but not both.
    /// </summary>
    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Organization this glossary term belongs to (if organization-level).
    /// Organization terms are shared across all projects in the organization.
    /// </summary>
    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    /// <summary>
    /// The source term to match (e.g., "Dashboard").
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("source_term")]
    public required string SourceTerm { get; set; }

    /// <summary>
    /// Source language code (e.g., "en").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("source_language")]
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Optional description explaining the term's usage context.
    /// </summary>
    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Whether matching should be case-sensitive.
    /// </summary>
    [Column("case_sensitive")]
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// User who created this term.
    /// </summary>
    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Translations for this term in different languages.
    /// </summary>
    public ICollection<GlossaryTranslation> Translations { get; set; } = new List<GlossaryTranslation>();
}
