using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to transfer organization ownership to another member.
/// </summary>
public class TransferOwnershipRequest
{
    /// <summary>
    /// The user ID of the new owner (must be an existing member).
    /// </summary>
    [Required(ErrorMessage = "New owner ID is required")]
    public int NewOwnerId { get; set; }
}
