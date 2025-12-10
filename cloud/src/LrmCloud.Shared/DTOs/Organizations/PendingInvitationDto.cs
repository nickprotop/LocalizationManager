namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// DTO for a pending organization invitation.
/// </summary>
public class PendingInvitationDto
{
    /// <summary>
    /// The invitation token for accepting/declining.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// The organization ID the user is being invited to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The name of the organization.
    /// </summary>
    public required string OrganizationName { get; set; }

    /// <summary>
    /// The organization slug (URL-friendly identifier).
    /// </summary>
    public required string OrganizationSlug { get; set; }

    /// <summary>
    /// The role the user will have if they accept.
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Email of the person who sent the invitation.
    /// </summary>
    public string? InvitedByEmail { get; set; }

    /// <summary>
    /// Name of the person who sent the invitation.
    /// </summary>
    public string? InvitedByName { get; set; }

    /// <summary>
    /// When the invitation expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the invitation was sent.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
