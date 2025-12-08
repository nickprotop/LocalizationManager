namespace LrmCloud.Shared.Constants;

/// <summary>
/// Project format constants for localization file types.
/// </summary>
public static class ProjectFormat
{
    /// <summary>
    /// .NET RESX format
    /// </summary>
    public const string Resx = "resx";

    /// <summary>
    /// JSON format
    /// </summary>
    public const string Json = "json";

    /// <summary>
    /// i18next JSON format
    /// </summary>
    public const string I18Next = "i18next";

    /// <summary>
    /// All valid formats
    /// </summary>
    public static readonly string[] All = { Resx, Json, I18Next };

    /// <summary>
    /// Check if a format is valid
    /// </summary>
    public static bool IsValid(string format)
    {
        return All.Contains(format, StringComparer.OrdinalIgnoreCase);
    }
}
