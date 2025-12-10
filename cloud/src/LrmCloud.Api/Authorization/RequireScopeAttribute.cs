using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LrmCloud.Api.Authorization;

/// <summary>
/// Authorization attribute that enforces scope requirements for API key authentication.
/// For JWT authentication, all scopes are implicitly allowed (full user access).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireScopeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredScopes;

    /// <summary>
    /// Creates a new scope requirement. Access is granted if the key has ANY of the specified scopes.
    /// </summary>
    /// <param name="scopes">One or more scopes. Access granted if key has at least one of these.</param>
    public RequireScopeAttribute(params string[] scopes)
    {
        _requiredScopes = scopes;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Must be authenticated
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check auth type - JWT users have full access
        var authType = user.FindFirst("auth_type")?.Value;
        if (authType != "api_key")
        {
            // JWT authentication - no scope restrictions
            return;
        }

        // API key authentication - check scopes
        var scopesClaim = user.FindFirst("scopes")?.Value ?? "";
        var userScopes = scopesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Check if user has at least one of the required scopes
        var hasRequiredScope = _requiredScopes.Any(required =>
            userScopes.Contains(required, StringComparer.OrdinalIgnoreCase));

        if (!hasRequiredScope)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<RequireScopeAttribute>>();
            logger?.LogWarning(
                "API key scope denied. Required: [{Required}], Has: [{Has}]",
                string.Join(", ", _requiredScopes),
                scopesClaim);

            context.Result = new ObjectResult(new
            {
                Error = $"API key missing required scope. Requires one of: {string.Join(", ", _requiredScopes)}",
                Code = "AUTH_SCOPE_DENIED"
            })
            {
                StatusCode = 403
            };
        }
    }
}

/// <summary>
/// Requires read scope for API key access.
/// </summary>
public class RequireReadScopeAttribute : RequireScopeAttribute
{
    public RequireReadScopeAttribute() : base("read", "write", "admin") { }
}

/// <summary>
/// Requires write scope for API key access.
/// </summary>
public class RequireWriteScopeAttribute : RequireScopeAttribute
{
    public RequireWriteScopeAttribute() : base("write", "admin") { }
}

/// <summary>
/// Requires admin scope for API key access.
/// </summary>
public class RequireAdminScopeAttribute : RequireScopeAttribute
{
    public RequireAdminScopeAttribute() : base("admin") { }
}
