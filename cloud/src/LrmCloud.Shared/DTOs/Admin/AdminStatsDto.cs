namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Database and system statistics for the admin dashboard.
/// </summary>
public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }  // Logged in last 30 days
    public int PaidUsers { get; set; }
    public int TotalOrganizations { get; set; }
    public int TotalProjects { get; set; }
    public long TotalTranslations { get; set; }
    public long TotalTranslationCharsUsed { get; set; }
    public long TotalOtherCharsUsed { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long StorageSizeBytes { get; set; }
    public Dictionary<string, int> UsersByPlan { get; set; } = new();
    public Dictionary<string, int> UsersByAuthType { get; set; } = new();
}
