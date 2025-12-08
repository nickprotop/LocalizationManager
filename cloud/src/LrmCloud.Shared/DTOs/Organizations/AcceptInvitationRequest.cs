using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to accept an organization invitation.
/// </summary>
public class AcceptInvitationRequest
{
    [Required(ErrorMessage = "Invitation token is required")]
    public required string Token { get; set; }
}
