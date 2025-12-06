namespace LocalizationManager.Core.Scanning.Models;

/// <summary>
/// Result of scanning source code for localization key references
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Total number of files scanned
    /// </summary>
    public int FilesScanned { get; set; }

    /// <summary>
    /// Total number of key references found
    /// </summary>
    public int TotalReferences { get; set; }

    /// <summary>
    /// Number of unique keys found in code
    /// </summary>
    public int UniqueKeysFound { get; set; }

    /// <summary>
    /// Keys referenced in code but missing from .resx files
    /// </summary>
    public List<KeyUsage> MissingKeys { get; set; } = new();

    /// <summary>
    /// Keys defined in .resx but never used in code
    /// </summary>
    public List<string> UnusedKeys { get; set; } = new();

    /// <summary>
    /// All key usage information
    /// </summary>
    public List<KeyUsage> AllKeyUsages { get; set; } = new();

    /// <summary>
    /// Number of warnings (low-confidence references)
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Source path that was scanned
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Resource path used for comparison
    /// </summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>
    /// List of file patterns that were excluded
    /// </summary>
    public List<string> ExcludedPatterns { get; set; } = new();

    /// <summary>
    /// Whether the scan found any issues
    /// </summary>
    public bool HasIssues => MissingKeys.Count > 0 || UnusedKeys.Count > 0;
}
