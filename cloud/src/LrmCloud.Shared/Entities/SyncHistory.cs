using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Records every push operation for history viewing and revert capability.
/// Stores the changes made (diffs) so operations can be undone.
/// </summary>
[Table("sync_history")]
public class SyncHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Short unique identifier for user-friendly references (e.g., "abc12345").
    /// Used in CLI: lrm cloud log, lrm cloud revert abc12345
    /// </summary>
    [Required]
    [MaxLength(8)]
    [Column("history_id")]
    public required string HistoryId { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// User who performed this operation.
    /// </summary>
    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Type of operation: "push", "revert".
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("operation_type")]
    public required string OperationType { get; set; }

    /// <summary>
    /// Source of the sync operation: "cli", "web-edit", "github".
    /// </summary>
    [MaxLength(20)]
    [Column("source")]
    public string Source { get; set; } = "cli";

    /// <summary>
    /// User-provided message describing the changes (from --message flag).
    /// </summary>
    [MaxLength(500)]
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Number of entries added in this operation.
    /// </summary>
    [Column("entries_added")]
    public int EntriesAdded { get; set; }

    /// <summary>
    /// Number of entries modified in this operation.
    /// </summary>
    [Column("entries_modified")]
    public int EntriesModified { get; set; }

    /// <summary>
    /// Number of entries deleted in this operation.
    /// </summary>
    [Column("entries_deleted")]
    public int EntriesDeleted { get; set; }

    /// <summary>
    /// JSON containing the changes made in this operation.
    /// Stores BEFORE state for modified/deleted entries to enable revert.
    /// Format: { "changes": [{ "key", "lang", "changeType", "beforeValue", "beforeHash", "afterValue", "afterHash" }] }
    /// </summary>
    [Column("changes_json", TypeName = "jsonb")]
    public string? ChangesJson { get; set; }

    /// <summary>
    /// If this was a revert operation, references the history entry that was reverted.
    /// </summary>
    [Column("reverted_from_id")]
    public int? RevertedFromId { get; set; }

    [ForeignKey(nameof(RevertedFromId))]
    public SyncHistory? RevertedFrom { get; set; }

    /// <summary>
    /// Status: "completed", "reverted" (if this entry was later undone).
    /// </summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "completed";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a single change within a sync operation (for ChangesJson).
/// </summary>
public class SyncChangeEntry
{
    public required string Key { get; set; }
    public required string Lang { get; set; }

    /// <summary>
    /// Type of change: "added", "modified", "deleted"
    /// </summary>
    public required string ChangeType { get; set; }

    /// <summary>
    /// Value before the change (null for additions).
    /// </summary>
    public string? BeforeValue { get; set; }

    /// <summary>
    /// Hash before the change (null for additions).
    /// </summary>
    public string? BeforeHash { get; set; }

    /// <summary>
    /// Value after the change (null for deletions).
    /// </summary>
    public string? AfterValue { get; set; }

    /// <summary>
    /// Hash after the change (null for deletions).
    /// </summary>
    public string? AfterHash { get; set; }

    /// <summary>
    /// Comment before the change (for display in diff).
    /// </summary>
    public string? BeforeComment { get; set; }

    /// <summary>
    /// Comment after the change.
    /// </summary>
    public string? AfterComment { get; set; }
}

/// <summary>
/// Container for changes JSON serialization.
/// </summary>
public class SyncChangesData
{
    public List<SyncChangeEntry> Changes { get; set; } = new();
}
