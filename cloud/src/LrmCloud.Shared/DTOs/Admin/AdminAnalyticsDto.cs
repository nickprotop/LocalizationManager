namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Revenue analytics data including MRR and history.
/// </summary>
public class RevenueAnalyticsDto
{
    /// <summary>Current month's MRR (Monthly Recurring Revenue).</summary>
    public decimal CurrentMrr { get; set; }

    /// <summary>Previous month's MRR for comparison.</summary>
    public decimal PreviousMrr { get; set; }

    /// <summary>MRR growth percentage from previous month.</summary>
    public decimal MrrGrowthPercent { get; set; }

    /// <summary>Historical MRR data points for charting.</summary>
    public List<MrrDataPoint> MrrHistory { get; set; } = new();

    /// <summary>Revenue breakdown by plan (team, enterprise).</summary>
    public Dictionary<string, decimal> RevenueByPlan { get; set; } = new();
}

/// <summary>
/// Single data point for MRR history chart.
/// </summary>
public class MrrDataPoint
{
    public DateTime Month { get; set; }
    public decimal Mrr { get; set; }
    public int PaidUsers { get; set; }
}

/// <summary>
/// User analytics including growth, churn, and conversions.
/// </summary>
public class UserAnalyticsDto
{
    public int TotalUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersLastMonth { get; set; }
    public int ChurnedUsersThisMonth { get; set; }
    public decimal ChurnRate { get; set; }
    public decimal GrowthRate { get; set; }

    /// <summary>Historical user growth data points.</summary>
    public List<UserGrowthDataPoint> GrowthHistory { get; set; } = new();

    /// <summary>Plan conversion funnel data.</summary>
    public List<ConversionDataPoint> ConversionFunnel { get; set; } = new();
}

/// <summary>
/// Single data point for user growth history.
/// </summary>
public class UserGrowthDataPoint
{
    public DateTime Month { get; set; }
    public int NewUsers { get; set; }
    public int ChurnedUsers { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}

/// <summary>
/// Plan conversion data point (e.g., free to team).
/// </summary>
public class ConversionDataPoint
{
    public string FromPlan { get; set; } = string.Empty;
    public string ToPlan { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>
/// Usage analytics including translation character trends.
/// </summary>
public class UsageAnalyticsDto
{
    public long TotalCharsThisMonth { get; set; }
    public long TotalCharsLastMonth { get; set; }
    public decimal GrowthPercent { get; set; }
    public long TotalLrmChars { get; set; }
    public long TotalByokChars { get; set; }

    /// <summary>Daily usage trend data points.</summary>
    public List<UsageTrendDataPoint> DailyTrends { get; set; } = new();

    /// <summary>Usage breakdown by provider.</summary>
    public Dictionary<string, long> UsageByProvider { get; set; } = new();
}

/// <summary>
/// Single data point for daily usage trends.
/// </summary>
public class UsageTrendDataPoint
{
    public DateTime Date { get; set; }
    public long LrmChars { get; set; }
    public long ByokChars { get; set; }
    public int ApiCalls { get; set; }
}

/// <summary>
/// Active user trend data (DAU/WAU/MAU).
/// </summary>
public class ActiveUserTrendDto
{
    public DateTime Date { get; set; }
    public int DailyActiveUsers { get; set; }
    public int WeeklyActiveUsers { get; set; }
    public int MonthlyActiveUsers { get; set; }
}

/// <summary>
/// Combined analytics response for the analytics dashboard.
/// </summary>
public class AdminAnalyticsSummaryDto
{
    public RevenueAnalyticsDto Revenue { get; set; } = new();
    public UserAnalyticsDto Users { get; set; } = new();
    public UsageAnalyticsDto Usage { get; set; } = new();
}
