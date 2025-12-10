using LrmCloud.Shared.DTOs.Usage;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for user usage statistics.
/// </summary>
public interface IUsageService
{
    /// <summary>
    /// Gets usage statistics for a user.
    /// </summary>
    Task<UsageStatsDto> GetUserStatsAsync(int userId);
}
