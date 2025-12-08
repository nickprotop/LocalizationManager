using LrmCloud.Shared.DTOs.Auth;

namespace LrmCloud.Api.Services;

public interface IGitHubAuthService
{
    /// <summary>
    /// Generates the GitHub OAuth authorization URL with state parameter.
    /// </summary>
    /// <returns>Authorization URL and state token for CSRF protection</returns>
    (string AuthorizationUrl, string State) GetAuthorizationUrl();

    /// <summary>
    /// Exchanges OAuth authorization code for access token and user profile.
    /// Creates new user or links to existing account.
    /// </summary>
    /// <param name="code">OAuth authorization code from GitHub callback</param>
    /// <param name="state">State parameter for CSRF validation</param>
    /// <param name="expectedState">Expected state value from session/cookie</param>
    /// <param name="ipAddress">Client IP address for audit trail</param>
    /// <returns>Success status, login response with tokens, or error message</returns>
    Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> HandleCallbackAsync(
        string code,
        string state,
        string expectedState,
        string? ipAddress = null);

    /// <summary>
    /// Unlinks GitHub account from user.
    /// Requires password verification if user has email/password auth.
    /// </summary>
    /// <param name="userId">User ID to unlink</param>
    /// <param name="password">Optional password for verification (required for email auth users)</param>
    /// <returns>Success status and error message if failed</returns>
    Task<(bool Success, string? ErrorMessage)> UnlinkGitHubAccountAsync(int userId, string? password = null);
}
