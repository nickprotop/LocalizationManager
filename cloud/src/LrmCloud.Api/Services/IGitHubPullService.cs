using LrmCloud.Shared.DTOs.GitHub;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for pulling translations from GitHub to Cloud.
/// Implements three-way merge with conflict detection.
/// </summary>
public interface IGitHubPullService
{
    /// <summary>
    /// Preview changes that would be pulled from GitHub.
    /// Does not apply any changes.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID performing the preview</param>
    /// <returns>Pull result showing what would change</returns>
    Task<GitHubPullResult> PreviewPullAsync(int projectId, int userId);

    /// <summary>
    /// Pull translations from GitHub to Cloud.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID performing the pull</param>
    /// <param name="strategy">Conflict resolution strategy: "prompt" (default), "github" (accept all remote), "cloud" (keep all local)</param>
    /// <returns>Pull result with any conflicts</returns>
    Task<GitHubPullResult> PullFromGitHubAsync(
        int projectId,
        int userId,
        string strategy = "prompt");

    /// <summary>
    /// Resolve conflicts from a previous pull.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="userId">User ID performing the resolution</param>
    /// <param name="request">Conflict resolutions</param>
    /// <returns>Result after applying resolutions</returns>
    Task<GitHubPullResult> ResolveConflictsAsync(
        int projectId,
        int userId,
        GitHubPullConflictResolutionRequest request);

    /// <summary>
    /// Get pending conflicts for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Summary of pending conflicts</returns>
    Task<GitHubPullConflictSummary> GetPendingConflictsAsync(int projectId);
}
