using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = null!;
}
