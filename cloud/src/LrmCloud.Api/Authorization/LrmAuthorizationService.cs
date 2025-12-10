using LrmCloud.Api.Data;
using LrmCloud.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Authorization;

/// <summary>
/// Implementation of authorization service for checking permissions.
/// </summary>
public class LrmAuthorizationService : ILrmAuthorizationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LrmAuthorizationService> _logger;

    public LrmAuthorizationService(AppDbContext db, ILogger<LrmAuthorizationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> HasOrganizationRoleAsync(int userId, int organizationId, params string[] allowedRoles)
    {
        var role = await GetOrganizationRoleAsync(userId, organizationId);
        return role != null && allowedRoles.Contains(role);
    }

    public async Task<bool> IsOrganizationOwnerAsync(int userId, int organizationId)
    {
        var org = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null);

        return org?.OwnerId == userId;
    }

    public async Task<bool> IsOrganizationAdminAsync(int userId, int organizationId)
    {
        var role = await GetOrganizationRoleAsync(userId, organizationId);
        return role != null && OrganizationRole.IsAdminOrOwner(role);
    }

    public async Task<bool> IsOrganizationMemberAsync(int userId, int organizationId)
    {
        return await _db.OrganizationMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
    }

    public async Task<bool> HasProjectAccessAsync(int userId, int projectId)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return false;

        // Personal project - check if user owns it
        if (project.OrganizationId == null)
            return project.UserId == userId;

        // Organization project - check membership
        return await IsOrganizationMemberAsync(userId, project.OrganizationId.Value);
    }

    public async Task<bool> CanEditProjectAsync(int userId, int projectId)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return false;

        // Personal project - check if user owns it
        if (project.OrganizationId == null)
            return project.UserId == userId;

        // Organization project - check if admin/owner or if project allows member editing
        var role = await GetOrganizationRoleAsync(userId, project.OrganizationId.Value);
        if (role == null)
            return false;

        // Admins and owners can always edit
        if (OrganizationRole.IsAdminOrOwner(role))
            return true;

        // Members can edit (viewers cannot)
        return role == OrganizationRole.Member;
    }

    public async Task<string?> GetOrganizationRoleAsync(int userId, int organizationId)
    {
        var member = await _db.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        return member?.Role;
    }
}
