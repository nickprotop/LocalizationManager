namespace LrmCloud.Shared.DTOs.Auth;

public class LoginResponse
{
    public UserDto User { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }
}
