using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Organization-level reviewers that can be inherited by projects.
/// </summary>
[Table("organization_reviewers")]
public class OrganizationReviewer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Role: "reviewer" or "approver".
    /// Approvers can also review, but reviewers cannot approve.
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("role")]
    public required string Role { get; set; }

    [Column("added_by_id")]
    public int? AddedById { get; set; }

    [ForeignKey(nameof(AddedById))]
    public User? AddedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
