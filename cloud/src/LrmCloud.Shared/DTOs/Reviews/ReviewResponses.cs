namespace LrmCloud.Shared.DTOs.Reviews;

/// <summary>
/// Response from a bulk review/approve operation.
/// </summary>
public class BulkReviewResponse
{
    /// <summary>
    /// Number of translations successfully processed.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Number of translations skipped (wrong status, no permission, etc.).
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// IDs of translations that were skipped.
    /// </summary>
    public List<int> SkippedIds { get; set; } = new();

    /// <summary>
    /// Reason for skipped translations.
    /// </summary>
    public string? SkipReason { get; set; }
}
