namespace LocalizationManager.Core.Scanning.Models;

/// <summary>
/// Represents a single reference to a localization key in source code
/// </summary>
public class KeyReference
{
    /// <summary>
    /// The localization key being referenced
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// File path where the reference was found
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number in the file (1-based)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number in the line (1-based), if available
    /// </summary>
    public int? Column { get; set; }

    /// <summary>
    /// The pattern that matched (e.g., "Resources.KeyName", "@Localizer[\"Key\"]")
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Surrounding code context for the reference
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Confidence level for this reference
    /// </summary>
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;

    /// <summary>
    /// Warning message if confidence is low
    /// </summary>
    public string? Warning { get; set; }
}
