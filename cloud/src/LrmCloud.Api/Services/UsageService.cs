using LrmCloud.Api.Data;
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

    public UsageService(AppDbContext db, ILogger<UsageService> logger)
    {
        _db = db;
        _logger = logger;
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
            MemberSince = user.CreatedAt
        };
    }
}
