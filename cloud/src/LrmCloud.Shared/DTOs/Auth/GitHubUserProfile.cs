using System.Text.Json.Serialization;

namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// GitHub user profile from GitHub API.
/// See: https://docs.github.com/en/rest/users/users#get-the-authenticated-user
/// </summary>
public class GitHubUserProfile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; } = null!;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }
}
