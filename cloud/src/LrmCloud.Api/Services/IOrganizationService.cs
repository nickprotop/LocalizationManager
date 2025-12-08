using LrmCloud.Shared.DTOs.Organizations;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing organizations and their members.
/// </summary>
public interface IOrganizationService
{
    // ============================================================
    // Organization CRUD
    // ============================================================

    /// <summary>
    /// Creates a new organization owned by the specified user.
    /// </summary>
    Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> CreateOrganizationAsync(int userId, CreateOrganizationRequest request);

    /// <summary>
    /// Gets a specific organization if the user has access to it.
    /// </summary>
    Task<OrganizationDto?> GetOrganizationAsync(int organizationId, int userId);

    /// <summary>
    /// Gets all organizations the user is a member of.
    /// </summary>
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(int userId);

    /// <summary>
    /// Updates an organization's details (owner only).
    /// </summary>
    Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> UpdateOrganizationAsync(int organizationId, int userId, UpdateOrganizationRequest request);

    /// <summary>
    /// Soft deletes an organization (owner only).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeleteOrganizationAsync(int organizationId, int userId);

    // ============================================================
    // Member Management
    // ============================================================

    /// <summary>
    /// Gets all members of an organization.
    /// </summary>
    Task<List<OrganizationMemberDto>> GetMembersAsync(int organizationId, int userId);

    /// <summary>
    /// Invites a user to join an organization by email (admin+ required).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> InviteMemberAsync(int organizationId, int userId, InviteMemberRequest request);

    /// <summary>
    /// Accepts an invitation to join an organization.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> AcceptInvitationAsync(int userId, string token);

    /// <summary>
    /// Removes a member from an organization (admin+ required, cannot remove owner).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> RemoveMemberAsync(int organizationId, int userId, int memberUserId);

    /// <summary>
    /// Updates a member's role in an organization (owner only).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> UpdateMemberRoleAsync(int organizationId, int userId, int memberUserId, string newRole);

    // ============================================================
    // Authorization Helpers
    // ============================================================

    /// <summary>
    /// Checks if a user is the owner of an organization.
    /// </summary>
    Task<bool> IsOwnerAsync(int organizationId, int userId);

    /// <summary>
    /// Checks if a user is an admin or owner of an organization.
    /// </summary>
    Task<bool> IsAdminOrOwnerAsync(int organizationId, int userId);

    /// <summary>
    /// Checks if a user is a member (any role) of an organization.
    /// </summary>
    Task<bool> IsMemberAsync(int organizationId, int userId);

    /// <summary>
    /// Gets the user's role in an organization, or null if not a member.
    /// </summary>
    Task<string?> GetUserRoleAsync(int organizationId, int userId);
}
