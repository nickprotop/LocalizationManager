using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// User-level translation provider API key.
/// Keys are encrypted at rest.
/// </summary>
[Table("user_api_keys")]
public class UserApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Translation provider name: "google", "deepl", "openai", "azure", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// AES-256 encrypted API key.
    /// </summary>
    [Required]
    [Column("encrypted_key")]
    public required string EncryptedKey { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Organization-level translation provider API key.
/// Shared across all organization projects.
/// </summary>
[Table("organization_api_keys")]
public class OrganizationApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    [Required]
    [Column("encrypted_key")]
    public required string EncryptedKey { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Project-level translation provider API key.
/// Highest priority in the hierarchy.
/// </summary>
[Table("project_api_keys")]
public class ProjectApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    [Required]
    [Column("encrypted_key")]
    public required string EncryptedKey { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
