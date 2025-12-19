using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Reviews;

namespace LrmCloud.Web.Services;

/// <summary>
/// HTTP client service for review workflow management.
/// Supports both project-level and organization-level reviewer management.
/// </summary>
public class ReviewWorkflowService
{
    private readonly HttpClient _http;
    private readonly ILogger<ReviewWorkflowService> _logger;

    public ReviewWorkflowService(HttpClient http, ILogger<ReviewWorkflowService> logger)
    {
        _http = http;
        _logger = logger;
    }

    #region Workflow Settings

    /// <summary>
    /// Get workflow settings for a project.
    /// </summary>
    public async Task<ReviewWorkflowSettingsDto?> GetWorkflowSettingsAsync(int projectId)
    {
        try
        {
            var response = await _http.GetAsync($"projects/{projectId}/workflow/settings");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewWorkflowSettingsDto>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get workflow settings for project {ProjectId}", projectId);
            return null;
        }
    }

    /// <summary>
    /// Update workflow settings for a project.
    /// </summary>
    public async Task<ReviewWorkflowSettingsDto?> UpdateWorkflowSettingsAsync(int projectId, UpdateWorkflowSettingsRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"projects/{projectId}/workflow/settings", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewWorkflowSettingsDto>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update workflow settings for project {ProjectId}", projectId);
            throw;
        }
    }

    #endregion

    #region Project Reviewers

    /// <summary>
    /// Get all reviewers for a project (includes inherited organization reviewers if enabled).
    /// </summary>
    public async Task<List<ReviewerDto>> GetProjectReviewersAsync(int projectId)
    {
        try
        {
            var response = await _http.GetAsync($"projects/{projectId}/workflow/reviewers");
            if (!response.IsSuccessStatusCode)
                return new();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReviewerDto>>>();
            return result?.Data ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get reviewers for project {ProjectId}", projectId);
            return new();
        }
    }

    /// <summary>
    /// Add a reviewer to a project.
    /// </summary>
    public async Task<ReviewerDto?> AddProjectReviewerAsync(int projectId, AddReviewerRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"projects/{projectId}/workflow/reviewers", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewerDto>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to add reviewer to project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Remove a reviewer from a project.
    /// </summary>
    public async Task<bool> RemoveProjectReviewerAsync(int projectId, int reviewerUserId)
    {
        try
        {
            var response = await _http.DeleteAsync($"projects/{projectId}/workflow/reviewers/{reviewerUserId}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to remove reviewer {UserId} from project {ProjectId}", reviewerUserId, projectId);
            return false;
        }
    }

    #endregion

    #region Organization Reviewers

    /// <summary>
    /// Get all reviewers for an organization.
    /// </summary>
    public async Task<List<ReviewerDto>> GetOrganizationReviewersAsync(int organizationId)
    {
        try
        {
            var response = await _http.GetAsync($"organizations/{organizationId}/reviewers");
            if (!response.IsSuccessStatusCode)
                return new();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReviewerDto>>>();
            return result?.Data ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get reviewers for organization {OrganizationId}", organizationId);
            return new();
        }
    }

    /// <summary>
    /// Add a reviewer to an organization.
    /// </summary>
    public async Task<ReviewerDto?> AddOrganizationReviewerAsync(int organizationId, AddReviewerRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"organizations/{organizationId}/reviewers", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewerDto>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to add reviewer to organization {OrganizationId}", organizationId);
            throw;
        }
    }

    /// <summary>
    /// Remove a reviewer from an organization.
    /// </summary>
    public async Task<bool> RemoveOrganizationReviewerAsync(int organizationId, int reviewerUserId)
    {
        try
        {
            var response = await _http.DeleteAsync($"organizations/{organizationId}/reviewers/{reviewerUserId}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to remove reviewer {UserId} from organization {OrganizationId}", reviewerUserId, organizationId);
            return false;
        }
    }

    #endregion

    #region Review Actions

    /// <summary>
    /// Bulk review translations (mark as reviewed).
    /// </summary>
    public async Task<BulkReviewResponse?> ReviewTranslationsAsync(int projectId, ReviewTranslationsRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"projects/{projectId}/workflow/review", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkReviewResponse>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to review translations for project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Bulk approve translations (mark as approved).
    /// </summary>
    public async Task<BulkReviewResponse?> ApproveTranslationsAsync(int projectId, ApproveTranslationsRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"projects/{projectId}/workflow/approve", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkReviewResponse>>();
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to approve translations for project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Reject a translation back to translated status.
    /// </summary>
    public async Task<bool> RejectTranslationAsync(int projectId, int translationId, RejectTranslationRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"projects/{projectId}/workflow/translations/{translationId}/reject", request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reject translation {TranslationId} for project {ProjectId}", translationId, projectId);
            return false;
        }
    }

    #endregion

    #region Authorization Checks

    /// <summary>
    /// Check if current user can review translations in this project.
    /// </summary>
    public async Task<bool> CanReviewAsync(int projectId, string? languageCode = null)
    {
        try
        {
            var url = $"projects/{projectId}/workflow/can-review";
            if (!string.IsNullOrEmpty(languageCode))
                url += $"?languageCode={Uri.EscapeDataString(languageCode)}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result?.Data ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check review permission for project {ProjectId}", projectId);
            return false;
        }
    }

    /// <summary>
    /// Check if current user can approve translations in this project.
    /// </summary>
    public async Task<bool> CanApproveAsync(int projectId, string? languageCode = null)
    {
        try
        {
            var url = $"projects/{projectId}/workflow/can-approve";
            if (!string.IsNullOrEmpty(languageCode))
                url += $"?languageCode={Uri.EscapeDataString(languageCode)}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result?.Data ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check approve permission for project {ProjectId}", projectId);
            return false;
        }
    }

    #endregion
}
