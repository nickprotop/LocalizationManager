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

    /// <summary>
    /// BYOK (Bring Your Own Key) translation characters used.
    /// Tracked separately from LRM usage. Unlimited, but monitored.
    /// </summary>
    public long ByokCharsUsed { get; set; }

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
}
