namespace LrmCloud.Shared.DTOs.Files;

/// <summary>
/// Preview response for file export.
/// </summary>
public class FileExportPreviewResponse
{
    /// <summary>
    /// List of files that would be generated.
    /// </summary>
    public List<ExportFileInfo> Files { get; set; } = new();

    /// <summary>
    /// Total number of keys across all files.
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Available formats for export.
    /// </summary>
    public List<string> AvailableFormats { get; set; } = new()
    {
        "resx", "json", "i18next", "android", "ios", "po", "xliff"
    };
}

/// <summary>
/// Information about a single file in export.
/// </summary>
public class ExportFileInfo
{
    /// <summary>
    /// File path (relative).
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Language code for this file.
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// Number of keys in this file.
    /// </summary>
    public int KeyCount { get; set; }

    /// <summary>
    /// Whether this is the default language file.
    /// </summary>
    public bool IsDefault { get; set; }
}
