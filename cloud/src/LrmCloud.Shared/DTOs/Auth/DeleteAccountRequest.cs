using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Request to delete a user account (requires password confirmation).
/// </summary>
public class DeleteAccountRequest
{
    /// <summary>
    /// Current password for confirmation.
    /// </summary>
    [Required(ErrorMessage = "Password is required to delete your account")]
    public string Password { get; set; } = null!;
}
