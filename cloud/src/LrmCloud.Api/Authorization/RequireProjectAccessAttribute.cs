using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace LrmCloud.Api.Authorization;

/// <summary>
/// Attribute to require access to a project for accessing an endpoint.
/// The project ID must be available as a route parameter named "id" or "projectId".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireProjectAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly bool _requireEditAccess;

    /// <summary>
    /// Creates a new RequireProjectAccessAttribute.
    /// </summary>
    /// <param name="requireEditAccess">If true, requires edit access (owner/admin/member). If false, read access is sufficient.</param>
    public RequireProjectAccessAttribute(bool requireEditAccess = false)
    {
        _requireEditAccess = requireEditAccess;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Try to get project ID from route
        var projectIdString = context.RouteData.Values["id"]?.ToString()
            ?? context.RouteData.Values["projectId"]?.ToString();

        if (string.IsNullOrEmpty(projectIdString) || !int.TryParse(projectIdString, out var projectId))
        {
            context.Result = new BadRequestObjectResult("Project ID is required");
            return;
        }

        // Check if this is an API key with project scope restriction
        var authType = user.FindFirst("auth_type")?.Value;
        if (authType == "api_key")
        {
            var keyProjectIdClaim = user.FindFirst("project_id")?.Value;
            if (!string.IsNullOrEmpty(keyProjectIdClaim) && int.TryParse(keyProjectIdClaim, out var keyProjectId))
            {
                // API key is scoped to a specific project - must match
                if (keyProjectId != projectId)
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<RequireProjectAccessAttribute>>();
                    logger?.LogWarning(
                        "API key project scope denied. Key scoped to project {KeyProjectId}, requested {RequestedProjectId}",
                        keyProjectId, projectId);

                    context.Result = new ObjectResult(new
                    {
                        Error = "API key is not authorized for this project",
                        Code = "AUTH_PROJECT_DENIED"
                    })
                    {
                        StatusCode = 403
                    };
                    return;
                }
            }
        }

        // Get the authorization service
        var authService = context.HttpContext.RequestServices.GetRequiredService<ILrmAuthorizationService>();

        bool hasAccess;
        if (_requireEditAccess)
        {
            hasAccess = await authService.CanEditProjectAsync(userId, projectId);
        }
        else
        {
            hasAccess = await authService.HasProjectAccessAsync(userId, projectId);
        }

        if (!hasAccess)
        {
            context.Result = new ForbidResult();
        }
    }
}

/// <summary>
/// Shorthand attribute requiring edit access to a project.
/// </summary>
public class RequireProjectEditAttribute : RequireProjectAccessAttribute
{
    public RequireProjectEditAttribute() : base(requireEditAccess: true)
    {
    }
}
