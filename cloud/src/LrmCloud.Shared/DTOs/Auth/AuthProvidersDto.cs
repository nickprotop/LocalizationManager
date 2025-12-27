namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Indicates which authentication providers are configured on this server.
/// </summary>
public record AuthProvidersDto
{
    /// <summary>
    /// Whether GitHub OAuth is configured.
    /// </summary>
    public bool GitHub { get; init; }
}
