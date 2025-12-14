using MudBlazor;

namespace LrmCloud.Web.Helpers;

/// <summary>
/// Shared UI helper methods for consistent styling across components.
/// </summary>
public static class UiHelpers
{
    // ==========================================================================
    // Number/Data Formatting
    // ==========================================================================

    /// <summary>
    /// Formats a character count for display (e.g., "1.5M", "100K", "500").
    /// </summary>
    public static string FormatCharacters(long chars)
    {
        if (chars >= 1_000_000)
            return $"{chars / 1_000_000.0:F1}M";
        if (chars >= 1_000)
            return $"{chars / 1_000.0:F1}K";
        return chars.ToString("N0");
    }

    /// <summary>
    /// Formats a number for display (e.g., "1.5M", "100K", "500").
    /// </summary>
    public static string FormatNumber(long number)
    {
        if (number >= 1_000_000)
            return $"{number / 1_000_000.0:F1}M";
        if (number >= 1_000)
            return $"{number / 1_000.0:F1}K";
        return number.ToString("N0");
    }

    /// <summary>
    /// Formats bytes for display (e.g., "1.5 GB", "100 MB", "500 KB").
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824)
            return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)
            return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)
            return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

    /// <summary>
    /// Formats a percentage for display.
    /// </summary>
    public static string FormatPercentage(double percentage, int decimals = 0)
    {
        return percentage.ToString($"F{decimals}") + "%";
    }

    // ==========================================================================
    // Color Helpers for Project/Format/Completion
    // ==========================================================================

    /// <summary>
    /// Gets color based on resource file format.
    /// </summary>
    public static Color GetFormatColor(string format) => format?.ToLower() switch
    {
        "resx" => Color.Primary,
        "json" => Color.Info,
        "jsonlocalization" => Color.Info,
        "i18next" => Color.Secondary,
        _ => Color.Default
    };

    /// <summary>
    /// Gets color based on translation completion percentage.
    /// </summary>
    public static Color GetCompletionColor(double percentage) => percentage switch
    {
        >= 90 => Color.Success,
        >= 50 => Color.Warning,
        _ => Color.Error
    };

    /// <summary>
    /// Gets color based on usage percentage (for limits/quotas).
    /// </summary>
    public static Color GetUsageColor(double percentage) => percentage switch
    {
        >= 90 => Color.Error,
        >= 70 => Color.Warning,
        _ => Color.Success
    };

    /// <summary>
    /// Gets color based on usage values.
    /// </summary>
    public static Color GetUsageColor(long used, long limit)
    {
        if (limit <= 0) return Color.Success;
        var percentage = (double)used / limit * 100;
        return GetUsageColor(percentage);
    }

    /// <summary>
    /// Calculates usage percentage with capping at 100.
    /// </summary>
    public static double GetUsagePercentage(long used, long limit)
    {
        if (limit <= 0) return 0;
        return Math.Min(100, (double)used / limit * 100);
    }

    // ==========================================================================
    // Plan/Subscription Colors
    // ==========================================================================

    /// <summary>
    /// Gets color based on subscription plan.
    /// </summary>
    public static Color GetPlanColor(string plan) => plan?.ToLower() switch
    {
        "free" => Color.Default,
        "team" => Color.Secondary,
        "enterprise" => Color.Primary,
        _ => Color.Default
    };

    // ==========================================================================
    // Organization/Role Colors
    // ==========================================================================

    /// <summary>
    /// Gets color based on organization role.
    /// </summary>
    public static Color GetRoleColor(string role) => role switch
    {
        "owner" => Color.Error,
        "Owner" => Color.Error,
        "admin" => Color.Warning,
        "Admin" => Color.Warning,
        "member" => Color.Primary,
        "Member" => Color.Primary,
        "viewer" => Color.Default,
        "Viewer" => Color.Default,
        _ => Color.Default
    };

    /// <summary>
    /// Gets consistent color based on string (for avatars, etc.).
    /// </summary>
    public static Color GetHashColor(string text)
    {
        var hash = text?.GetHashCode() ?? 0;
        var colors = new[] { Color.Primary, Color.Secondary, Color.Info, Color.Success, Color.Warning };
        return colors[Math.Abs(hash) % colors.Length];
    }

    // ==========================================================================
    // Text Helpers
    // ==========================================================================

    /// <summary>
    /// Gets initials from a name (e.g., "John Doe" -> "JD").
    /// </summary>
    public static string GetInitials(string name, int maxLength = 2)
    {
        if (string.IsNullOrEmpty(name))
            return "?";

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Join("", words.Take(maxLength).Select(w => char.ToUpper(w[0])));

        return name.Length >= maxLength
            ? name[..maxLength].ToUpper()
            : name.ToUpper();
    }

    /// <summary>
    /// Truncates text with ellipsis if it exceeds max length.
    /// </summary>
    public static string Truncate(string text, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? "";

        return text[..(maxLength - ellipsis.Length)] + ellipsis;
    }

    // ==========================================================================
    // Time/Date Helpers
    // ==========================================================================

    /// <summary>
    /// Gets relative time string (e.g., "2 hours ago", "3 days ago").
    /// </summary>
    public static string GetRelativeTime(DateTime dateTime)
    {
        var now = DateTime.UtcNow;
        var diff = now - dateTime;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes != 1 ? "s" : "")} ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours != 1 ? "s" : "")} ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays != 1 ? "s" : "")} ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} week{((int)(diff.TotalDays / 7) != 1 ? "s" : "")} ago";
        if (diff.TotalDays < 365)
            return $"{(int)(diff.TotalDays / 30)} month{((int)(diff.TotalDays / 30) != 1 ? "s" : "")} ago";

        return $"{(int)(diff.TotalDays / 365)} year{((int)(diff.TotalDays / 365) != 1 ? "s" : "")} ago";
    }
}
