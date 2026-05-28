namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Resource key data transfer object.
/// </summary>
public class ResourceKeyDto
{
    public int Id { get; set; }
    public required string KeyName { get; set; }

    /// <summary>
    /// Base name of the resource group this key belongs to.
    /// Empty string for single-group projects.
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    public string? KeyPath { get; set; }
    public bool IsPlural { get; set; }
    /// <summary>
    /// Source text for this key (value from default language file, msgid for PO format).
    /// </summary>
    public string? SourceText { get; set; }
    /// <summary>
    /// For plural keys, the source plural text pattern (PO msgid_plural or "other" form).
    /// </summary>
    public string? SourcePluralText { get; set; }
    public string? Comment { get; set; }
    public int Version { get; set; }
    public int TranslationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
