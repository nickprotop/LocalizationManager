using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Detailed usage event for tracking who translated what, on which project, in which organization.
/// Used for analytics and detailed billing breakdown.
/// </summary>
[Table("usage_events")]
public class UsageEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// The user who performed the translation action.
    /// </summary>
    [Column("acting_user_id")]
    public int ActingUserId { get; set; }

    [ForeignKey(nameof(ActingUserId))]
    public User? ActingUser { get; set; }

    /// <summary>
    /// The user whose quota was charged (org owner for org projects).
    /// </summary>
    [Column("billed_user_id")]
    public int BilledUserId { get; set; }

    [ForeignKey(nameof(BilledUserId))]
    public User? BilledUser { get; set; }

    /// <summary>
    /// The project where translation occurred (optional).
    /// </summary>
    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// The organization that owns the project (if any).
    /// </summary>
    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    /// <summary>
    /// Number of characters translated.
    /// </summary>
    [Column("characters_used")]
    public long CharactersUsed { get; set; }

    /// <summary>
    /// Provider used for translation: "lrm", "google", "deepl", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// True if LRM managed provider was used, false for BYOK.
    /// </summary>
    [Column("is_lrm_provider")]
    public bool IsLrmProvider { get; set; }

    /// <summary>
    /// Source of the API key: "user", "organization", "project", or "lrm" for the managed provider.
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("key_source")]
    public string KeySource { get; set; } = string.Empty;

    /// <summary>
    /// When this event occurred.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
