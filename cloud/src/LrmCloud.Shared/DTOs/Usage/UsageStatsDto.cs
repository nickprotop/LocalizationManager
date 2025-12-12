namespace LrmCloud.Shared.DTOs.Usage;

/// <summary>
/// User usage statistics for the dashboard.
/// </summary>
public class UsageStatsDto
{
    // LRM Translation usage (counts against plan)
    public int TranslationCharsUsed { get; set; }
    public int TranslationCharsLimit { get; set; }
    public DateTime? TranslationCharsResetAt { get; set; }
    public double TranslationUsagePercent => TranslationCharsLimit > 0
        ? Math.Round((double)TranslationCharsUsed / TranslationCharsLimit * 100, 1)
        : 0;

    // Other providers usage (BYOK + free community)

    /// <summary>
    /// Other providers characters used this period.
    /// </summary>
    public long OtherCharsUsed { get; set; }

    /// <summary>
    /// Other providers character limit per month.
    /// </summary>
    public long OtherCharsLimit { get; set; }

    /// <summary>
    /// When the other providers usage counter resets.
    /// </summary>
    public DateTime? OtherCharsResetAt { get; set; }

    /// <summary>
    /// Percentage of other providers limit used (0-100).
    /// </summary>
    public double OtherUsagePercent => OtherCharsLimit > 0
        ? Math.Round((double)OtherCharsUsed / OtherCharsLimit * 100, 1)
        : 0;

    /// <summary>
    /// Legacy property - maps to OtherCharsUsed.
    /// </summary>
    public long ByokCharsUsed => OtherCharsUsed;

    // Project stats
    public int ProjectCount { get; set; }
    public int ResourceFileCount { get; set; }
    public int TotalKeyCount { get; set; }

    // API key stats
    public int ApiKeyCount { get; set; }
    public int ActiveApiKeyCount { get; set; }

    // Account info
    public string Plan { get; set; } = "free";
    public DateTime MemberSince { get; set; }

    // Plan limits
    /// <summary>
    /// Maximum projects allowed on current plan.
    /// int.MaxValue means unlimited.
    /// </summary>
    public int MaxProjects { get; set; }

    /// <summary>
    /// Maximum API keys allowed on current plan.
    /// int.MaxValue means unlimited.
    /// </summary>
    public int MaxApiKeys { get; set; }

    /// <summary>
    /// Maximum team members allowed on current plan.
    /// 0 means teams not available, int.MaxValue means unlimited.
    /// </summary>
    public int MaxTeamMembers { get; set; }

    // Computed limit properties
    public bool CanCreateProject => MaxProjects == int.MaxValue || ProjectCount < MaxProjects;
    public bool CanCreateApiKey => MaxApiKeys == int.MaxValue || ApiKeyCount < MaxApiKeys;
    public bool CanCreateOrganization => MaxTeamMembers > 0;
    public bool IsProjectLimitReached => MaxProjects != int.MaxValue && ProjectCount >= MaxProjects;
    public bool IsApiKeyLimitReached => MaxApiKeys != int.MaxValue && ApiKeyCount >= MaxApiKeys;
}
