using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Localization resource key entity.
/// Represents a single translatable key within a project.
/// </summary>
[Table("resource_keys")]
public class ResourceKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("key_name")]
    public required string KeyName { get; set; }

    [MaxLength(500)]
    [Column("key_path")]
    public string? KeyPath { get; set; }

    [Column("is_plural")]
    public bool IsPlural { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// SHA256 hash of the key metadata (keyName + comment + isPlural).
    /// Used for key-level change detection.
    /// </summary>
    [MaxLength(64)]
    [Column("hash")]
    public string? Hash { get; set; }

    /// <summary>
    /// Version for optimistic locking.
    /// Incremented on each update.
    /// </summary>
    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Translation> Translations { get; set; } = new List<Translation>();
}
