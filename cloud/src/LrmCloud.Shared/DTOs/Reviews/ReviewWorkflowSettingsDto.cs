namespace LrmCloud.Shared.DTOs.Reviews;

/// <summary>
/// Project workflow settings and reviewers.
/// </summary>
public class ReviewWorkflowSettingsDto
{
    /// <summary>
    /// Whether review workflow is enabled for this project.
    /// </summary>
    public bool ReviewWorkflowEnabled { get; set; }

    /// <summary>
    /// Whether review is required before export/sync.
    /// </summary>
    public bool RequireReviewBeforeExport { get; set; }

    /// <summary>
    /// Whether approval is required before export/sync.
    /// </summary>
    public bool RequireApprovalBeforeExport { get; set; }

    /// <summary>
    /// Whether to inherit reviewers from organization.
    /// </summary>
    public bool InheritOrganizationReviewers { get; set; } = true;

    /// <summary>
    /// Project-specific reviewers.
    /// </summary>
    public List<ReviewerDto> Reviewers { get; set; } = new();

    /// <summary>
    /// Inherited organization reviewers.
    /// </summary>
    public List<ReviewerDto> InheritedReviewers { get; set; } = new();

    /// <summary>
    /// Translation statistics by status.
    /// </summary>
    public TranslationStatusStats? Stats { get; set; }
}

/// <summary>
/// Translation counts by status.
/// </summary>
public class TranslationStatusStats
{
    public int PendingCount { get; set; }
    public int TranslatedCount { get; set; }
    public int ReviewedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int TotalCount { get; set; }
}
