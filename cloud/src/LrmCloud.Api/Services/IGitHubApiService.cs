using Octokit;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for interacting with the GitHub API using user OAuth tokens.
/// </summary>
public interface IGitHubApiService
{
    /// <summary>
    /// List repositories the user has access to.
    /// </summary>
    Task<IReadOnlyList<Repository>> ListUserRepositoriesAsync(int userId);

    /// <summary>
    /// List branches for a repository.
    /// </summary>
    Task<IReadOnlyList<Branch>> ListBranchesAsync(int userId, string owner, string repo);

    /// <summary>
    /// Get contents of a directory in a repository.
    /// </summary>
    Task<IReadOnlyList<RepositoryContent>> GetDirectoryContentsAsync(int userId, string owner, string repo, string path, string branch);

    /// <summary>
    /// Get file content from a repository.
    /// </summary>
    Task<string?> GetFileContentAsync(int userId, string owner, string repo, string path, string branch);

    /// <summary>
    /// Create a new branch from an existing branch.
    /// </summary>
    Task<Reference> CreateBranchAsync(int userId, string owner, string repo, string branchName, string fromBranch);

    /// <summary>
    /// Create or update a file in a repository.
    /// </summary>
    Task CreateOrUpdateFileAsync(int userId, string owner, string repo, string path, string content, string message, string branch, string? sha = null);

    /// <summary>
    /// Create a pull request.
    /// </summary>
    Task<PullRequest> CreatePullRequestAsync(int userId, string owner, string repo, string title, string head, string baseBranch, string body);

    /// <summary>
    /// Validate that the user's GitHub token is valid and has the required scopes.
    /// </summary>
    Task<(bool Valid, string? ErrorMessage, IReadOnlyList<string>? Scopes)> ValidateTokenAsync(int userId);

    /// <summary>
    /// Get a repository by owner and name.
    /// </summary>
    Task<Repository?> GetRepositoryAsync(int userId, string owner, string repo);

    /// <summary>
    /// Detect localization files in a directory (auto-detection for setup).
    /// </summary>
    Task<List<DetectedLocalizationPath>> DetectLocalizationFilesAsync(int userId, string owner, string repo, string branch, string basePath = "");
}

/// <summary>
/// Represents a detected localization path with its format.
/// </summary>
public record DetectedLocalizationPath(
    string Path,
    string Format,
    int FileCount,
    bool HasLrmConfig
);
