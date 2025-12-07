using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// API key for CLI authentication.
/// Key is hashed; only prefix is stored for identification.
/// </summary>
[Table("api_keys")]
public class ApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// First 10 characters of the key for identification (e.g., "lrm_abc123").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("key_prefix")]
    public required string KeyPrefix { get; set; }

    /// <summary>
    /// Hashed full key for verification.
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("key_hash")]
    public required string KeyHash { get; set; }

    [MaxLength(255)]
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Comma-separated scopes: "read", "write", "admin".
    /// </summary>
    [MaxLength(255)]
    [Column("scopes")]
    public string Scopes { get; set; } = "read,write";

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
