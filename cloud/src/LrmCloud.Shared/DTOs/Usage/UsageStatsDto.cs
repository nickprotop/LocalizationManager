namespace LrmCloud.Shared.DTOs.Usage;

/// <summary>
/// User usage statistics for the dashboard.
/// </summary>
public class UsageStatsDto
{
    // Translation usage
    public int TranslationCharsUsed { get; set; }
    public int TranslationCharsLimit { get; set; }
    public DateTime? TranslationCharsResetAt { get; set; }
    public double TranslationUsagePercent => TranslationCharsLimit > 0
        ? Math.Round((double)TranslationCharsUsed / TranslationCharsLimit * 100, 1)
        : 0;

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
