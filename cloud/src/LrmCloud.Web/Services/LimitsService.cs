using LrmCloud.Shared.DTOs.Usage;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for checking user plan limits.
/// Caches usage stats and provides convenience methods for limit checks.
/// </summary>
public class LimitsService
{
    private readonly UsageService _usageService;
    private UsageStatsDto? _cachedStats;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public LimitsService(UsageService usageService)
    {
        _usageService = usageService;
    }

    /// <summary>
    /// Get cached usage stats. Refreshes if cache is expired.
    /// </summary>
    public async Task<UsageStatsDto?> GetStatsAsync(bool forceRefresh = false)
    {
        if (forceRefresh || _cachedStats == null || DateTime.UtcNow > _cacheExpiry)
        {
            _cachedStats = await _usageService.GetStatsAsync();
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        }
        return _cachedStats;
    }

    /// <summary>
    /// Check if user can create a new project.
    /// </summary>
    public async Task<(bool CanCreate, string? Reason)> CanCreateProjectAsync()
    {
        var stats = await GetStatsAsync();
        if (stats == null) return (true, null); // Allow if we can't check

        if (stats.IsProjectLimitReached)
        {
            return (false, $"Project limit reached ({stats.ProjectCount}/{stats.MaxProjects}). Upgrade your plan for more projects.");
        }

        return (true, null);
    }

    /// <summary>
    /// Check if user can create a new API key.
    /// </summary>
    public async Task<(bool CanCreate, string? Reason)> CanCreateApiKeyAsync()
    {
        var stats = await GetStatsAsync();
        if (stats == null) return (true, null);

        if (stats.IsApiKeyLimitReached)
        {
            return (false, $"API key limit reached ({stats.ApiKeyCount}/{stats.MaxApiKeys}). Upgrade your plan for more API keys.");
        }

        return (true, null);
    }

    /// <summary>
    /// Check if user can create an organization.
    /// </summary>
    public async Task<(bool CanCreate, string? Reason)> CanCreateOrganizationAsync()
    {
        var stats = await GetStatsAsync();
        if (stats == null) return (true, null);

        if (!stats.CanCreateOrganization)
        {
            return (false, "Organizations are not available on the Free plan. Upgrade to Team or Enterprise.");
        }

        return (true, null);
    }

    /// <summary>
    /// Check if user can invite more members to an organization.
    /// </summary>
    public async Task<(bool CanInvite, string? Reason)> CanInviteMemberAsync(int currentMemberCount)
    {
        var stats = await GetStatsAsync();
        if (stats == null) return (true, null);

        if (stats.MaxTeamMembers != int.MaxValue && currentMemberCount >= stats.MaxTeamMembers)
        {
            return (false, $"Member limit reached ({currentMemberCount}/{stats.MaxTeamMembers}). Upgrade your plan for more team members.");
        }

        return (true, null);
    }

    /// <summary>
    /// Get translation usage warning if near or at limit.
    /// </summary>
    public async Task<(bool IsWarning, bool IsBlocked, string? Message)> GetTranslationUsageWarningAsync()
    {
        var stats = await GetStatsAsync();
        if (stats == null) return (false, false, null);

        if (stats.TranslationUsagePercent >= 100)
        {
            return (true, true, $"LRM translation limit reached. Used {stats.TranslationCharsUsed:N0}/{stats.TranslationCharsLimit:N0} characters.");
        }

        if (stats.TranslationUsagePercent >= 90)
        {
            return (true, false, $"LRM translation usage is at {stats.TranslationUsagePercent}%. Consider upgrading your plan.");
        }

        if (stats.TranslationUsagePercent >= 75)
        {
            return (true, false, $"LRM translation usage is at {stats.TranslationUsagePercent}%.");
        }

        return (false, false, null);
    }

    /// <summary>
    /// Invalidate the cache (call after creating/deleting resources).
    /// </summary>
    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
    }
}
