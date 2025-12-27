using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Organization (team) entity for multi-user collaboration.
/// </summary>
[Table("organizations")]
public class Organization
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public required string Name { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("slug")]
    public required string Slug { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    [MaxLength(50)]
    [Column("plan")]
    public string Plan { get; set; } = "team";

    /// <summary>
    /// Payment provider customer ID.
    /// </summary>
    [MaxLength(255)]
    [Column("payment_customer_id")]
    public string? PaymentCustomerId { get; set; }

    /// <summary>
    /// Active payment provider: "stripe", "paypal", etc.
    /// </summary>
    [MaxLength(50)]
    [Column("payment_provider")]
    public string? PaymentProvider { get; set; }

    // GitHub integration (organization-level token)
    /// <summary>
    /// Encrypted GitHub access token for organization-wide repository access.
    /// Used as fallback when project doesn't have its own token.
    /// </summary>
    [Column("github_access_token_encrypted")]
    public string? GitHubAccessTokenEncrypted { get; set; }

    /// <summary>
    /// User ID who connected the organization's GitHub account.
    /// </summary>
    [Column("github_connected_by_user_id")]
    public int? GitHubConnectedByUserId { get; set; }

    [ForeignKey(nameof(GitHubConnectedByUserId))]
    public User? GitHubConnectedByUser { get; set; }

    [Column("translation_chars_used")]
    public int TranslationCharsUsed { get; set; }

    [Column("translation_chars_limit")]
    public int TranslationCharsLimit { get; set; } = 500000;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<OrganizationApiKey> OrganizationApiKeys { get; set; } = new List<OrganizationApiKey>();
    public ICollection<OrganizationReviewer> Reviewers { get; set; } = new List<OrganizationReviewer>();
}
