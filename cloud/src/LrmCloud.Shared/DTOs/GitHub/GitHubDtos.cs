namespace LrmCloud.Shared.DTOs.GitHub;

/// <summary>
/// Repository information from GitHub.
/// </summary>
public record GitHubRepoDto(
    long Id,
    string FullName,
    string DefaultBranch,
    bool Private,
    string? Description
);

/// <summary>
/// Branch information from GitHub.
/// </summary>
public record GitHubBranchDto(
    string Name,
    string CommitSha
);

/// <summary>
/// File/directory information from GitHub.
/// </summary>
public record GitHubFileDto(
    string Name,
    string Path,
    string Type, // "file" or "dir"
    long Size
);

/// <summary>
/// Request to connect a GitHub repository to a project.
/// </summary>
public record ConnectGitHubRequest(
    string RepoFullName,
    string DefaultBranch,
    string? BasePath
);

/// <summary>
/// Request to connect a GitHub repository using Personal Access Token.
/// </summary>
public record ConnectGitHubPatRequest(
    string RepoFullName,
    string DefaultBranch,
    string? BasePath,
    string PersonalAccessToken
);

/// <summary>
/// Result of a GitHub sync operation.
/// </summary>
public record GitHubSyncResult(
    bool Success,
    string? PullRequestUrl,
    int FilesUpdated,
    string? ErrorMessage
);

/// <summary>
/// Current GitHub connection status for a project.
/// </summary>
public record GitHubSyncStatus(
    bool Connected,
    string? RepoFullName,
    string? BasePath,
    DateTime? LastSyncedAt,
    string? LastPrUrl,
    string? SyncStatus,
    string? ConnectionMethod, // "oauth", "pat", "organization"
    string? ConnectedByUsername,
    string? TokenSource = null,     // "user", "organization", "project_pat"
    string? TokenSourceName = null  // "@username" or "Org Name" or "PAT"
);

/// <summary>
/// Detected localization path with format information.
/// </summary>
public record DetectedLocalizationPathDto(
    string Path,
    string Format,
    int FileCount,
    bool HasLrmConfig
);

/// <summary>
/// Result of auto-detecting localization files in a repository.
/// </summary>
public record DetectLocalizationResult(
    List<DetectedLocalizationPathDto> DetectedPaths,
    string? RecommendedPath,
    string? RecommendedFormat,
    bool HasLrmConfig
);

/// <summary>
/// Trigger a sync to GitHub.
/// </summary>
public record TriggerSyncRequest(
    string? CommitMessage,
    bool CreatePr = true
);

/// <summary>
/// GitHub token validation result.
/// </summary>
public record GitHubTokenStatus(
    bool Valid,
    bool HasRepoScope,
    string? GitHubLogin,
    string? ErrorMessage
);

// ============================================================================
// GitHub Pull DTOs (for pulling from GitHub to Cloud)
// ============================================================================

/// <summary>
/// Request to pull translations from GitHub.
/// </summary>
/// <param name="PreviewOnly">If true, only preview changes without applying</param>
/// <param name="Strategy">Conflict resolution strategy: "prompt" (default), "github" (accept all remote), "cloud" (keep all local)</param>
public record GitHubPullRequest(
    bool PreviewOnly = false,
    string Strategy = "prompt"
);

/// <summary>
/// Result of a GitHub pull operation.
/// </summary>
public record GitHubPullResult(
    bool Success,
    string? ErrorMessage,
    int EntriesApplied,
    int EntriesAdded,
    int EntriesDeleted,
    int EntriesUnchanged,
    List<GitHubPullConflict> Conflicts,
    string? CommitSha,
    List<string> ProcessedFiles
);

/// <summary>
/// A conflict detected during GitHub pull.
/// </summary>
/// <param name="Key">Resource key name</param>
/// <param name="LanguageCode">Language code</param>
/// <param name="PluralForm">Plural form (empty for non-plural)</param>
/// <param name="ConflictType">Type of conflict: "BothModified", "DeletedInGitHub", "DeletedInCloud"</param>
/// <param name="GitHubValue">Value from GitHub</param>
/// <param name="CloudValue">Current value in Cloud</param>
/// <param name="BaseValue">Last synced value (base for three-way merge)</param>
/// <param name="CloudModifiedAt">When Cloud value was last modified</param>
/// <param name="CloudModifiedBy">Who modified the Cloud value</param>
public record GitHubPullConflict(
    string Key,
    string LanguageCode,
    string PluralForm,
    string ConflictType,
    string? GitHubValue,
    string? CloudValue,
    string? BaseValue,
    DateTime? CloudModifiedAt,
    string? CloudModifiedBy
);

/// <summary>
/// Resolution for a single conflict.
/// </summary>
/// <param name="Key">Resource key name</param>
/// <param name="LanguageCode">Language code</param>
/// <param name="PluralForm">Plural form (empty for non-plural)</param>
/// <param name="Resolution">Resolution choice: "github", "cloud", "edit", "skip"</param>
/// <param name="EditedValue">Custom value if Resolution is "edit"</param>
public record GitHubPullConflictResolution(
    string Key,
    string LanguageCode,
    string PluralForm,
    string Resolution,
    string? EditedValue
);

/// <summary>
/// Request to resolve conflicts from a pull operation.
/// </summary>
public record GitHubPullConflictResolutionRequest(
    List<GitHubPullConflictResolution> Resolutions
);

/// <summary>
/// Summary of pending conflicts for a project.
/// </summary>
public record GitHubPullConflictSummary(
    int TotalConflicts,
    int BothModifiedCount,
    int DeletedInGitHubCount,
    int DeletedInCloudCount,
    List<GitHubPullConflict> Conflicts
);

// ============================================================================
// User and Organization GitHub DTOs
// ============================================================================

/// <summary>
/// Enhanced GitHub status for user profile page.
/// </summary>
public record UserGitHubStatus(
    bool Connected,
    string? GitHubLogin,
    string? AvatarUrl,
    DateTime? ConnectedAt,
    bool HasRepoScope,
    int ProjectsUsingToken
);

/// <summary>
/// GitHub connection status for an organization.
/// </summary>
public record OrganizationGitHubStatus(
    bool Connected,
    string? ConnectedByUsername,
    int? ConnectedByUserId,
    DateTime? ConnectedAt,
    int ProjectsUsingToken
);

/// <summary>
/// Information about a project using a GitHub token.
/// </summary>
public record ProjectTokenUsage(
    int ProjectId,
    string ProjectName,
    string? RepoFullName,
    string TokenSource // "user", "organization", "project_pat"
);

/// <summary>
/// List of projects using a GitHub token.
/// </summary>
public record GitHubTokenUsage(
    List<ProjectTokenUsage> Projects
);

/// <summary>
/// Request to connect organization GitHub using current user's token.
/// </summary>
public record ConnectOrganizationGitHubRequest(
    bool CopyUserToken = true
);

/// <summary>
/// Available token sources for connecting a project.
/// </summary>
public record AvailableTokenSources(
    bool UserTokenAvailable,
    string? UserGitHubLogin,
    bool OrganizationTokenAvailable,
    string? OrganizationName,
    string? OrganizationConnectedBy
);
