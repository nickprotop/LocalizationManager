using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Reviews;

/// <summary>
/// Request to add a reviewer to a project or organization.
/// </summary>
public class AddReviewerRequest
{
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Role: "reviewer" or "approver".
    /// </summary>
    [Required]
    [RegularExpression("^(reviewer|approver)$", ErrorMessage = "Role must be 'reviewer' or 'approver'")]
    public required string Role { get; set; }

    /// <summary>
    /// Optional: Restrict to specific languages (comma-separated codes).
    /// </summary>
    public string[]? LanguageCodes { get; set; }
}

/// <summary>
/// Request to update workflow settings.
/// </summary>
public class UpdateWorkflowSettingsRequest
{
    public bool? ReviewWorkflowEnabled { get; set; }
    public bool? RequireReviewBeforeExport { get; set; }
    public bool? RequireApprovalBeforeExport { get; set; }
    public bool? InheritOrganizationReviewers { get; set; }
}

/// <summary>
/// Request to bulk review translations.
/// </summary>
public class ReviewTranslationsRequest
{
    /// <summary>
    /// List of translation IDs to review.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one translation ID is required")]
    public required List<int> TranslationIds { get; set; }

    /// <summary>
    /// Optional comment for the review.
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

/// <summary>
/// Request to bulk approve translations.
/// </summary>
public class ApproveTranslationsRequest
{
    /// <summary>
    /// List of translation IDs to approve.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one translation ID is required")]
    public required List<int> TranslationIds { get; set; }

    /// <summary>
    /// Optional comment for the approval.
    /// </summary>
    [MaxLength(500)]
    public string? Comment { get; set; }
}

/// <summary>
/// Request to reject a translation back to translated status.
/// </summary>
public class RejectTranslationRequest
{
    /// <summary>
    /// Reason for rejection (required).
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Comment { get; set; }
}
