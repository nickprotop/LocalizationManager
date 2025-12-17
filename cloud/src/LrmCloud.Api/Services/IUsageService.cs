using LrmCloud.Shared.DTOs.Usage;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for user and organization usage statistics.
/// </summary>
public interface IUsageService
{
    /// <summary>
    /// Gets usage statistics for a user.
    /// </summary>
    Task<UsageStatsDto> GetUserStatsAsync(int userId);

    /// <summary>
    /// Gets usage statistics for an organization.
    /// </summary>
    Task<OrganizationUsageDto?> GetOrganizationStatsAsync(int organizationId, int userId);

    /// <summary>
    /// Gets user's usage broken down by personal vs organization contributions.
    /// </summary>
    Task<UserUsageBreakdownDto> GetUserUsageBreakdownAsync(int userId);

    /// <summary>
    /// Gets organization usage breakdown by member (for admins/owners only).
    /// </summary>
    Task<List<OrgMemberUsageDto>> GetOrgMemberUsageAsync(int organizationId, int userId);

    /// <summary>
    /// Gets project usage breakdown by contributor.
    /// </summary>
    Task<ProjectUsageDto?> GetProjectUsageAsync(int projectId, int userId);
}
