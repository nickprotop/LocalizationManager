using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Glossary;

/// <summary>
/// Request to create a new glossary term.
/// </summary>
public class CreateGlossaryTermRequest
{
    /// <summary>
    /// The source term to add (e.g., "Dashboard").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public required string SourceTerm { get; set; }

    /// <summary>
    /// Source language code (e.g., "en").
    /// </summary>
    [Required]
    [MaxLength(10)]
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Optional description of the term's context.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether matching should be case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Translations for this term.
    /// Key = target language code, Value = translated term.
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();
}

/// <summary>
/// Request to update an existing glossary term.
/// </summary>
public class UpdateGlossaryTermRequest
{
    /// <summary>
    /// The source term (can be updated).
    /// </summary>
    [Required]
    [MaxLength(255)]
    public required string SourceTerm { get; set; }

    /// <summary>
    /// Source language code (can be updated).
    /// </summary>
    [Required]
    [MaxLength(10)]
    public required string SourceLanguage { get; set; }

    /// <summary>
    /// Optional description of the term's context.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether matching should be case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Translations for this term.
    /// Key = target language code, Value = translated term.
    /// Replaces all existing translations.
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();
}

/// <summary>
/// Request to add or update a single translation.
/// </summary>
public class SetGlossaryTranslationRequest
{
    /// <summary>
    /// Target language code (e.g., "fr").
    /// </summary>
    [Required]
    [MaxLength(10)]
    public required string TargetLanguage { get; set; }

    /// <summary>
    /// The translated term (e.g., "Tableau de bord").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public required string TranslatedTerm { get; set; }
}
