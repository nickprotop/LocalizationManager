namespace LrmCloud.Shared.DTOs.Reviews;

/// <summary>
/// Reviewer/Approver information.
/// </summary>
public class ReviewerDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Role: "reviewer" or "approver".
    /// </summary>
    public string Role { get; set; } = "reviewer";

    /// <summary>
    /// Languages this reviewer can review (null = all).
    /// </summary>
    public string[]? LanguageCodes { get; set; }

    /// <summary>
    /// Whether this reviewer is inherited from organization.
    /// </summary>
    public bool IsInherited { get; set; }

    public DateTime CreatedAt { get; set; }
}
