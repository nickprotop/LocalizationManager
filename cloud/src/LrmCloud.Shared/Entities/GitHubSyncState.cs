using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Tracks the sync state between Cloud and GitHub for three-way merge.
/// Stores the "base" state (what was last synced) for each entry.
/// </summary>
[Table("github_sync_state")]
public class GitHubSyncState
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
    /// SHA256 hash of the value last synced with GitHub.
    /// Used as the "base" for three-way merge.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("github_hash")]
    public required string GitHubHash { get; set; }

    /// <summary>
    /// The actual value last synced with GitHub.
    /// Stored for conflict resolution display.
    /// </summary>
    [Column("github_value")]
    public string? GitHubValue { get; set; }

    /// <summary>
    /// Comment value last synced with GitHub.
    /// </summary>
    [Column("github_comment")]
    public string? GitHubComment { get; set; }

    /// <summary>
    /// The Git commit SHA when this entry was last synced.
    /// </summary>
    [MaxLength(40)]
    [Column("github_commit_sha")]
    public string? GitHubCommitSha { get; set; }

    /// <summary>
    /// When this entry was last synced with GitHub.
    /// </summary>
    [Column("synced_at")]
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version for optimistic locking.
    /// </summary>
    [Column("version")]
    public int Version { get; set; } = 1;
}
