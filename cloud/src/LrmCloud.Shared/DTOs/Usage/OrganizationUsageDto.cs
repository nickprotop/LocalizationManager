namespace LrmCloud.Shared.DTOs.Usage;

/// <summary>
/// Organization usage statistics for billing page.
/// </summary>
public class OrganizationUsageDto
{
    /// <summary>
    /// LRM Translation characters used this period.
    /// </summary>
    public long LrmCharsUsed { get; set; }

    /// <summary>
    /// LRM Translation character limit per month.
    /// </summary>
    public long LrmCharsLimit { get; set; }

    /// <summary>
    /// Percentage of LRM limit used (0-100).
    /// </summary>
    public double LrmUsagePercent => LrmCharsLimit > 0
        ? Math.Round((double)LrmCharsUsed / LrmCharsLimit * 100, 1)
        : 0;

    /// <summary>
    /// Other providers characters used this period (BYOK + free community).
    /// </summary>
    public long OtherCharsUsed { get; set; }

    /// <summary>
    /// Number of API calls made this period.
    /// </summary>
    public int ApiCalls { get; set; }

    /// <summary>
    /// Total storage used in bytes.
    /// </summary>
    public long StorageBytes { get; set; }

    /// <summary>
    /// Days remaining until billing cycle reset.
    /// </summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// Organization's current plan.
    /// </summary>
    public string Plan { get; set; } = "free";

    /// <summary>
    /// Number of members in the organization.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Maximum members allowed on current plan.
    /// </summary>
    public int MaxMembers { get; set; }

    /// <summary>
    /// Number of projects in the organization.
    /// </summary>
    public int ProjectCount { get; set; }
}
