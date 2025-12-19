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

    /// <summary>
    /// Number of translations excluded due to workflow requirements.
    /// Only populated when ReviewWorkflowEnabled is true and RequireApprovalBeforeExport or RequireReviewBeforeExport is set.
    /// </summary>
    public int ExcludedTranslationCount { get; set; }

    /// <summary>
    /// Informational message about excluded translations due to workflow.
    /// Null if no translations were excluded.
    /// </summary>
    public string? WorkflowMessage { get; set; }
}
