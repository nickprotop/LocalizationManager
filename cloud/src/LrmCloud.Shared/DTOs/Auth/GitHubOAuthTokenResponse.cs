using System.Text.Json.Serialization;

namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Response from GitHub OAuth token endpoint.
/// See: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
/// </summary>
public class GitHubOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;
}
