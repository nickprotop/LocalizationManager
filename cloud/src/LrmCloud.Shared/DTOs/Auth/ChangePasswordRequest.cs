using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters")]
    public string NewPassword { get; set; } = null!;
}
