using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Refresh token for JWT authentication.
/// Allows users to obtain new access tokens without re-authenticating.
/// </summary>
[Table("refresh_tokens")]
public class RefreshToken
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// Token selector for O(1) lookup. First part of the token (before the dot).
    /// This is stored in plain text and used for fast database lookup via index.
    /// </summary>
    [MaxLength(32)]
    [Column("token_selector")]
    public string? TokenSelector { get; set; }

    /// <summary>
    /// Hashed token verifier (second part of the token, after the dot).
    /// This is hashed with BCrypt and verified after lookup by selector.
    /// For legacy tokens without selector, this contains the full token hash.
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("token_hash")]
    public required string TokenHash { get; set; }

    /// <summary>
    /// When this refresh token expires.
    /// </summary>
    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this token was created.
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this token was revoked (null if still valid).
    /// </summary>
    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// When this token was last used to refresh.
    /// </summary>
    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// If this token was replaced by a new one (token rotation), this is the hash of the new token.
    /// </summary>
    [MaxLength(255)]
    [Column("replaced_by_token_hash")]
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>
    /// IP address that created this token.
    /// </summary>
    [MaxLength(45)]
    [Column("created_by_ip")]
    public string? CreatedByIp { get; set; }

    /// <summary>
    /// IP address that revoked this token.
    /// </summary>
    [MaxLength(45)]
    [Column("revoked_by_ip")]
    public string? RevokedByIp { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    // Helper properties
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsRevoked => RevokedAt != null;

    [NotMapped]
    public bool IsActive => !IsRevoked && !IsExpired;
}
