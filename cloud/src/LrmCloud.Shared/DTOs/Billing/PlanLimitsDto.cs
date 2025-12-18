namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Plan limits configuration for display in billing UI.
/// </summary>
public record PlanLimitsDto
{
    /// <summary>
    /// Free tier limits.
    /// </summary>
    public PlanTierLimits Free { get; init; } = new();

    /// <summary>
    /// Team tier limits.
    /// </summary>
    public PlanTierLimits Team { get; init; } = new();

    /// <summary>
    /// Enterprise tier limits.
    /// </summary>
    public PlanTierLimits Enterprise { get; init; } = new();
}

/// <summary>
/// Limits for a specific plan tier.
/// </summary>
public record PlanTierLimits
{
    /// <summary>
    /// LRM translation character limit per month.
    /// </summary>
    public int TranslationChars { get; init; }

    /// <summary>
    /// BYOK/Other provider character limit per month.
    /// </summary>
    public long OtherChars { get; init; }

    /// <summary>
    /// Maximum number of projects (int.MaxValue = unlimited).
    /// </summary>
    public int MaxProjects { get; init; }

    /// <summary>
    /// Maximum team members (0 = not available, int.MaxValue = unlimited).
    /// </summary>
    public int MaxTeamMembers { get; init; }

    /// <summary>
    /// Maximum API keys (int.MaxValue = unlimited).
    /// </summary>
    public int MaxApiKeys { get; init; }

    /// <summary>
    /// Maximum snapshots per project.
    /// </summary>
    public int MaxSnapshots { get; init; }

    /// <summary>
    /// Snapshot retention days.
    /// </summary>
    public int SnapshotRetentionDays { get; init; }

    /// <summary>
    /// Maximum storage bytes per account.
    /// </summary>
    public long MaxStorageBytes { get; init; }

    /// <summary>
    /// Maximum file size bytes.
    /// </summary>
    public int MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Monthly price in USD.
    /// </summary>
    public decimal Price { get; init; }
}
