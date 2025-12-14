using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Projects;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing localization projects.
/// </summary>
public interface IProjectService
{
    // ============================================================
    // Project CRUD
    // ============================================================

    /// <summary>
    /// Creates a new project (personal or organization).
    /// </summary>
    Task<(bool Success, ProjectDto? Project, string? ErrorMessage)> CreateProjectAsync(
        int userId, CreateProjectRequest request);

    /// <summary>
    /// Gets a specific project if user has access.
    /// </summary>
    Task<ProjectDto?> GetProjectAsync(int projectId, int userId);

    /// <summary>
    /// Gets a project by username and project name.
    /// </summary>
    Task<ProjectDto?> GetProjectByNameAsync(string username, string projectName, int userId);

    /// <summary>
    /// Gets all projects accessible by the user (personal + organization).
    /// </summary>
    Task<List<ProjectDto>> GetUserProjectsAsync(int userId);

    /// <summary>
    /// Gets projects accessible by the user with pagination.
    /// </summary>
    Task<PagedResult<ProjectDto>> GetUserProjectsPagedAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        int? organizationId = null,
        string? sortBy = null,
        bool sortDescending = false);

    /// <summary>
    /// Updates a project (requires appropriate permissions).
    /// </summary>
    Task<(bool Success, ProjectDto? Project, string? ErrorMessage)> UpdateProjectAsync(
        int projectId, int userId, UpdateProjectRequest request);

    /// <summary>
    /// Deletes a project (requires appropriate permissions).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeleteProjectAsync(int projectId, int userId);

    /// <summary>
    /// Triggers a sync from GitHub for a project.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TriggerSyncAsync(int projectId, int userId);

    // ============================================================
    // Authorization Helpers
    // ============================================================

    /// <summary>
    /// Checks if user can view a project.
    /// </summary>
    Task<bool> CanViewProjectAsync(int projectId, int userId);

    /// <summary>
    /// Checks if user can edit a project (admin+ for org, owner for personal).
    /// </summary>
    Task<bool> CanEditProjectAsync(int projectId, int userId);

    /// <summary>
    /// Checks if user can manage resources (member+ for org, owner for personal).
    /// </summary>
    Task<bool> CanManageResourcesAsync(int projectId, int userId);

    // ============================================================
    // Configuration Management
    // ============================================================

    /// <summary>
    /// Gets the project configuration (lrm.json).
    /// </summary>
    Task<ConfigurationDto?> GetConfigurationAsync(int projectId, int userId);

    /// <summary>
    /// Updates the project configuration (lrm.json) with optimistic locking.
    /// </summary>
    Task<(bool Success, ConfigurationDto? Configuration, string? ErrorMessage)> UpdateConfigurationAsync(
        int projectId, int userId, UpdateConfigurationRequest request);

    /// <summary>
    /// Gets the configuration history for a project.
    /// </summary>
    Task<List<ConfigurationHistoryDto>?> GetConfigurationHistoryAsync(int projectId, int userId, int limit);

    /// <summary>
    /// Gets the sync status for a project.
    /// </summary>
    Task<SyncStatusDto?> GetSyncStatusAsync(int projectId, int userId);
}
