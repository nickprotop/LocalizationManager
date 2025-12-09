namespace LrmCloud.Shared.DTOs.Sync;

/// <summary>
/// Response containing all resource files from the server.
/// </summary>
public class PullResponse
{
    /// <summary>
    /// Optional lrm.json configuration content.
    /// If the project has a configuration stored on the server, it will be included here.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// List of all resource files for the project.
    /// Includes all languages currently stored in the database.
    /// </summary>
    public List<FileDto> Files { get; set; } = new();
}
