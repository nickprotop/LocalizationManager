using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
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
            TranslationCharsLimit = _limits.GetTranslationCharsLimit(user.Plan),
            TranslationCharsResetAt = user.TranslationCharsResetAt,
            // Other providers usage (BYOK + free community)
            OtherCharsUsed = user.OtherCharsUsed,
            OtherCharsLimit = _limits.GetOtherCharsLimit(user.Plan),
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
            .Include(o => o.Owner) // Include owner for quota info
            .Include(o => o.Members)
            .Include(o => o.Projects)
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        if (org?.Owner == null)
        {
            return null;
        }

        var owner = org.Owner;

        // Storage tracking not implemented yet - return 0
        var storageBytes = 0L;

        // Calculate days remaining in billing cycle (reset on 1st of month)
        var now = DateTime.UtcNow;
        var nextMonth = now.AddMonths(1);
        var resetDate = new DateTime(nextMonth.Year, nextMonth.Month, 1);
        var daysRemaining = (int)(resetDate - now).TotalDays;

        // Organization shares the owner's quota - no separate org limits
        return new OrganizationUsageDto
        {
            // Show OWNER's usage (org shares owner's quota)
            LrmCharsUsed = owner.TranslationCharsUsed,
            LrmCharsLimit = _limits.GetTranslationCharsLimit(owner.Plan),
            OtherCharsUsed = owner.OtherCharsUsed,
            OtherCharsLimit = _limits.GetOtherCharsLimit(owner.Plan),
            ApiCalls = 0, // TODO: Track organization-level API calls
            StorageBytes = storageBytes,
            DaysRemaining = daysRemaining,
            Plan = owner.Plan, // Show owner's plan
            MemberCount = org.Members.Count,
            MaxMembers = _limits.GetMaxTeamMembers(owner.Plan),
            ProjectCount = org.Projects.Count
        };
    }

    public async Task<UserUsageBreakdownDto> GetUserUsageBreakdownAsync(int userId)
    {
        var events = await _db.UsageEvents
            .Where(e => e.ActingUserId == userId)
            .ToListAsync();

        var personalEvents = events.Where(e => e.OrganizationId == null).ToList();
        var orgGroups = events
            .Where(e => e.OrganizationId != null)
            .GroupBy(e => e.OrganizationId!.Value)
            .ToList();

        var orgContributions = new List<OrgUsageContributionDto>();
        foreach (var orgGroup in orgGroups)
        {
            var org = await _db.Organizations.FindAsync(orgGroup.Key);
            orgContributions.Add(new OrgUsageContributionDto
            {
                OrganizationId = orgGroup.Key,
                OrganizationName = org?.Name ?? "Unknown",
                LrmCharsUsed = orgGroup.Where(e => e.IsLrmProvider).Sum(e => e.CharactersUsed),
                ByokCharsUsed = orgGroup.Where(e => !e.IsLrmProvider).Sum(e => e.CharactersUsed)
            });
        }

        return new UserUsageBreakdownDto
        {
            PersonalLrmChars = personalEvents.Where(e => e.IsLrmProvider).Sum(e => e.CharactersUsed),
            PersonalByokChars = personalEvents.Where(e => !e.IsLrmProvider).Sum(e => e.CharactersUsed),
            OrganizationContributions = orgContributions.OrderByDescending(c => c.LrmCharsUsed + c.ByokCharsUsed).ToList()
        };
    }

    public async Task<List<OrgMemberUsageDto>> GetOrgMemberUsageAsync(int organizationId, int userId)
    {
        // Check if user is admin/owner
        var membership = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        if (membership == null || !OrganizationRole.IsAdminOrOwner(membership.Role))
        {
            return new List<OrgMemberUsageDto>();
        }

        var events = await _db.UsageEvents
            .Include(e => e.ActingUser)
            .Where(e => e.OrganizationId == organizationId)
            .ToListAsync();

        return events
            .GroupBy(e => e.ActingUserId)
            .Select(g => new OrgMemberUsageDto
            {
                UserId = g.Key,
                UserName = g.First().ActingUser?.DisplayName ?? g.First().ActingUser?.Email ?? "Unknown",
                Email = g.First().ActingUser?.Email ?? "",
                LrmCharsUsed = g.Where(e => e.IsLrmProvider).Sum(e => e.CharactersUsed),
                ByokCharsUsed = g.Where(e => !e.IsLrmProvider).Sum(e => e.CharactersUsed)
            })
            .OrderByDescending(m => m.TotalCharsUsed)
            .ToList();
    }

    public async Task<ProjectUsageDto?> GetProjectUsageAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return null;
        }

        // Check access: user is project owner, or member of the owning org
        bool hasAccess = project.UserId == userId;
        if (!hasAccess && project.OrganizationId.HasValue)
        {
            hasAccess = await _db.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == project.OrganizationId && m.UserId == userId);
        }

        if (!hasAccess)
        {
            return null;
        }

        var events = await _db.UsageEvents
            .Include(e => e.ActingUser)
            .Where(e => e.ProjectId == projectId)
            .ToListAsync();

        return new ProjectUsageDto
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            TotalLrmChars = events.Where(e => e.IsLrmProvider).Sum(e => e.CharactersUsed),
            TotalByokChars = events.Where(e => !e.IsLrmProvider).Sum(e => e.CharactersUsed),
            MemberBreakdown = events
                .GroupBy(e => e.ActingUserId)
                .Select(g => new ProjectMemberUsageDto
                {
                    UserId = g.Key,
                    UserName = g.First().ActingUser?.DisplayName ?? g.First().ActingUser?.Email ?? "Unknown",
                    LrmCharsUsed = g.Where(e => e.IsLrmProvider).Sum(e => e.CharactersUsed),
                    ByokCharsUsed = g.Where(e => !e.IsLrmProvider).Sum(e => e.CharactersUsed)
                })
                .OrderByDescending(m => m.TotalCharsUsed)
                .ToList()
        };
    }
}
