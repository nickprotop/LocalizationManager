namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Translation data transfer object.
/// </summary>
public class TranslationDto
{
    public int Id { get; set; }
    public required string LanguageCode { get; set; }
    public string? Value { get; set; }
    public string PluralForm { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? TranslatedBy { get; set; }
    public int? ReviewedBy { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Translation-specific comment (per-language note).
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// True if this translation was synthesized (e.g., from SourceText for display),
    /// not stored in the database.
    /// </summary>
    public bool IsVirtual { get; set; }
}
