using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Tracks synchronization of glossaries to external translation providers (e.g., DeepL).
/// Stores external glossary IDs and sync status for each language pair.
/// </summary>
[Table("glossary_provider_sync")]
public class GlossaryProviderSync
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Project this sync record belongs to (if project-level glossary).
    /// Either ProjectId OR OrganizationId must be set, but not both.
    /// </summary>
    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Organization this sync record belongs to (if organization-level glossary).
    /// </summary>
    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    /// <summary>
    /// Name of the translation provider (e.g., "deepl").
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider_name")]
    public required string ProviderName { get; set; }

    /// <summary>
    /// Source language code for this glossary (e.g., "en").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("source_language")]
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Target language code for this glossary (e.g., "fr").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("target_language")]
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// External glossary ID from the provider (e.g., DeepL glossary_id).
    /// </summary>
    [MaxLength(255)]
    [Column("external_glossary_id")]
    public string? ExternalGlossaryId { get; set; }

    /// <summary>
    /// When this glossary was last successfully synced to the provider.
    /// </summary>
    [Column("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Current sync status: pending, synced, error.
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("sync_status")]
    public string SyncStatus { get; set; } = "pending";

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    [Column("sync_error")]
    public string? SyncError { get; set; }

    /// <summary>
    /// Number of entries in the glossary at last sync.
    /// </summary>
    [Column("entry_count")]
    public int EntryCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
