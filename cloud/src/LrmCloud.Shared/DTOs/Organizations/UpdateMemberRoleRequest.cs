using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to update a member's role in an organization.
/// </summary>
public class UpdateMemberRoleRequest
{
    [Required(ErrorMessage = "Role is required")]
    [MaxLength(50, ErrorMessage = "Role must not exceed 50 characters")]
    public required string Role { get; set; }
}
