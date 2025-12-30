using LrmCloud.Shared.DTOs.Sync;

namespace LrmCloud.Shared.DTOs.Files;

/// <summary>
/// Request to import files into a project.
/// </summary>
public class FileImportRequest
{
    /// <summary>
    /// Files to import (path + content pairs).
    /// </summary>
    public List<FileDto> Files { get; set; } = new();

    /// <summary>
    /// Resource format (resx, json, i18next, android, ios, po, xliff).
    /// If null, auto-detected from file extensions.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Optional message for the import (stored in sync history).
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Response from file import operation.
/// </summary>
public class FileImportResponse
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of entries added/modified.
    /// </summary>
    public int Applied { get; set; }

    /// <summary>
    /// History ID for this import operation.
    /// </summary>
    public string? HistoryId { get; set; }

    /// <summary>
    /// Any errors that occurred during import.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
