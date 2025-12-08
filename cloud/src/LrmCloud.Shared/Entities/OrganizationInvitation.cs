using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Pending invitation for a user to join an organization.
/// </summary>
[Table("organization_invitations")]
public class OrganizationInvitation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public required string Email { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = Constants.OrganizationRole.Member;

    [Required]
    [MaxLength(255)]
    [Column("token_hash")]
    public required string TokenHash { get; set; }

    [Required]
    [Column("invited_by")]
    public int InvitedBy { get; set; }

    [ForeignKey(nameof(InvitedBy))]
    public User? Inviter { get; set; }

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }
}
