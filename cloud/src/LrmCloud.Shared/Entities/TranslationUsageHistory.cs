using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Per-provider translation usage history for detailed tracking.
/// Aggregated by user, provider, and period for analytics.
/// </summary>
[Table("translation_usage_history")]
public class TranslationUsageHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Provider name: "lrm", "google", "deepl", "mymemory", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider_name")]
    public required string ProviderName { get; set; }

    /// <summary>
    /// Characters translated using this provider.
    /// </summary>
    [Column("chars_used")]
    public long CharsUsed { get; set; }

    /// <summary>
    /// Number of API calls made to this provider.
    /// </summary>
    [Column("api_calls")]
    public int ApiCalls { get; set; }

    /// <summary>
    /// Period start (typically month start for monthly aggregation).
    /// </summary>
    [Column("period_start")]
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Period end (typically month end).
    /// </summary>
    [Column("period_end")]
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Last time this record was updated.
    /// </summary>
    [Column("last_used_at")]
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was created.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
