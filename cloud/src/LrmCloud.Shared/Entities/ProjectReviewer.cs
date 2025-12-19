using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Junction table for project reviewers and approvers.
/// </summary>
[Table("project_reviewers")]
public class ProjectReviewer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

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

    /// <summary>
    /// Optional: Restrict reviewer to specific languages (comma-separated).
    /// Null means all languages.
    /// </summary>
    [MaxLength(200)]
    [Column("language_codes")]
    public string? LanguageCodes { get; set; }

    [Column("added_by_id")]
    public int? AddedById { get; set; }

    [ForeignKey(nameof(AddedById))]
    public User? AddedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Reviewer role constants.
/// </summary>
public static class ReviewerRole
{
    /// <summary>
    /// Can mark translations as "reviewed".
    /// </summary>
    public const string Reviewer = "reviewer";

    /// <summary>
    /// Can mark translations as "approved" (also has reviewer privileges).
    /// </summary>
    public const string Approver = "approver";

    public static readonly string[] All = { Reviewer, Approver };

    public static bool IsValid(string role) => All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool CanReview(string role) => role == Reviewer || role == Approver;

    public static bool CanApprove(string role) => role == Approver;
}
