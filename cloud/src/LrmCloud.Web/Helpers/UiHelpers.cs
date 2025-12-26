using LrmCloud.Shared.DTOs.Projects;
using Radzen;

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

    /// <summary>
    /// Calculates usage percentage with capping at 100.
    /// </summary>
    public static double GetUsagePercentage(long used, long limit)
    {
        if (limit <= 0) return 0;
        return Math.Min(100, (double)used / limit * 100);
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

    // ==========================================================================
    // Radzen-specific Helpers
    // ==========================================================================

    /// <summary>
    /// Gets Radzen BadgeStyle based on resource file format.
    /// </summary>
    public static BadgeStyle GetFormatBadgeStyle(string format) => format?.ToLower() switch
    {
        "resx" => BadgeStyle.Primary,
        "json" => BadgeStyle.Info,
        "jsonlocalization" => BadgeStyle.Info,
        "i18next" => BadgeStyle.Secondary,
        "android" => BadgeStyle.Success,
        "ios" => BadgeStyle.Warning,
        _ => BadgeStyle.Light
    };

    /// <summary>
    /// Gets Radzen ProgressBarStyle based on completion percentage.
    /// </summary>
    public static ProgressBarStyle GetCompletionProgressStyle(double percentage) => percentage switch
    {
        >= 90 => ProgressBarStyle.Success,
        >= 50 => ProgressBarStyle.Warning,
        _ => ProgressBarStyle.Danger
    };

    /// <summary>
    /// Gets Radzen ProgressBarStyle based on usage percentage (inverted - high usage is bad).
    /// </summary>
    public static ProgressBarStyle GetUsageProgressStyle(double percentage) => percentage switch
    {
        >= 90 => ProgressBarStyle.Danger,
        >= 70 => ProgressBarStyle.Warning,
        _ => ProgressBarStyle.Success
    };

    /// <summary>
    /// Gets Radzen ProgressBarStyle based on usage percentage (alias for consistency).
    /// </summary>
    public static ProgressBarStyle GetUsageProgressBarStyle(double percentage) => GetUsageProgressStyle(percentage);

    /// <summary>
    /// Gets CSS color value string based on usage percentage.
    /// </summary>
    public static string GetUsageColorValue(double percentage) => percentage switch
    {
        >= 90 => "var(--rz-danger)",
        >= 70 => "var(--rz-warning)",
        _ => "var(--rz-success)"
    };

    /// <summary>
    /// Gets Radzen BadgeStyle based on subscription plan.
    /// </summary>
    public static BadgeStyle GetPlanBadgeStyle(string plan) => plan?.ToLower() switch
    {
        "free" => BadgeStyle.Light,
        "team" => BadgeStyle.Secondary,
        "enterprise" => BadgeStyle.Primary,
        _ => BadgeStyle.Light
    };

    /// <summary>
    /// Gets Radzen BadgeStyle based on organization role.
    /// </summary>
    public static BadgeStyle GetRoleBadgeStyle(string role) => role switch
    {
        "owner" or "Owner" => BadgeStyle.Danger,
        "admin" or "Admin" => BadgeStyle.Warning,
        "member" or "Member" => BadgeStyle.Primary,
        "viewer" or "Viewer" => BadgeStyle.Light,
        _ => BadgeStyle.Light
    };

    /// <summary>
    /// Gets a tooltip string describing validation issues for a project.
    /// </summary>
    public static string GetValidationTooltip(ProjectDto project)
    {
        var parts = new List<string>();
        if (project.ValidationErrors > 0)
            parts.Add($"{project.ValidationErrors} error{(project.ValidationErrors != 1 ? "s" : "")}");
        if (project.ValidationWarnings > 0)
            parts.Add($"{project.ValidationWarnings} warning{(project.ValidationWarnings != 1 ? "s" : "")}");
        return string.Join(", ", parts);
    }
}
