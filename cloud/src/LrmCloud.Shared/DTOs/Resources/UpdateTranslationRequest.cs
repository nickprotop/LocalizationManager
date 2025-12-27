using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Request to update a translation.
/// </summary>
public class UpdateTranslationRequest
{
    [Required(ErrorMessage = "Value is required")]
    public required string Value { get; set; }

    public string PluralForm { get; set; } = "";

    [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters")]
    public string Status { get; set; } = "translated";

    /// <summary>
    /// Current version for optimistic locking.
    /// If provided and doesn't match, update will fail.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// Translation-specific comment (per-language note).
    /// </summary>
    [MaxLength(1000, ErrorMessage = "Comment must not exceed 1000 characters")]
    public string? Comment { get; set; }
}
