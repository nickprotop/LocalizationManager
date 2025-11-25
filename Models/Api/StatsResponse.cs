namespace LocalizationManager.Models.Api;

public class StatsResponse
{
    public int TotalKeys { get; set; }
    public List<LanguageStats> Languages { get; set; } = new();
    public double OverallCoverage { get; set; }
}

public class LanguageStats
{
    public string LanguageCode { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int TranslatedCount { get; set; }
    public int TotalCount { get; set; }
    public double Coverage { get; set; }
}
