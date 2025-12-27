using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Stores pending conflicts from GitHub pull operations.
/// These conflicts need user resolution before changes can be applied.
/// </summary>
[Table("pending_conflicts")]
public class PendingConflict
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Resource key name.
    /// </summary>
    [Required]
    [MaxLength(500)]
    [Column("key_name")]
    public required string KeyName { get; set; }

    /// <summary>
    /// Language code (e.g., "en", "fr", "de").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("language_code")]
    public required string LanguageCode { get; set; }

    /// <summary>
    /// Plural form identifier (empty for non-plural, "one", "other", etc. for plurals).
    /// </summary>
    [MaxLength(20)]
    [Column("plural_form")]
    public string PluralForm { get; set; } = "";

    /// <summary>
    /// Type of conflict: "BothModified", "DeletedInGitHub", "DeletedInCloud".
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("conflict_type")]
    public required string ConflictType { get; set; }

    /// <summary>
    /// Value from GitHub.
    /// </summary>
    [Column("github_value")]
    public string? GitHubValue { get; set; }

    /// <summary>
    /// Current value in Cloud.
    /// </summary>
    [Column("cloud_value")]
    public string? CloudValue { get; set; }

    /// <summary>
    /// Last synced value (base for three-way merge).
    /// </summary>
    [Column("base_value")]
    public string? BaseValue { get; set; }

    /// <summary>
    /// When Cloud value was last modified.
    /// </summary>
    [Column("cloud_modified_at")]
    public DateTime? CloudModifiedAt { get; set; }

    /// <summary>
    /// Username of who modified the Cloud value.
    /// </summary>
    [MaxLength(200)]
    [Column("cloud_modified_by")]
    public string? CloudModifiedBy { get; set; }

    /// <summary>
    /// When this conflict was detected.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Git commit SHA from which the conflict originated.
    /// </summary>
    [MaxLength(40)]
    [Column("commit_sha")]
    public string? CommitSha { get; set; }
}
