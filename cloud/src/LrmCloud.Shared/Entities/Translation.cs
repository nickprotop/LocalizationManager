using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Translation entity for a specific resource key and language.
/// </summary>
[Table("translations")]
public class Translation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("resource_key_id")]
    public int ResourceKeyId { get; set; }

    [ForeignKey(nameof(ResourceKeyId))]
    public ResourceKey? ResourceKey { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("language_code")]
    public required string LanguageCode { get; set; }

    [Column("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Per-language comment/note for this translation.
    /// Each language can have its own comment (e.g., "Save button" in EN, "Κουμπί αποθήκευσης" in EL).
    /// </summary>
    [Column("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// Plural form identifier (empty for non-plural, "one", "other", "few", "many" for plurals).
    /// </summary>
    [MaxLength(20)]
    [Column("plural_form")]
    public string PluralForm { get; set; } = "";

    /// <summary>
    /// Translation status: pending, translated, reviewed, approved.
    /// </summary>
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// How the translation was created: "manual", "machine:google", "machine:deepl", etc.
    /// </summary>
    [MaxLength(50)]
    [Column("translated_by")]
    public string? TranslatedBy { get; set; }

    [Column("reviewed_by")]
    public int? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }

    /// <summary>
    /// When the translation was reviewed.
    /// </summary>
    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// User who approved the translation.
    /// </summary>
    [Column("approved_by_id")]
    public int? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public User? ApprovedBy { get; set; }

    /// <summary>
    /// When the translation was approved.
    /// </summary>
    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Comment provided when rejecting a translation.
    /// </summary>
    [MaxLength(500)]
    [Column("rejection_comment")]
    public string? RejectionComment { get; set; }

    /// <summary>
    /// SHA256 hash of the translation value + comment.
    /// Used for three-way merge conflict detection.
    /// </summary>
    [MaxLength(64)]
    [Column("hash")]
    public string? Hash { get; set; }

    /// <summary>
    /// Version for optimistic locking.
    /// </summary>
    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
