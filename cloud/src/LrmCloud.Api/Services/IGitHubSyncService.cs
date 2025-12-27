using LrmCloud.Shared.DTOs.GitHub;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for syncing project translations to GitHub repositories.
/// Creates branches, commits files, and opens Pull Requests.
/// </summary>
public interface IGitHubSyncService
{
    /// <summary>
    /// Synchronize a project's translations to its connected GitHub repository.
    /// Creates a branch, pushes translation files, and opens a Pull Request.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID performing the sync</param>
    /// <param name="commitMessage">Optional custom commit message</param>
    /// <param name="createPr">Whether to create a Pull Request (default: true)</param>
    /// <returns>Sync result with PR URL if successful</returns>
    Task<GitHubSyncResult> SyncToGitHubAsync(
        int projectId,
        int userId,
        string? commitMessage = null,
        bool createPr = true);

    /// <summary>
    /// Get the current GitHub sync status for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID checking the status</param>
    /// <returns>Current sync status</returns>
    Task<GitHubSyncStatus> GetSyncStatusAsync(int projectId, int userId);

    /// <summary>
    /// Validate that a project is properly configured for GitHub sync.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID to check token access</param>
    /// <returns>Validation result with any error message</returns>
    Task<(bool Valid, string? ErrorMessage)> ValidateSyncConfigurationAsync(int projectId, int userId);
}
