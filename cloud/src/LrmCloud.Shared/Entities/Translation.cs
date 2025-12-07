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
    /// Version for optimistic locking.
    /// </summary>
    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
