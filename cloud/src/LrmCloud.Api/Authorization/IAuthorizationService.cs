using LrmCloud.Shared.Constants;

namespace LrmCloud.Api.Authorization;

/// <summary>
/// Service for checking organization and project permissions.
/// </summary>
public interface ILrmAuthorizationService
{
    /// <summary>
    /// Checks if a user has the required role in an organization.
    /// </summary>
    Task<bool> HasOrganizationRoleAsync(int userId, int organizationId, params string[] allowedRoles);

    /// <summary>
    /// Checks if a user is the owner of an organization.
    /// </summary>
    Task<bool> IsOrganizationOwnerAsync(int userId, int organizationId);

    /// <summary>
    /// Checks if a user is an admin or owner of an organization.
    /// </summary>
    Task<bool> IsOrganizationAdminAsync(int userId, int organizationId);

    /// <summary>
    /// Checks if a user is a member of an organization (any role).
    /// </summary>
    Task<bool> IsOrganizationMemberAsync(int userId, int organizationId);

    /// <summary>
    /// Checks if a user has access to a project (personal or through organization).
    /// </summary>
    Task<bool> HasProjectAccessAsync(int userId, int projectId);

    /// <summary>
    /// Checks if a user can edit a project (owner or org admin).
    /// </summary>
    Task<bool> CanEditProjectAsync(int userId, int projectId);

    /// <summary>
    /// Gets the user's role in an organization (null if not a member).
    /// </summary>
    Task<string?> GetOrganizationRoleAsync(int userId, int organizationId);
}
