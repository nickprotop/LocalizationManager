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
    /// Number of entries added/modified (total).
    /// </summary>
    public int Applied { get; set; }

    /// <summary>
    /// Number of new keys added.
    /// </summary>
    public int Added { get; set; }

    /// <summary>
    /// Number of existing keys modified.
    /// </summary>
    public int Modified { get; set; }

    /// <summary>
    /// Number of entries unchanged (same value already exists).
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// History ID for this import operation.
    /// </summary>
    public string? HistoryId { get; set; }

    /// <summary>
    /// Any errors that occurred during import.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Files that failed to parse (not fatal - import continues with remaining files).
    /// </summary>
    public List<FileParseError> ParseErrors { get; set; } = new();
}

/// <summary>
/// Request to preview import changes before applying.
/// </summary>
public class FileImportPreviewRequest
{
    /// <summary>
    /// Files to preview (path + content pairs).
    /// </summary>
    public List<FileDto> Files { get; set; } = new();

    /// <summary>
    /// Resource format (resx, json, i18next, android, ios, po, xliff).
    /// If null, auto-detected from file extensions.
    /// </summary>
    public string? Format { get; set; }
}

/// <summary>
/// Response from import preview - shows what would change.
/// </summary>
public class FileImportPreviewResponse
{
    /// <summary>
    /// Summary of changes that would be applied.
    /// </summary>
    public ImportPreviewSummary Summary { get; set; } = new();

    /// <summary>
    /// List of changes that would be made.
    /// </summary>
    public List<ImportChangePreview> Changes { get; set; } = new();

    /// <summary>
    /// Files that failed to parse.
    /// </summary>
    public List<FileParseError> ParseErrors { get; set; } = new();

    /// <summary>
    /// Any errors that occurred during preview.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Summary of import changes.
/// </summary>
public class ImportPreviewSummary
{
    /// <summary>
    /// Total entries that would be processed.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// New keys that would be added.
    /// </summary>
    public int ToAdd { get; set; }

    /// <summary>
    /// Existing keys that would be modified.
    /// </summary>
    public int ToModify { get; set; }

    /// <summary>
    /// Entries that are unchanged (same value exists).
    /// </summary>
    public int Unchanged { get; set; }
}

/// <summary>
/// Preview of a single change.
/// </summary>
public class ImportChangePreview
{
    /// <summary>
    /// Resource key name.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Language code.
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// Type of change: "add", "modify", "unchanged".
    /// </summary>
    public string ChangeType { get; set; } = "";

    /// <summary>
    /// Current value (null for new keys).
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// New value from import.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Whether this is a plural key.
    /// </summary>
    public bool IsPlural { get; set; }
}

/// <summary>
/// Represents a file that failed to parse during import.
/// </summary>
public class FileParseError
{
    /// <summary>
    /// Path of the file that failed to parse.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Error message describing the parse failure.
    /// </summary>
    public string Error { get; set; } = "";
}
