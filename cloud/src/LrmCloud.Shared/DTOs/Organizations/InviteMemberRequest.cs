using System.ComponentModel.DataAnnotations;
using LrmCloud.Shared.Constants;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to invite a member to an organization.
/// </summary>
public class InviteMemberRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255, ErrorMessage = "Email must not exceed 255 characters")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Role is required")]
    [MaxLength(50, ErrorMessage = "Role must not exceed 50 characters")]
    public string Role { get; set; } = OrganizationRole.Member;
}
