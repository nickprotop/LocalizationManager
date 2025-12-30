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

    /// <summary>
    /// Whether the email was verified by GitHub.
    /// This is populated from the /user/emails endpoint when the primary /user
    /// endpoint returns no email (user has email set to private).
    /// Not serialized from JSON - set programmatically.
    /// </summary>
    [JsonIgnore]
    public bool EmailVerified { get; set; }
}

/// <summary>
/// GitHub email entry from the /user/emails endpoint.
/// See: https://docs.github.com/en/rest/users/emails#list-email-addresses-for-the-authenticated-user
/// </summary>
public class GitHubEmail
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }
}
