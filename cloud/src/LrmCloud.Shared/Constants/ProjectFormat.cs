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
    /// Android strings.xml format
    /// </summary>
    public const string Android = "android";

    /// <summary>
    /// iOS .strings/.stringsdict format
    /// </summary>
    public const string Ios = "ios";

    /// <summary>
    /// All valid formats
    /// </summary>
    public static readonly string[] All = { Resx, Json, I18Next, Android, Ios };

    /// <summary>
    /// Check if a format is valid
    /// </summary>
    public static bool IsValid(string format)
    {
        return All.Contains(format, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the file extensions associated with a format
    /// </summary>
    public static string[] GetExtensions(string format)
    {
        return format.ToLowerInvariant() switch
        {
            Resx => new[] { ".resx" },
            Json or I18Next => new[] { ".json" },
            Android => new[] { ".xml" },
            Ios => new[] { ".strings", ".stringsdict" },
            _ => Array.Empty<string>()
        };
    }
}
