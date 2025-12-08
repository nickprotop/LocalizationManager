namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Represents a resource file (for push/pull operations).
/// </summary>
public class ResourceDto
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Hash { get; set; }
    public string? LanguageCode { get; set; }
}
