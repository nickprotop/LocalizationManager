using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Request to change user's email address.
/// Requires current password for security.
/// </summary>
public class ChangeEmailRequest
{
    /// <summary>
    /// New email address
    /// </summary>
    [Required(ErrorMessage = "New email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255)]
    public string NewEmail { get; set; } = null!;

    /// <summary>
    /// Current password for verification
    /// </summary>
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = null!;
}
