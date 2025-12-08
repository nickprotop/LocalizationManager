namespace LrmCloud.Shared.Constants;

/// <summary>
/// Project synchronization status constants.
/// </summary>
public static class SyncStatus
{
    /// <summary>
    /// Never been synced
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// Sync in progress
    /// </summary>
    public const string Syncing = "syncing";

    /// <summary>
    /// Last sync succeeded
    /// </summary>
    public const string Success = "success";

    /// <summary>
    /// Last sync failed
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// All valid status values
    /// </summary>
    public static readonly string[] All = { Pending, Syncing, Success, Failed };

    /// <summary>
    /// Check if a status is valid
    /// </summary>
    public static bool IsValid(string status)
    {
        return All.Contains(status, StringComparer.OrdinalIgnoreCase);
    }
}
