using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Reviews;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing review workflow, reviewers, and translation approvals.
/// </summary>
public class ReviewWorkflowService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReviewWorkflowService> _logger;

    public ReviewWorkflowService(AppDbContext db, ILogger<ReviewWorkflowService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Workflow Settings

    /// <summary>
    /// Get workflow settings for a project.
    /// </summary>
    public async Task<ReviewWorkflowSettingsDto?> GetWorkflowSettingsAsync(int projectId, int userId)
    {
        var project = await _db.Projects
            .Include(p => p.Reviewers)
                .ThenInclude(r => r.User)
            .Include(p => p.Organization)
                .ThenInclude(o => o!.Reviewers)
                    .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return null;

        // Check access
        if (!await HasProjectAccessAsync(projectId, userId))
            return null;

        var settings = new ReviewWorkflowSettingsDto
        {
            ReviewWorkflowEnabled = project.ReviewWorkflowEnabled,
            RequireReviewBeforeExport = project.RequireReviewBeforeExport,
            RequireApprovalBeforeExport = project.RequireApprovalBeforeExport,
            InheritOrganizationReviewers = project.InheritOrganizationReviewers,
            Reviewers = project.Reviewers.Select(r => MapToReviewerDto(r, false)).ToList()
        };

        // Add inherited org reviewers if enabled
        if (project.InheritOrganizationReviewers && project.Organization != null)
        {
            settings.InheritedReviewers = project.Organization.Reviewers
                .Select(r => MapToReviewerDto(r, true))
                .ToList();
        }

        // Get translation stats
        settings.Stats = await GetTranslationStatsAsync(projectId);

        return settings;
    }

    /// <summary>
    /// Update workflow settings for a project.
    /// </summary>
    public async Task<(bool Success, ReviewWorkflowSettingsDto? Settings, string? Error)> UpdateWorkflowSettingsAsync(
        int projectId, int userId, UpdateWorkflowSettingsRequest request)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return (false, null, "Project not found");

        // Check if user is owner or admin
        if (!await IsProjectOwnerOrAdminAsync(projectId, userId))
            return (false, null, "Only project owner or organization admin can update workflow settings");

        if (request.ReviewWorkflowEnabled.HasValue)
            project.ReviewWorkflowEnabled = request.ReviewWorkflowEnabled.Value;
        if (request.RequireReviewBeforeExport.HasValue)
            project.RequireReviewBeforeExport = request.RequireReviewBeforeExport.Value;
        if (request.RequireApprovalBeforeExport.HasValue)
            project.RequireApprovalBeforeExport = request.RequireApprovalBeforeExport.Value;
        if (request.InheritOrganizationReviewers.HasValue)
            project.InheritOrganizationReviewers = request.InheritOrganizationReviewers.Value;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Updated workflow settings for project {ProjectId} by user {UserId}", projectId, userId);

        var settings = await GetWorkflowSettingsAsync(projectId, userId);
        return (true, settings, null);
    }

    private async Task<TranslationStatusStats> GetTranslationStatsAsync(int projectId)
    {
        var stats = await _db.Translations
            .Where(t => t.ResourceKey!.ProjectId == projectId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return new TranslationStatusStats
        {
            PendingCount = stats.FirstOrDefault(s => s.Status == "pending")?.Count ?? 0,
            TranslatedCount = stats.FirstOrDefault(s => s.Status == "translated")?.Count ?? 0,
            ReviewedCount = stats.FirstOrDefault(s => s.Status == "reviewed")?.Count ?? 0,
            ApprovedCount = stats.FirstOrDefault(s => s.Status == "approved")?.Count ?? 0,
            TotalCount = stats.Sum(s => s.Count)
        };
    }

    #endregion

    #region Reviewer Management

    /// <summary>
    /// Get all reviewers for a project.
    /// </summary>
    public async Task<List<ReviewerDto>> GetProjectReviewersAsync(int projectId, int userId)
    {
        if (!await HasProjectAccessAsync(projectId, userId))
            return new List<ReviewerDto>();

        var project = await _db.Projects
            .Include(p => p.Reviewers)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return new List<ReviewerDto>();

        return project.Reviewers.Select(r => MapToReviewerDto(r, false)).ToList();
    }

    /// <summary>
    /// Add a reviewer to a project.
    /// </summary>
    public async Task<(bool Success, ReviewerDto? Reviewer, string? Error)> AddProjectReviewerAsync(
        int projectId, int userId, AddReviewerRequest request)
    {
        if (!await IsProjectOwnerOrAdminAsync(projectId, userId))
            return (false, null, "Only project owner or organization admin can add reviewers");

        // Check user exists
        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null)
            return (false, null, "User not found");

        // Check not already a reviewer
        var existing = await _db.ProjectReviewers
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == request.UserId);
        if (existing != null)
            return (false, null, "User is already a reviewer for this project");

        var reviewer = new ProjectReviewer
        {
            ProjectId = projectId,
            UserId = request.UserId,
            Role = request.Role,
            LanguageCodes = request.LanguageCodes != null ? string.Join(",", request.LanguageCodes) : null,
            AddedById = userId
        };

        _db.ProjectReviewers.Add(reviewer);
        await _db.SaveChangesAsync();

        // Reload with user
        await _db.Entry(reviewer).Reference(r => r.User).LoadAsync();

        _logger.LogInformation("Added reviewer {ReviewerUserId} to project {ProjectId} by user {UserId}",
            request.UserId, projectId, userId);

        return (true, MapToReviewerDto(reviewer, false), null);
    }

    /// <summary>
    /// Remove a reviewer from a project.
    /// </summary>
    public async Task<(bool Success, string? Error)> RemoveProjectReviewerAsync(
        int projectId, int userId, int reviewerUserId)
    {
        if (!await IsProjectOwnerOrAdminAsync(projectId, userId))
            return (false, "Only project owner or organization admin can remove reviewers");

        var reviewer = await _db.ProjectReviewers
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == reviewerUserId);

        if (reviewer == null)
            return (false, "Reviewer not found");

        _db.ProjectReviewers.Remove(reviewer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Removed reviewer {ReviewerUserId} from project {ProjectId} by user {UserId}",
            reviewerUserId, projectId, userId);

        return (true, null);
    }

    /// <summary>
    /// Get all reviewers for an organization.
    /// </summary>
    public async Task<List<ReviewerDto>> GetOrganizationReviewersAsync(int organizationId, int userId)
    {
        if (!await IsOrganizationMemberAsync(organizationId, userId))
            return new List<ReviewerDto>();

        var reviewers = await _db.OrganizationReviewers
            .Include(r => r.User)
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync();

        return reviewers.Select(r => MapToReviewerDto(r, false)).ToList();
    }

    /// <summary>
    /// Add a reviewer to an organization.
    /// </summary>
    public async Task<(bool Success, ReviewerDto? Reviewer, string? Error)> AddOrganizationReviewerAsync(
        int organizationId, int userId, AddReviewerRequest request)
    {
        if (!await IsOrganizationAdminAsync(organizationId, userId))
            return (false, null, "Only organization owner or admin can add reviewers");

        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null)
            return (false, null, "User not found");

        var existing = await _db.OrganizationReviewers
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == request.UserId);
        if (existing != null)
            return (false, null, "User is already a reviewer for this organization");

        var reviewer = new OrganizationReviewer
        {
            OrganizationId = organizationId,
            UserId = request.UserId,
            Role = request.Role,
            AddedById = userId
        };

        _db.OrganizationReviewers.Add(reviewer);
        await _db.SaveChangesAsync();

        await _db.Entry(reviewer).Reference(r => r.User).LoadAsync();

        _logger.LogInformation("Added reviewer {ReviewerUserId} to organization {OrganizationId} by user {UserId}",
            request.UserId, organizationId, userId);

        return (true, MapToReviewerDto(reviewer, false), null);
    }

    /// <summary>
    /// Remove a reviewer from an organization.
    /// </summary>
    public async Task<(bool Success, string? Error)> RemoveOrganizationReviewerAsync(
        int organizationId, int userId, int reviewerUserId)
    {
        if (!await IsOrganizationAdminAsync(organizationId, userId))
            return (false, "Only organization owner or admin can remove reviewers");

        var reviewer = await _db.OrganizationReviewers
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == reviewerUserId);

        if (reviewer == null)
            return (false, "Reviewer not found");

        _db.OrganizationReviewers.Remove(reviewer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Removed reviewer {ReviewerUserId} from organization {OrganizationId} by user {UserId}",
            reviewerUserId, organizationId, userId);

        return (true, null);
    }

    #endregion

    #region Review Actions

    /// <summary>
    /// Bulk review translations (mark as "reviewed").
    /// </summary>
    public async Task<(bool Success, BulkReviewResponse? Response, string? Error)> ReviewTranslationsAsync(
        int projectId, int userId, ReviewTranslationsRequest request)
    {
        // Check user can review
        if (!await CanReviewAsync(projectId, userId))
            return (false, null, "You don't have permission to review translations for this project");

        var translations = await _db.Translations
            .Include(t => t.ResourceKey)
            .Where(t => request.TranslationIds.Contains(t.Id) && t.ResourceKey!.ProjectId == projectId)
            .ToListAsync();

        var response = new BulkReviewResponse();

        foreach (var translation in translations)
        {
            // Can only review "translated" translations
            if (translation.Status != "translated")
            {
                response.SkippedCount++;
                response.SkippedIds.Add(translation.Id);
                continue;
            }

            translation.Status = "reviewed";
            translation.ReviewedById = userId;
            translation.ReviewedAt = DateTime.UtcNow;
            translation.RejectionComment = null; // Clear any previous rejection
            response.ProcessedCount++;
        }

        if (response.SkippedCount > 0)
            response.SkipReason = "Some translations were not in 'translated' status";

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} reviewed {Count} translations for project {ProjectId}",
            userId, response.ProcessedCount, projectId);

        return (true, response, null);
    }

    /// <summary>
    /// Bulk approve translations (mark as "approved").
    /// </summary>
    public async Task<(bool Success, BulkReviewResponse? Response, string? Error)> ApproveTranslationsAsync(
        int projectId, int userId, ApproveTranslationsRequest request)
    {
        // Check user can approve
        if (!await CanApproveAsync(projectId, userId))
            return (false, null, "You don't have permission to approve translations for this project");

        var translations = await _db.Translations
            .Include(t => t.ResourceKey)
            .Where(t => request.TranslationIds.Contains(t.Id) && t.ResourceKey!.ProjectId == projectId)
            .ToListAsync();

        var response = new BulkReviewResponse();

        foreach (var translation in translations)
        {
            // Can only approve "reviewed" translations
            if (translation.Status != "reviewed")
            {
                response.SkippedCount++;
                response.SkippedIds.Add(translation.Id);
                continue;
            }

            translation.Status = "approved";
            translation.ApprovedById = userId;
            translation.ApprovedAt = DateTime.UtcNow;
            response.ProcessedCount++;
        }

        if (response.SkippedCount > 0)
            response.SkipReason = "Some translations were not in 'reviewed' status";

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} approved {Count} translations for project {ProjectId}",
            userId, response.ProcessedCount, projectId);

        return (true, response, null);
    }

    /// <summary>
    /// Reject a translation back to "translated" status.
    /// </summary>
    public async Task<(bool Success, string? Error)> RejectTranslationAsync(
        int projectId, int translationId, int userId, RejectTranslationRequest request)
    {
        var translation = await _db.Translations
            .Include(t => t.ResourceKey)
            .FirstOrDefaultAsync(t => t.Id == translationId && t.ResourceKey!.ProjectId == projectId);

        if (translation == null)
            return (false, "Translation not found");

        // Check user can reject (reviewers can reject reviewed, approvers can reject approved)
        var canReview = await CanReviewAsync(projectId, userId);
        var canApprove = await CanApproveAsync(projectId, userId);

        if (translation.Status == "reviewed" && !canReview)
            return (false, "Only reviewers can reject reviewed translations");

        if (translation.Status == "approved" && !canApprove)
            return (false, "Only approvers can reject approved translations");

        if (translation.Status != "reviewed" && translation.Status != "approved")
            return (false, "Can only reject reviewed or approved translations");

        translation.Status = "translated";
        translation.RejectionComment = request.Comment;
        // Don't clear reviewed/approved by - keep history

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} rejected translation {TranslationId} for project {ProjectId}: {Comment}",
            userId, translationId, projectId, request.Comment);

        return (true, null);
    }

    #endregion

    #region Authorization Helpers

    /// <summary>
    /// Check if user can review translations in this project.
    /// </summary>
    public async Task<bool> CanReviewAsync(int projectId, int userId, string? languageCode = null)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Project owner can always review
        if (project.UserId == userId)
            return true;

        // Org owner/admin can always review
        if (project.OrganizationId.HasValue && await IsOrganizationAdminAsync(project.OrganizationId.Value, userId))
            return true;

        // Check project reviewers
        var projectReviewer = await _db.ProjectReviewers
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == userId);

        if (projectReviewer != null && ReviewerRole.CanReview(projectReviewer.Role))
        {
            // Check language restriction
            if (languageCode != null && projectReviewer.LanguageCodes != null)
            {
                var allowedLangs = projectReviewer.LanguageCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!allowedLangs.Contains(languageCode, StringComparer.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        // Check inherited org reviewers
        if (project.InheritOrganizationReviewers && project.OrganizationId.HasValue)
        {
            var orgReviewer = await _db.OrganizationReviewers
                .FirstOrDefaultAsync(r => r.OrganizationId == project.OrganizationId && r.UserId == userId);

            if (orgReviewer != null && ReviewerRole.CanReview(orgReviewer.Role))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if user can approve translations in this project.
    /// </summary>
    public async Task<bool> CanApproveAsync(int projectId, int userId, string? languageCode = null)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Project owner can always approve
        if (project.UserId == userId)
            return true;

        // Org owner/admin can always approve
        if (project.OrganizationId.HasValue && await IsOrganizationAdminAsync(project.OrganizationId.Value, userId))
            return true;

        // Check project approvers
        var projectReviewer = await _db.ProjectReviewers
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserId == userId);

        if (projectReviewer != null && ReviewerRole.CanApprove(projectReviewer.Role))
        {
            if (languageCode != null && projectReviewer.LanguageCodes != null)
            {
                var allowedLangs = projectReviewer.LanguageCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!allowedLangs.Contains(languageCode, StringComparer.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        // Check inherited org approvers
        if (project.InheritOrganizationReviewers && project.OrganizationId.HasValue)
        {
            var orgReviewer = await _db.OrganizationReviewers
                .FirstOrDefaultAsync(r => r.OrganizationId == project.OrganizationId && r.UserId == userId);

            if (orgReviewer != null && ReviewerRole.CanApprove(orgReviewer.Role))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validate a status transition.
    /// </summary>
    public (bool IsValid, string? Error) ValidateStatusTransition(string currentStatus, string newStatus)
    {
        // Valid transitions:
        // pending -> translated (any editor)
        // translated -> reviewed (reviewers only)
        // reviewed -> approved (approvers only)
        // reviewed -> translated (reject)
        // approved -> translated (reject)

        var validTransitions = new Dictionary<string, string[]>
        {
            { "pending", new[] { "translated" } },
            { "translated", new[] { "reviewed" } },
            { "reviewed", new[] { "approved", "translated" } },
            { "approved", new[] { "translated" } }
        };

        if (!validTransitions.TryGetValue(currentStatus, out var allowedTargets))
            return (false, $"Unknown status: {currentStatus}");

        if (!allowedTargets.Contains(newStatus))
            return (false, $"Cannot transition from '{currentStatus}' to '{newStatus}'");

        return (true, null);
    }

    #endregion

    #region Private Helpers

    private async Task<bool> HasProjectAccessAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Personal project owner
        if (project.UserId == userId)
            return true;

        // Org member
        if (project.OrganizationId.HasValue)
        {
            return await _db.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == project.OrganizationId && m.UserId == userId);
        }

        return false;
    }

    private async Task<bool> IsProjectOwnerOrAdminAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Personal project owner
        if (project.UserId == userId)
            return true;

        // Org owner or admin
        if (project.OrganizationId.HasValue)
        {
            return await IsOrganizationAdminAsync(project.OrganizationId.Value, userId);
        }

        return false;
    }

    private async Task<bool> IsOrganizationMemberAsync(int organizationId, int userId)
    {
        var org = await _db.Organizations.FindAsync(organizationId);
        if (org == null)
            return false;

        if (org.OwnerId == userId)
            return true;

        return await _db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
    }

    private async Task<bool> IsOrganizationAdminAsync(int organizationId, int userId)
    {
        var org = await _db.Organizations.FindAsync(organizationId);
        if (org == null)
            return false;

        if (org.OwnerId == userId)
            return true;

        return await _db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId && m.Role == "admin");
    }

    private static ReviewerDto MapToReviewerDto(ProjectReviewer reviewer, bool isInherited)
    {
        return new ReviewerDto
        {
            Id = reviewer.Id,
            UserId = reviewer.UserId,
            Username = reviewer.User?.Username ?? "",
            DisplayName = reviewer.User?.DisplayName,
            AvatarUrl = reviewer.User?.AvatarUrl,
            Role = reviewer.Role,
            LanguageCodes = reviewer.LanguageCodes?.Split(',', StringSplitOptions.RemoveEmptyEntries),
            IsInherited = isInherited,
            CreatedAt = reviewer.CreatedAt
        };
    }

    private static ReviewerDto MapToReviewerDto(OrganizationReviewer reviewer, bool isInherited)
    {
        return new ReviewerDto
        {
            Id = reviewer.Id,
            UserId = reviewer.UserId,
            Username = reviewer.User?.Username ?? "",
            DisplayName = reviewer.User?.DisplayName,
            AvatarUrl = reviewer.User?.AvatarUrl,
            Role = reviewer.Role,
            LanguageCodes = null, // Org reviewers apply to all languages
            IsInherited = isInherited,
            CreatedAt = reviewer.CreatedAt
        };
    }

    #endregion
}
