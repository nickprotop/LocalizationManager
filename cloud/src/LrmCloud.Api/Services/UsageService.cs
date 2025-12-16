using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Usage;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Implementation of usage statistics service.
/// </summary>
public class UsageService : IUsageService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UsageService> _logger;
    private readonly LimitsConfiguration _limits;

    public UsageService(AppDbContext db, ILogger<UsageService> logger, CloudConfiguration config)
    {
        _db = db;
        _logger = logger;
        _limits = config.Limits;
    }

    public async Task<UsageStatsDto> GetUserStatsAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return new UsageStatsDto();
        }

        // Get project count (personal projects + org projects user has access to)
        var projectCount = await _db.Projects
            .Where(p => p.UserId == userId ||
                        (p.OrganizationId.HasValue &&
                         p.Organization!.Members.Any(m => m.UserId == userId)))
            .CountAsync();

        // Get resource key count from user's accessible projects
        var totalKeyCount = await _db.ResourceKeys
            .Where(rk => rk.Project!.UserId == userId ||
                        (rk.Project!.OrganizationId.HasValue &&
                         rk.Project!.Organization!.Members.Any(m => m.UserId == userId)))
            .CountAsync();

        // Count unique languages used across translations
        var resourceFileCount = await _db.Translations
            .Where(t => t.ResourceKey!.Project!.UserId == userId ||
                       (t.ResourceKey!.Project!.OrganizationId.HasValue &&
                        t.ResourceKey!.Project!.Organization!.Members.Any(m => m.UserId == userId)))
            .Select(t => new { t.ResourceKey!.ProjectId, t.LanguageCode })
            .Distinct()
            .CountAsync();

        // Get API key counts
        var apiKeyCount = await _db.ApiKeys
            .Where(k => k.UserId == userId)
            .CountAsync();

        var activeApiKeyCount = await _db.ApiKeys
            .Where(k => k.UserId == userId &&
                       (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
            .CountAsync();

        return new UsageStatsDto
        {
            // LRM Translation usage (counts against plan)
            TranslationCharsUsed = user.TranslationCharsUsed,
            TranslationCharsLimit = user.TranslationCharsLimit,
            TranslationCharsResetAt = user.TranslationCharsResetAt,
            // Other providers usage (BYOK + free community)
            OtherCharsUsed = user.OtherCharsUsed,
            OtherCharsLimit = user.OtherCharsLimit,
            OtherCharsResetAt = user.OtherCharsResetAt,
            // Other stats
            ProjectCount = projectCount,
            ResourceFileCount = resourceFileCount, // Using this for unique language/project combos
            TotalKeyCount = totalKeyCount,
            ApiKeyCount = apiKeyCount,
            ActiveApiKeyCount = activeApiKeyCount,
            Plan = user.Plan,
            MemberSince = user.CreatedAt,
            // Plan limits
            MaxProjects = _limits.GetMaxProjects(user.Plan),
            MaxApiKeys = _limits.GetMaxApiKeys(user.Plan),
            MaxTeamMembers = _limits.GetMaxTeamMembers(user.Plan)
        };
    }

    public async Task<OrganizationUsageDto?> GetOrganizationStatsAsync(int organizationId, int userId)
    {
        // Verify user is a member of the organization
        var membership = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        if (membership == null)
        {
            return null;
        }

        var org = await _db.Organizations
            .Include(o => o.Members)
            .Include(o => o.Projects)
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        if (org == null)
        {
            return null;
        }

        // Storage tracking not implemented yet - return 0
        var storageBytes = 0L;

        // Calculate days remaining in billing cycle (reset on 1st of month)
        var now = DateTime.UtcNow;
        var nextMonth = now.AddMonths(1);
        var resetDate = new DateTime(nextMonth.Year, nextMonth.Month, 1);
        var daysRemaining = (int)(resetDate - now).TotalDays;

        return new OrganizationUsageDto
        {
            LrmCharsUsed = org.TranslationCharsUsed,
            LrmCharsLimit = org.TranslationCharsLimit,
            OtherCharsUsed = 0, // TODO: Track organization-level BYOK usage
            ApiCalls = 0, // TODO: Track organization-level API calls
            StorageBytes = storageBytes,
            DaysRemaining = daysRemaining,
            Plan = org.Plan,
            MemberCount = org.Members.Count,
            MaxMembers = _limits.GetMaxTeamMembers(org.Plan),
            ProjectCount = org.Projects.Count
        };
    }
}
