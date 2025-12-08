namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Extended user profile with subscription and usage information.
/// Used for authenticated user profile endpoints.
/// </summary>
public class UserProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool EmailVerified { get; set; }
    public string AuthType { get; set; } = "email";

    // GitHub OAuth info
    public long? GitHubId { get; set; }

    // Subscription
    public string Plan { get; set; } = "free";
    public string? StripeCustomerId { get; set; }

    // Usage & Limits
    public int TranslationCharsUsed { get; set; }
    public int TranslationCharsLimit { get; set; }
    public DateTime? TranslationCharsResetAt { get; set; }

    // Timestamps
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
