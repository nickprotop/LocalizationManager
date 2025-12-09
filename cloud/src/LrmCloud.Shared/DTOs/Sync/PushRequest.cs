namespace LrmCloud.Shared.DTOs.Sync;

/// <summary>
/// Request to push resource changes to the server.
/// Only includes files that have been modified, added, or deleted since the last push.
/// </summary>
public class PushRequest
{
    /// <summary>
    /// Optional lrm.json configuration content.
    /// Only included if the configuration has changed.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// List of files that have been modified or added.
    /// Does not include unchanged files (incremental sync optimization).
    /// </summary>
    public List<FileDto> ModifiedFiles { get; set; } = new();

    /// <summary>
    /// List of file paths that have been deleted locally.
    /// Server will remove the corresponding language translations from the database.
    /// Example: ["Resources.el.resx"] will delete all Greek translations.
    /// </summary>
    public List<string> DeletedFiles { get; set; } = new();
}
