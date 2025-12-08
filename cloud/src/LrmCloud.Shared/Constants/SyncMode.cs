namespace LrmCloud.Shared.Constants;

/// <summary>
/// Project synchronization mode constants.
/// </summary>
public static class SyncMode
{
    /// <summary>
    /// Manual synchronization (user-triggered)
    /// </summary>
    public const string Manual = "manual";

    /// <summary>
    /// Automatic synchronization (on push to repo)
    /// </summary>
    public const string Auto = "auto";

    /// <summary>
    /// All valid sync modes
    /// </summary>
    public static readonly string[] All = { Manual, Auto };

    /// <summary>
    /// Check if a sync mode is valid
    /// </summary>
    public static bool IsValid(string mode)
    {
        return All.Contains(mode, StringComparer.OrdinalIgnoreCase);
    }
}
