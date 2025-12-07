using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// User account entity.
/// Supports both email/password and OAuth (GitHub) authentication.
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("auth_type")]
    public required string AuthType { get; set; } // "email", "github"

    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [Column("email_verified")]
    public bool EmailVerified { get; set; }

    [MaxLength(255)]
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    // GitHub OAuth
    [Column("github_id")]
    public long? GitHubId { get; set; }

    [Column("github_access_token_encrypted")]
    public string? GitHubAccessTokenEncrypted { get; set; }

    [Column("github_token_expires_at")]
    public DateTime? GitHubTokenExpiresAt { get; set; }

    // Profile
    [Required]
    [MaxLength(255)]
    [Column("username")]
    public required string Username { get; set; }

    [MaxLength(255)]
    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    // Subscription
    [MaxLength(50)]
    [Column("plan")]
    public string Plan { get; set; } = "free";

    [MaxLength(255)]
    [Column("stripe_customer_id")]
    public string? StripeCustomerId { get; set; }

    [Column("translation_chars_used")]
    public int TranslationCharsUsed { get; set; }

    [Column("translation_chars_limit")]
    public int TranslationCharsLimit { get; set; } = 10000;

    [Column("translation_chars_reset_at")]
    public DateTime? TranslationCharsResetAt { get; set; }

    // Security
    [MaxLength(255)]
    [Column("password_reset_token_hash")]
    public string? PasswordResetTokenHash { get; set; }

    [Column("password_reset_expires")]
    public DateTime? PasswordResetExpires { get; set; }

    [MaxLength(255)]
    [Column("email_verification_token_hash")]
    public string? EmailVerificationTokenHash { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; }

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    // Soft delete
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Audit
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<OrganizationMember> OrganizationMemberships { get; set; } = new List<OrganizationMember>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<UserApiKey> UserApiKeys { get; set; } = new List<UserApiKey>();
}
