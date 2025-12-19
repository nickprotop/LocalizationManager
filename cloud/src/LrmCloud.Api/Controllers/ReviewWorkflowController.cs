using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for review workflow management.
/// </summary>
[Route("api/projects/{projectId}/workflow")]
[Authorize]
public class ReviewWorkflowController : ApiControllerBase
{
    private readonly ReviewWorkflowService _workflowService;
    private readonly ILogger<ReviewWorkflowController> _logger;

    public ReviewWorkflowController(
        ReviewWorkflowService workflowService,
        ILogger<ReviewWorkflowController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // ============================================================
    // Workflow Settings
    // ============================================================

    /// <summary>
    /// Gets workflow settings for a project.
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(ApiResponse<ReviewWorkflowSettingsDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ReviewWorkflowSettingsDto>>> GetSettings(int projectId)
    {
        var settings = await _workflowService.GetWorkflowSettingsAsync(projectId, GetUserId());
        if (settings == null)
            return NotFound("WORKFLOW_NOT_FOUND", "Project not found or access denied");

        return Success(settings);
    }

    /// <summary>
    /// Updates workflow settings for a project.
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(ApiResponse<ReviewWorkflowSettingsDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<ReviewWorkflowSettingsDto>>> UpdateSettings(
        int projectId, [FromBody] UpdateWorkflowSettingsRequest request)
    {
        var (success, settings, error) = await _workflowService.UpdateWorkflowSettingsAsync(projectId, GetUserId(), request);
        if (!success)
            return BadRequest("WORKFLOW_UPDATE_FAILED", error!);

        return Success(settings!);
    }

    // ============================================================
    // Reviewer Management
    // ============================================================

    /// <summary>
    /// Gets all reviewers for a project.
    /// </summary>
    [HttpGet("reviewers")]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewerDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ReviewerDto>>>> GetReviewers(int projectId)
    {
        var reviewers = await _workflowService.GetProjectReviewersAsync(projectId, GetUserId());
        return Success(reviewers);
    }

    /// <summary>
    /// Adds a reviewer to a project.
    /// </summary>
    [HttpPost("reviewers")]
    [ProducesResponseType(typeof(ApiResponse<ReviewerDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<ReviewerDto>>> AddReviewer(
        int projectId, [FromBody] AddReviewerRequest request)
    {
        var (success, reviewer, error) = await _workflowService.AddProjectReviewerAsync(projectId, GetUserId(), request);
        if (!success)
            return BadRequest("REVIEWER_ADD_FAILED", error!);

        return Created(nameof(GetReviewers), new { projectId }, reviewer!);
    }

    /// <summary>
    /// Removes a reviewer from a project.
    /// </summary>
    [HttpDelete("reviewers/{reviewerUserId}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> RemoveReviewer(int projectId, int reviewerUserId)
    {
        var (success, error) = await _workflowService.RemoveProjectReviewerAsync(projectId, GetUserId(), reviewerUserId);
        if (!success)
            return BadRequest("REVIEWER_REMOVE_FAILED", error!);

        return Success("Reviewer removed");
    }

    // ============================================================
    // Review Actions
    // ============================================================

    /// <summary>
    /// Bulk review translations (mark as reviewed).
    /// </summary>
    [HttpPost("review")]
    [ProducesResponseType(typeof(ApiResponse<BulkReviewResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<BulkReviewResponse>>> ReviewTranslations(
        int projectId, [FromBody] ReviewTranslationsRequest request)
    {
        var (success, response, error) = await _workflowService.ReviewTranslationsAsync(projectId, GetUserId(), request);
        if (!success)
            return BadRequest("REVIEW_FAILED", error!);

        return Success(response!);
    }

    /// <summary>
    /// Bulk approve translations (mark as approved).
    /// </summary>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(ApiResponse<BulkReviewResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<BulkReviewResponse>>> ApproveTranslations(
        int projectId, [FromBody] ApproveTranslationsRequest request)
    {
        var (success, response, error) = await _workflowService.ApproveTranslationsAsync(projectId, GetUserId(), request);
        if (!success)
            return BadRequest("APPROVE_FAILED", error!);

        return Success(response!);
    }

    /// <summary>
    /// Rejects a translation back to "translated" status.
    /// </summary>
    [HttpPost("translations/{translationId}/reject")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> RejectTranslation(
        int projectId, int translationId, [FromBody] RejectTranslationRequest request)
    {
        var (success, error) = await _workflowService.RejectTranslationAsync(projectId, translationId, GetUserId(), request);
        if (!success)
            return BadRequest("REJECT_FAILED", error!);

        return Success("Translation rejected");
    }

    // ============================================================
    // Authorization Check Endpoints
    // ============================================================

    /// <summary>
    /// Check if current user can review translations in this project.
    /// </summary>
    [HttpGet("can-review")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<ActionResult<ApiResponse<bool>>> CanReview(int projectId, [FromQuery] string? languageCode = null)
    {
        var canReview = await _workflowService.CanReviewAsync(projectId, GetUserId(), languageCode);
        return Success(canReview);
    }

    /// <summary>
    /// Check if current user can approve translations in this project.
    /// </summary>
    [HttpGet("can-approve")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<ActionResult<ApiResponse<bool>>> CanApprove(int projectId, [FromQuery] string? languageCode = null)
    {
        var canApprove = await _workflowService.CanApproveAsync(projectId, GetUserId(), languageCode);
        return Success(canApprove);
    }
}

/// <summary>
/// Organization reviewers endpoints.
/// </summary>
[Route("api/organizations/{organizationId}/reviewers")]
[Authorize]
public class OrganizationReviewersController : ApiControllerBase
{
    private readonly ReviewWorkflowService _workflowService;
    private readonly ILogger<OrganizationReviewersController> _logger;

    public OrganizationReviewersController(
        ReviewWorkflowService workflowService,
        ILogger<OrganizationReviewersController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Gets all reviewers for an organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewerDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ReviewerDto>>>> GetReviewers(int organizationId)
    {
        var reviewers = await _workflowService.GetOrganizationReviewersAsync(organizationId, GetUserId());
        return Success(reviewers);
    }

    /// <summary>
    /// Adds a reviewer to an organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReviewerDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<ReviewerDto>>> AddReviewer(
        int organizationId, [FromBody] AddReviewerRequest request)
    {
        var (success, reviewer, error) = await _workflowService.AddOrganizationReviewerAsync(organizationId, GetUserId(), request);
        if (!success)
            return BadRequest("REVIEWER_ADD_FAILED", error!);

        return Created(nameof(GetReviewers), new { organizationId }, reviewer!);
    }

    /// <summary>
    /// Removes a reviewer from an organization.
    /// </summary>
    [HttpDelete("{reviewerUserId}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> RemoveReviewer(int organizationId, int reviewerUserId)
    {
        var (success, error) = await _workflowService.RemoveOrganizationReviewerAsync(organizationId, GetUserId(), reviewerUserId);
        if (!success)
            return BadRequest("REVIEWER_REMOVE_FAILED", error!);

        return Success("Reviewer removed");
    }
}
