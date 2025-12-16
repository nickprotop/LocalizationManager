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
}
