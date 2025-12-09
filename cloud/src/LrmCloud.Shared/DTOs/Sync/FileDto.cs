namespace LrmCloud.Shared.DTOs.Sync;

/// <summary>
/// Represents a resource file with its path and content.
/// </summary>
public class FileDto
{
    /// <summary>
    /// Relative path of the file (e.g., "Resources.resx", "strings.el.json").
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Raw file content (XML for RESX, JSON for JSON formats).
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// SHA256 hash of the content for change detection (optional).
    /// </summary>
    public string? Hash { get; set; }
}
