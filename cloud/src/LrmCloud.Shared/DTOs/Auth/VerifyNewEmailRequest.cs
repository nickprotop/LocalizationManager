using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Request to verify new email address with token from verification email.
/// </summary>
public class VerifyNewEmailRequest
{
    /// <summary>
    /// Verification token sent to new email
    /// </summary>
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = null!;
}
