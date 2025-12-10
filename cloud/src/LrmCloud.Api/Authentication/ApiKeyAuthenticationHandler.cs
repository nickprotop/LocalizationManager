using System.Security.Claims;
using System.Text.Encodings.Web;
using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LrmCloud.Api.Authentication;

/// <summary>
/// Authentication handler for CLI API keys.
/// Validates keys via X-API-Key header and creates a ClaimsPrincipal on success.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ICliApiKeyService _apiKeyService;
    private readonly AppDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICliApiKeyService apiKeyService,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if X-API-Key header is present
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeader))
        {
            // No API key header - let other schemes handle authentication
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("API key header is empty");
        }

        // Validate format
        if (!apiKey.StartsWith("lrm_"))
        {
            return AuthenticateResult.Fail("Invalid API key format");
        }

        // Validate key and get details
        var validationResult = await _apiKeyService.ValidateKeyWithScopesAsync(apiKey);
        if (validationResult == null)
        {
            Logger.LogWarning("Invalid API key attempted: {KeyPrefix}...", apiKey[..Math.Min(10, apiKey.Length)]);
            return AuthenticateResult.Fail("Invalid or expired API key");
        }

        // Load user to get email and name for claims
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == validationResult.UserId);

        if (user == null)
        {
            Logger.LogError("API key validated but user {UserId} not found", validationResult.UserId);
            return AuthenticateResult.Fail("User not found");
        }

        // Create claims matching the JWT structure plus API key specific claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.Username ?? user.Email ?? ""),
            new("auth_type", "api_key"),
            new("plan", user.Plan),
            new("email_verified", user.EmailVerified.ToString()),
            new("scopes", validationResult.Scopes),
            new("key_prefix", validationResult.KeyPrefix)
        };

        // Add project scope claim if key is project-scoped
        if (validationResult.ProjectId.HasValue)
        {
            claims.Add(new Claim("project_id", validationResult.ProjectId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.DefaultScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

        Logger.LogInformation("API key authentication successful for user {UserId} (key: {KeyPrefix})",
            user.Id, validationResult.KeyPrefix);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", "ApiKey");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
