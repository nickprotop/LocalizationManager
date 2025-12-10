using LrmCloud.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace LrmCloud.Api.Authorization;

/// <summary>
/// Attribute to require a specific organization role for accessing an endpoint.
/// The organization ID must be available as a route parameter named "id" or "organizationId".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireOrgRoleAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    /// <summary>
    /// Creates a new RequireOrgRoleAttribute with the specified allowed roles.
    /// </summary>
    /// <param name="roles">The roles that are allowed to access the endpoint.</param>
    public RequireOrgRoleAttribute(params string[] roles)
    {
        _allowedRoles = roles.Length > 0 ? roles : new[] { OrganizationRole.Member, OrganizationRole.Admin, OrganizationRole.Owner };
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

        // Try to get organization ID from route
        var orgIdString = context.RouteData.Values["id"]?.ToString()
            ?? context.RouteData.Values["organizationId"]?.ToString();

        if (string.IsNullOrEmpty(orgIdString) || !int.TryParse(orgIdString, out var organizationId))
        {
            context.Result = new BadRequestObjectResult("Organization ID is required");
            return;
        }

        // Get the authorization service
        var authService = context.HttpContext.RequestServices.GetRequiredService<ILrmAuthorizationService>();

        var hasAccess = await authService.HasOrganizationRoleAsync(userId, organizationId, _allowedRoles);
        if (!hasAccess)
        {
            context.Result = new ForbidResult();
        }
    }
}

/// <summary>
/// Shorthand attribute requiring admin or owner role.
/// </summary>
public class RequireOrgAdminAttribute : RequireOrgRoleAttribute
{
    public RequireOrgAdminAttribute() : base(OrganizationRole.Admin, OrganizationRole.Owner)
    {
    }
}

/// <summary>
/// Shorthand attribute requiring owner role only.
/// </summary>
public class RequireOrgOwnerAttribute : RequireOrgRoleAttribute
{
    public RequireOrgOwnerAttribute() : base(OrganizationRole.Owner)
    {
    }
}
