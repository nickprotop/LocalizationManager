namespace LocalizationManager.Core.Scanning.Models;

/// <summary>
/// Confidence level for detected key references
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// High confidence - exact static reference (e.g., Resources.KeyName, GetString("Key"))
    /// </summary>
    High,

    /// <summary>
    /// Medium confidence - likely correct but some uncertainty (e.g., variable passed to GetString)
    /// </summary>
    Medium,

    /// <summary>
    /// Low confidence - dynamic or interpolated string (e.g., GetString($"{prefix}Key"))
    /// </summary>
    Low
}
