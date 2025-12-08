namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Request to unlink GitHub account from user.
/// Password is optional but required if user has email/password auth set up.
/// </summary>
public class UnlinkGitHubRequest
{
    /// <summary>
    /// Optional password for verification (required for email/password users)
    /// </summary>
    public string? Password { get; set; }
}
