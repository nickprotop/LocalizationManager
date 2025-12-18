using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Translation Memory (TM) entry for reusing past translations.
/// Stores source text and its translation for exact/fuzzy matching.
/// </summary>
[Table("translation_memories")]
public class TranslationMemory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// User who owns this TM entry.
    /// </summary>
    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Optional organization for shared TM entries.
    /// </summary>
    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    /// <summary>
    /// Source language code (e.g., "en").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("source_language")]
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Target language code (e.g., "fr").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("target_language")]
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// Original source text.
    /// </summary>
    [Required]
    [Column("source_text")]
    public required string SourceText { get; set; }

    /// <summary>
    /// Translated text.
    /// </summary>
    [Required]
    [Column("translated_text")]
    public required string TranslatedText { get; set; }

    /// <summary>
    /// SHA256 hash of normalized source text for exact match lookup.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("source_hash")]
    public required string SourceHash { get; set; }

    /// <summary>
    /// Number of times this TM entry has been used.
    /// </summary>
    [Column("use_count")]
    public int UseCount { get; set; } = 1;

    /// <summary>
    /// Optional context (e.g., project name, key path) for disambiguation.
    /// </summary>
    [MaxLength(500)]
    [Column("context")]
    public string? Context { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
