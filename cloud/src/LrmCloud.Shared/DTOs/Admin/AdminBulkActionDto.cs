namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Base request for bulk actions.
/// </summary>
public class BulkActionRequest
{
    /// <summary>
    /// List of user IDs to apply the action to.
    /// </summary>
    public List<int> UserIds { get; set; } = new();
}

/// <summary>
/// Request to change plan for multiple users.
/// </summary>
public class BulkChangePlanRequest : BulkActionRequest
{
    /// <summary>
    /// New plan to assign (free, team, enterprise).
    /// </summary>
    public string NewPlan { get; set; } = string.Empty;
}

/// <summary>
/// Result of a bulk action operation.
/// </summary>
public class BulkActionResult
{
    /// <summary>
    /// Number of successfully processed items.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed items.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// List of errors for failed items.
    /// </summary>
    public List<BulkActionError> Errors { get; set; } = new();
}

/// <summary>
/// Error details for a single item in a bulk operation.
/// </summary>
public class BulkActionError
{
    /// <summary>
    /// ID of the user that failed.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Error message describing the failure.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
