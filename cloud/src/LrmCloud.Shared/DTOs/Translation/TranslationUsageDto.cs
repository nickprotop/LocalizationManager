namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Translation usage statistics for billing/limits.
/// </summary>
public class TranslationUsageDto
{
    /// <summary>
    /// Characters translated in current period.
    /// </summary>
    public long CharactersUsed { get; set; }

    /// <summary>
    /// Character limit for current period (null = unlimited).
    /// </summary>
    public long? CharacterLimit { get; set; }

    /// <summary>
    /// API calls made in current period.
    /// </summary>
    public int ApiCallsUsed { get; set; }

    /// <summary>
    /// API call limit for current period (null = unlimited).
    /// </summary>
    public int? ApiCallLimit { get; set; }

    /// <summary>
    /// When the usage counters reset.
    /// </summary>
    public DateTime? ResetsAt { get; set; }

    /// <summary>
    /// Current plan name.
    /// </summary>
    public string Plan { get; set; } = "free";

    /// <summary>
    /// Percentage of character limit used (0-100).
    /// </summary>
    public double UsagePercentage => CharacterLimit.HasValue && CharacterLimit.Value > 0
        ? (double)CharactersUsed / CharacterLimit.Value * 100
        : 0;

    /// <summary>
    /// Whether the user has exceeded their limit.
    /// </summary>
    public bool IsOverLimit => CharacterLimit.HasValue && CharactersUsed >= CharacterLimit.Value;
}

/// <summary>
/// Usage breakdown by provider.
/// </summary>
public class ProviderUsageDto
{
    public string ProviderName { get; set; } = string.Empty;
    public long CharactersUsed { get; set; }
    public int ApiCalls { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
