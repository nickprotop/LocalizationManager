namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Organization member data transfer object.
/// </summary>
public class OrganizationMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public required string Role { get; set; }
    public DateTime? JoinedAt { get; set; }
    public string? InvitedByUsername { get; set; }  // Username of inviter
}
