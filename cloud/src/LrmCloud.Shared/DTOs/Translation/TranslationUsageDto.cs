namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Translation usage statistics for billing/limits.
/// </summary>
public class TranslationUsageDto
{
    // LRM Translation usage (counts against plan)

    /// <summary>
    /// LRM characters translated in current period.
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
    /// Whether the user has exceeded their LRM limit.
    /// </summary>
    public bool IsOverLimit => CharacterLimit.HasValue && CharactersUsed >= CharacterLimit.Value;

    // Other providers usage (BYOK + free community)

    /// <summary>
    /// Other providers characters translated this period.
    /// </summary>
    public long OtherCharactersUsed { get; set; }

    /// <summary>
    /// Other providers character limit (null = unlimited).
    /// </summary>
    public long? OtherCharacterLimit { get; set; }

    /// <summary>
    /// When the other providers usage counter resets.
    /// </summary>
    public DateTime? OtherResetsAt { get; set; }

    /// <summary>
    /// Percentage of other providers limit used (0-100).
    /// </summary>
    public double OtherUsagePercentage => OtherCharacterLimit.HasValue && OtherCharacterLimit.Value > 0
        ? (double)OtherCharactersUsed / OtherCharacterLimit.Value * 100
        : 0;

    /// <summary>
    /// Whether the user has exceeded their other providers limit.
    /// </summary>
    public bool IsOtherOverLimit => OtherCharacterLimit.HasValue && OtherCharactersUsed >= OtherCharacterLimit.Value;

    /// <summary>
    /// Legacy property - maps to OtherCharactersUsed.
    /// </summary>
    public long ByokCharactersUsed => OtherCharactersUsed;
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
