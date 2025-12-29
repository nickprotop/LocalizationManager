namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Resource key data transfer object.
/// </summary>
public class ResourceKeyDto
{
    public int Id { get; set; }
    public required string KeyName { get; set; }
    public string? KeyPath { get; set; }
    public bool IsPlural { get; set; }
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
