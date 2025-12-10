using Microsoft.AspNetCore.Authentication;

namespace LrmCloud.Api.Authentication;

/// <summary>
/// Options for API key authentication scheme.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The authentication scheme name.
    /// </summary>
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// The header name to read the API key from.
    /// </summary>
    public const string HeaderName = "X-API-Key";
}
