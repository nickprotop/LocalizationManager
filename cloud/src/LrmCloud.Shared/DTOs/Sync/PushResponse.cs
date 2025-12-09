namespace LrmCloud.Shared.DTOs.Sync;

/// <summary>
/// Response after pushing resources to the server.
/// </summary>
public class PushResponse
{
    /// <summary>
    /// Indicates whether the push operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of files that were modified or added.
    /// </summary>
    public int ModifiedCount { get; set; }

    /// <summary>
    /// Number of files/languages that were deleted.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Optional message to display to the user.
    /// </summary>
    public string? Message { get; set; }
}
