namespace LocalizationManager.Core.Enums;

/// <summary>
/// Defines the supported output formats for command results.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Formatted table output using Spectre.Console (default).
    /// </summary>
    Table,

    /// <summary>
    /// JSON format for structured data output.
    /// </summary>
    Json,

    /// <summary>
    /// Simple plain text output without formatting.
    /// </summary>
    Simple
}
