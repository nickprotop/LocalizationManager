namespace LrmCloud.Shared.DTOs.Auth;

public class LoginResponse
{
    public UserDto User { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Optional auth event for UI notifications.
    /// Values: "autolinked" (GitHub linked to existing account),
    ///         "new_account_email_exists" (new account created, existing account has same email)
    /// </summary>
    public string? AuthEvent { get; set; }

    /// <summary>
    /// Email address related to the auth event (for UI display).
    /// </summary>
    public string? RelatedEmail { get; set; }
}
