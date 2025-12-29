using LrmCloud.Shared.Entities;

namespace LrmCloud.Api.Services;

/// <summary>
/// Resolves the localization format for GitHub operations.
/// Priority: GitHubFormat explicit > lrm.json in repo > auto-detect from files.
/// </summary>
public interface IGitHubFormatResolver
{
    /// <summary>
    /// Resolves the format to use for GitHub file operations.
    /// </summary>
    /// <param name="project">The project entity</param>
    /// <param name="userId">User ID for GitHub API access</param>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="branch">Branch name</param>
    /// <param name="basePath">Base path for localization files</param>
    /// <returns>Resolved format string (e.g., "json", "resx", "android")</returns>
    Task<string> ResolveFormatAsync(
        Project project,
        int userId,
        string owner,
        string repo,
        string branch,
        string basePath);
}
