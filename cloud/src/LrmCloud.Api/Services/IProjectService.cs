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
    /// Gets all projects accessible by the user (personal + organization).
    /// </summary>
    Task<List<ProjectDto>> GetUserProjectsAsync(int userId);

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
}
