using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Translation of a glossary term in a specific target language.
/// </summary>
[Table("glossary_translations")]
public class GlossaryTranslation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// The glossary term this translation belongs to.
    /// </summary>
    [Column("term_id")]
    public int TermId { get; set; }

    [ForeignKey(nameof(TermId))]
    public GlossaryTerm? Term { get; set; }

    /// <summary>
    /// Target language code (e.g., "fr").
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("target_language")]
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// The translated term (e.g., "Tableau de bord").
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("translated_term")]
    public required string TranslatedTerm { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
