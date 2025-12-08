namespace LrmCloud.Shared.Constants;

/// <summary>
/// Translation status constants for workflow management.
/// </summary>
public static class TranslationStatus
{
    /// <summary>
    /// Translation is pending (not yet translated)
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// Translation is completed
    /// </summary>
    public const string Translated = "translated";

    /// <summary>
    /// Translation has been reviewed
    /// </summary>
    public const string Reviewed = "reviewed";

    /// <summary>
    /// Translation is approved and final
    /// </summary>
    public const string Approved = "approved";

    /// <summary>
    /// All valid status values
    /// </summary>
    public static readonly string[] All = { Pending, Translated, Reviewed, Approved };

    /// <summary>
    /// Check if a status is valid
    /// </summary>
    public static bool IsValid(string status)
    {
        return All.Contains(status, StringComparer.OrdinalIgnoreCase);
    }
}
