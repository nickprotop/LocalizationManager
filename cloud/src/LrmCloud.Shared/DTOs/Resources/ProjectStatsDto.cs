namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Translation statistics for a project.
/// </summary>
public class ProjectStatsDto
{
    public int TotalKeys { get; set; }
    public Dictionary<string, LanguageStats> Languages { get; set; } = new();
    public double OverallCompletion { get; set; }
}

/// <summary>
/// Statistics for a specific language in a project.
/// </summary>
public class LanguageStats
{
    public string LanguageCode { get; set; } = "";
    public int TranslatedCount { get; set; }
    public int PendingCount { get; set; }
    public int ReviewedCount { get; set; }
    public int ApprovedCount { get; set; }
    public double CompletionPercentage { get; set; }
}
