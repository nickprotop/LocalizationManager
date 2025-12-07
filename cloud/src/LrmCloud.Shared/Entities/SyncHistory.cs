using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Sync history for tracking project synchronization events.
/// </summary>
[Table("sync_history")]
public class SyncHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Type of sync: "push", "pull", "webhook", "scheduled".
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("sync_type")]
    public required string SyncType { get; set; }

    /// <summary>
    /// Direction: "to_cloud", "from_cloud", "bidirectional".
    /// </summary>
    [MaxLength(20)]
    [Column("direction")]
    public string? Direction { get; set; }

    [MaxLength(40)]
    [Column("commit_sha")]
    public string? CommitSha { get; set; }

    [Column("pr_number")]
    public int? PrNumber { get; set; }

    [Column("pr_url")]
    public string? PrUrl { get; set; }

    [Column("keys_added")]
    public int KeysAdded { get; set; }

    [Column("keys_updated")]
    public int KeysUpdated { get; set; }

    [Column("keys_deleted")]
    public int KeysDeleted { get; set; }

    /// <summary>
    /// Status: "pending", "in_progress", "completed", "failed".
    /// </summary>
    [MaxLength(50)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sync conflict for tracking unresolved conflicts between local and remote.
/// </summary>
[Table("sync_conflicts")]
public class SyncConflict
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Column("resource_key_id")]
    public int? ResourceKeyId { get; set; }

    [ForeignKey(nameof(ResourceKeyId))]
    public ResourceKey? ResourceKey { get; set; }

    [MaxLength(10)]
    [Column("language_code")]
    public string? LanguageCode { get; set; }

    [Column("local_value")]
    public string? LocalValue { get; set; }

    [Column("remote_value")]
    public string? RemoteValue { get; set; }

    [Column("local_updated_at")]
    public DateTime? LocalUpdatedAt { get; set; }

    [Column("remote_updated_at")]
    public DateTime? RemoteUpdatedAt { get; set; }

    /// <summary>
    /// Resolution: "local_wins", "remote_wins", "manual".
    /// </summary>
    [MaxLength(50)]
    [Column("resolution")]
    public string? Resolution { get; set; }

    [Column("resolved_by")]
    public int? ResolvedById { get; set; }

    [ForeignKey(nameof(ResolvedById))]
    public User? ResolvedBy { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
