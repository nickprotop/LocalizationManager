using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Represents a point-in-time snapshot of project resources.
/// Similar to a git commit, each snapshot has a unique ID and preserves the state of all files.
/// </summary>
[Table("snapshots")]
public class Snapshot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Short unique identifier (8 characters, like git short SHA).
    /// Example: "a1b2c3d4"
    /// </summary>
    [Required]
    [MaxLength(8)]
    [Column("snapshot_id")]
    public required string SnapshotId { get; set; }

    /// <summary>
    /// User who created this snapshot (null for system-generated snapshots).
    /// </summary>
    [Column("created_by_user_id")]
    public int? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Description or commit message for this snapshot.
    /// Examples: "Push from CLI", "Updated French translations", "Manual backup before release"
    /// </summary>
    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Storage path in MinIO where snapshot files are stored.
    /// Example: "snapshots/a1b2c3d4/"
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("storage_path")]
    public required string StoragePath { get; set; }

    /// <summary>
    /// Number of resource files in this snapshot.
    /// </summary>
    [Column("file_count")]
    public int FileCount { get; set; }

    /// <summary>
    /// Number of translation keys in this snapshot.
    /// </summary>
    [Column("key_count")]
    public int KeyCount { get; set; }

    /// <summary>
    /// Total number of translations across all languages.
    /// </summary>
    [Column("translation_count")]
    public int TranslationCount { get; set; }

    /// <summary>
    /// Type of snapshot: "push", "manual", "restore", "auto".
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("snapshot_type")]
    public required string SnapshotType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
