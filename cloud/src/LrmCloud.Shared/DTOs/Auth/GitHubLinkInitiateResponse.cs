namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Response from initiating GitHub account linking.
/// Contains a short-lived code to use for the redirect.
/// </summary>
public record GitHubLinkInitiateResponse(string Code);
