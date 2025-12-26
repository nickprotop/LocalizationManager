using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Result of validating a project's resources.
/// </summary>
public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// Summary counts by category for quick display.
    /// </summary>
    public ValidationSummary Summary { get; set; } = new();

    /// <summary>
    /// When the validation was computed (for cache indicator).
    /// </summary>
    public DateTime? ValidatedAt { get; set; }

    /// <summary>
    /// Whether the result is from cache (vs freshly computed).
    /// </summary>
    public bool IsCached { get; set; }
}

/// <summary>
/// Summary of validation issues by category.
/// </summary>
public class ValidationSummary
{
    public int TotalIssues { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public int Info { get; set; }

    // Issue counts by category
    public int DuplicateKeys { get; set; }
    public int MissingTranslations { get; set; }
    public int EmptyValues { get; set; }
    public int PlaceholderMismatches { get; set; }
    public int ExtraKeys { get; set; }
    public int PendingReview { get; set; }
}

/// <summary>
/// A validation issue found in a project.
/// </summary>
public class ValidationIssue
{
    [Required]
    public string Severity { get; set; } = "warning";  // error, warning, info

    [Required]
    public required string Message { get; set; }

    /// <summary>
    /// Category of the issue for grouping/filtering.
    /// </summary>
    [Required]
    public string Category { get; set; } = ValidationCategory.Other;

    public string? KeyName { get; set; }
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Additional details (e.g., for placeholder mismatches).
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Validation issue categories.
/// </summary>
public static class ValidationCategory
{
    public const string Duplicate = "duplicate";
    public const string Missing = "missing";
    public const string Empty = "empty";
    public const string Placeholder = "placeholder";
    public const string Extra = "extra";
    public const string Pending = "pending";
    public const string Other = "other";
}
