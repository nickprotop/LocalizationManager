using System.Security.Claims;
using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Controller for GitHub repository integration.
/// </summary>
[Authorize]
[ApiController]
[Route("api/github")]
public class GitHubController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IGitHubApiService _githubApi;
    private readonly IGitHubSyncService _syncService;
    private readonly IGitHubPullService _pullService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<GitHubController> _logger;

    public GitHubController(
        AppDbContext db,
        IGitHubApiService githubApi,
        IGitHubSyncService syncService,
        IGitHubPullService pullService,
        CloudConfiguration config,
        ILogger<GitHubController> logger)
    {
        _db = db;
        _githubApi = githubApi;
        _syncService = syncService;
        _pullService = pullService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get the current user's GitHub token status.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<GitHubTokenStatus>>> GetTokenStatus()
    {
        var userId = GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound("not_found", "User not found");

        if (string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted))
        {
            return Success(new GitHubTokenStatus(
                Valid: false,
                HasRepoScope: false,
                GitHubLogin: null,
                ErrorMessage: "No GitHub account linked"
            ));
        }

        var (valid, errorMessage, _) = await _githubApi.ValidateTokenAsync(userId);

        return Success(new GitHubTokenStatus(
            Valid: valid,
            HasRepoScope: valid, // Assume if token is valid it has repo scope (we request it)
            GitHubLogin: user.GitHubId?.ToString(),
            ErrorMessage: errorMessage
        ));
    }

    /// <summary>
    /// List repositories the user has access to.
    /// </summary>
    [HttpGet("repos")]
    public async Task<ActionResult<ApiResponse<List<GitHubRepoDto>>>> ListRepositories()
    {
        var userId = GetUserId();

        try
        {
            var repos = await _githubApi.ListUserRepositoriesAsync(userId);
            var result = repos.Select(r => new GitHubRepoDto(
                r.Id,
                r.FullName,
                r.DefaultBranch,
                r.Private,
                r.Description
            )).ToList();

            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("github_error", ex.Message);
        }
    }

    /// <summary>
    /// List branches for a repository.
    /// </summary>
    [HttpGet("repos/{owner}/{repo}/branches")]
    public async Task<ActionResult<ApiResponse<List<GitHubBranchDto>>>> ListBranches(string owner, string repo)
    {
        var userId = GetUserId();

        try
        {
            var branches = await _githubApi.ListBranchesAsync(userId, owner, repo);
            var result = branches.Select(b => new GitHubBranchDto(
                b.Name,
                b.Commit.Sha
            )).ToList();

            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("github_error", ex.Message);
        }
    }

    /// <summary>
    /// Get directory contents from a repository.
    /// </summary>
    [HttpGet("repos/{owner}/{repo}/contents")]
    public async Task<ActionResult<ApiResponse<List<GitHubFileDto>>>> GetContents(
        string owner, string repo, [FromQuery] string path = "", [FromQuery] string branch = "main")
    {
        var userId = GetUserId();

        try
        {
            var contents = await _githubApi.GetDirectoryContentsAsync(userId, owner, repo, path, branch);
            var result = contents.Select(c => new GitHubFileDto(
                c.Name,
                c.Path,
                c.Type.ToString().ToLowerInvariant(),
                c.Size
            )).ToList();

            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("github_error", ex.Message);
        }
    }

    /// <summary>
    /// Auto-detect localization files in a repository.
    /// </summary>
    [HttpPost("repos/{owner}/{repo}/detect")]
    public async Task<ActionResult<ApiResponse<DetectLocalizationResult>>> DetectLocalization(
        string owner, string repo, [FromQuery] string branch = "main", [FromQuery] string basePath = "")
    {
        var userId = GetUserId();

        try
        {
            var detected = await _githubApi.DetectLocalizationFilesAsync(userId, owner, repo, branch, basePath);

            var hasLrmConfig = detected.Any(d => d.HasLrmConfig);
            var recommended = detected.OrderByDescending(d => d.FileCount).FirstOrDefault();

            var result = new DetectLocalizationResult(
                DetectedPaths: detected.Select(d => new DetectedLocalizationPathDto(
                    d.Path, d.Format, d.FileCount, d.HasLrmConfig
                )).ToList(),
                RecommendedPath: recommended?.Path,
                RecommendedFormat: recommended?.Format,
                HasLrmConfig: hasLrmConfig
            );

            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("github_error", ex.Message);
        }
    }

    /// <summary>
    /// Connect a GitHub repository to a project using user's OAuth token.
    /// </summary>
    [HttpPost("/api/projects/{projectId}/github/connect")]
    public async Task<ActionResult<ApiResponse<GitHubSyncStatus>>> ConnectRepository(
        int projectId, [FromBody] ConnectGitHubRequest request)
    {
        var userId = GetUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return NotFound("not_found", "Project not found");

        // Verify user has access to the project
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        // Verify the repository exists and user has access
        var parts = request.RepoFullName.Split('/');
        if (parts.Length != 2)
            return BadRequest("invalid_repo", "Invalid repository name. Expected format: owner/repo");

        var repository = await _githubApi.GetRepositoryAsync(userId, parts[0], parts[1]);
        if (repository == null)
            return NotFound("repo_not_found", "Repository not found or you don't have access to it");

        // Update project - reset sync state for fresh connection
        project.GitHubRepo = request.RepoFullName;
        project.GitHubDefaultBranch = request.DefaultBranch;
        project.GitHubBasePath = request.BasePath;
        project.GitHubConnectedByUserId = userId;
        project.SyncStatus = null; // Reset sync status
        project.SyncError = null;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        _logger.LogInformation("Connected GitHub repo {Repo} to project {ProjectId} by user {UserId}",
            request.RepoFullName, projectId, userId);

        return Success(new GitHubSyncStatus(
            Connected: true,
            RepoFullName: project.GitHubRepo,
            BasePath: project.GitHubBasePath,
            LastSyncedAt: project.LastSyncedAt,
            LastPrUrl: null,
            SyncStatus: null,
            ConnectionMethod: "oauth",
            ConnectedByUsername: user?.Username
        ));
    }

    /// <summary>
    /// Connect a GitHub repository to a project using a Personal Access Token.
    /// </summary>
    [HttpPost("/api/projects/{projectId}/github/connect-pat")]
    public async Task<ActionResult<ApiResponse<GitHubSyncStatus>>> ConnectRepositoryWithPat(
        int projectId, [FromBody] ConnectGitHubPatRequest request)
    {
        var userId = GetUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return NotFound("not_found", "Project not found");

        // Verify user has access to the project
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        // Verify the repository exists using the PAT
        var parts = request.RepoFullName.Split('/');
        if (parts.Length != 2)
            return BadRequest("invalid_repo", "Invalid repository name. Expected format: owner/repo");

        // TODO: Validate PAT by making a test request

        // Encrypt and store the PAT
        var encryptedToken = TokenEncryption.Encrypt(request.PersonalAccessToken, _config.Encryption.TokenKey);

        // Reset sync state for fresh connection
        project.GitHubRepo = request.RepoFullName;
        project.GitHubDefaultBranch = request.DefaultBranch;
        project.GitHubBasePath = request.BasePath;
        project.GitHubAccessTokenEncrypted = encryptedToken;
        project.GitHubConnectedByUserId = userId;
        project.SyncStatus = null; // Reset sync status
        project.SyncError = null;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        _logger.LogInformation("Connected GitHub repo {Repo} to project {ProjectId} with PAT by user {UserId}",
            request.RepoFullName, projectId, userId);

        return Success(new GitHubSyncStatus(
            Connected: true,
            RepoFullName: project.GitHubRepo,
            BasePath: project.GitHubBasePath,
            LastSyncedAt: project.LastSyncedAt,
            LastPrUrl: null,
            SyncStatus: null,
            ConnectionMethod: "pat",
            ConnectedByUsername: user?.Username
        ));
    }

    /// <summary>
    /// Disconnect a GitHub repository from a project.
    /// </summary>
    [HttpPost("/api/projects/{projectId}/github/disconnect")]
    public async Task<ActionResult<ApiResponse<bool>>> DisconnectRepository(int projectId)
    {
        var userId = GetUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return NotFound("not_found", "Project not found");

        // Verify user has access to the project
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        project.GitHubRepo = null;
        project.GitHubDefaultBranch = "main";
        project.GitHubBasePath = null;
        project.GitHubAccessTokenEncrypted = null;
        project.GitHubConnectedByUserId = null;
        project.LastSyncedAt = null;
        project.LastSyncedCommit = null;
        project.SyncStatus = null; // Clear sync status on disconnect
        project.SyncError = null;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Disconnected GitHub from project {ProjectId} by user {UserId}", projectId, userId);

        return Success(true);
    }

    /// <summary>
    /// Get GitHub sync status for a project.
    /// </summary>
    [HttpGet("/api/projects/{projectId}/github/status")]
    public async Task<ActionResult<ApiResponse<GitHubSyncStatus>>> GetSyncStatus(int projectId)
    {
        var userId = GetUserId();

        // Verify user has access to the project
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        var status = await _syncService.GetSyncStatusAsync(projectId, userId);
        return Success(status);
    }

    /// <summary>
    /// Trigger a sync to GitHub (create PR with translations).
    /// </summary>
    [HttpPost("/api/projects/{projectId}/github/sync")]
    public async Task<ActionResult<ApiResponse<GitHubSyncResult>>> TriggerSync(
        int projectId, [FromBody] TriggerSyncRequest request)
    {
        var userId = GetUserId();

        // Verify user has access to the project
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        // Validate configuration before sync
        var (valid, validationError) = await _syncService.ValidateSyncConfigurationAsync(projectId, userId);
        if (!valid)
            return BadRequest("sync_config_error", validationError ?? "Invalid sync configuration");

        // Perform the sync
        var result = await _syncService.SyncToGitHubAsync(
            projectId,
            userId,
            request.CommitMessage,
            request.CreatePr);

        if (!result.Success)
        {
            _logger.LogWarning("GitHub sync failed for project {ProjectId}: {Error}",
                projectId, result.ErrorMessage);
        }

        return Success(result);
    }

    // ============================================================================
    // GitHub Pull Endpoints (Pull from GitHub to Cloud)
    // ============================================================================

    /// <summary>
    /// Preview changes from pulling GitHub.
    /// Shows what would change without applying.
    /// </summary>
    [HttpGet("/api/projects/{projectId}/github/pull/preview")]
    public async Task<ActionResult<ApiResponse<GitHubPullResult>>> PreviewPull(int projectId)
    {
        var userId = GetUserId();

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        var result = await _pullService.PreviewPullAsync(projectId, userId);
        return Success(result);
    }

    /// <summary>
    /// Pull translations from GitHub to Cloud.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Pull request with optional strategy</param>
    /// <remarks>
    /// Strategy options:
    /// - "prompt" (default): Returns conflicts for user resolution
    /// - "github": Accept all GitHub values (auto-accept remote)
    /// - "cloud": Keep all Cloud values (auto-deny remote changes)
    /// </remarks>
    [HttpPost("/api/projects/{projectId}/github/pull")]
    public async Task<ActionResult<ApiResponse<GitHubPullResult>>> PullFromGitHub(
        int projectId, [FromBody] GitHubPullRequest request)
    {
        var userId = GetUserId();

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        if (request.PreviewOnly)
        {
            var previewResult = await _pullService.PreviewPullAsync(projectId, userId);
            return Success(previewResult);
        }

        var result = await _pullService.PullFromGitHubAsync(projectId, userId, request.Strategy);

        if (!result.Success)
        {
            _logger.LogWarning("GitHub pull failed for project {ProjectId}: {Error}",
                projectId, result.ErrorMessage);
        }

        return Success(result);
    }

    /// <summary>
    /// Resolve conflicts from a previous pull operation.
    /// </summary>
    [HttpPost("/api/projects/{projectId}/github/pull/resolve")]
    public async Task<ActionResult<ApiResponse<GitHubPullResult>>> ResolveConflicts(
        int projectId, [FromBody] GitHubPullConflictResolutionRequest request)
    {
        var userId = GetUserId();

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        var result = await _pullService.ResolveConflictsAsync(projectId, userId, request);
        return Success(result);
    }

    /// <summary>
    /// Get pending conflicts for a project.
    /// </summary>
    [HttpGet("/api/projects/{projectId}/github/pull/conflicts")]
    public async Task<ActionResult<ApiResponse<GitHubPullConflictSummary>>> GetPendingConflicts(int projectId)
    {
        var userId = GetUserId();

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        var result = await _pullService.GetPendingConflictsAsync(projectId);
        return Success(result);
    }

    // ============================================================================
    // User GitHub Account Endpoints
    // ============================================================================

    /// <summary>
    /// Get enhanced GitHub status for user profile page.
    /// </summary>
    [HttpGet("user-status")]
    public async Task<ActionResult<ApiResponse<UserGitHubStatus>>> GetUserGitHubStatus()
    {
        var userId = GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound("not_found", "User not found");

        if (string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted))
        {
            return Success(new UserGitHubStatus(
                Connected: false,
                GitHubLogin: null,
                AvatarUrl: null,
                ConnectedAt: null,
                HasRepoScope: false,
                ProjectsUsingToken: 0
            ));
        }

        // Get GitHub login by validating token
        var (valid, _, _) = await _githubApi.ValidateTokenAsync(userId);

        // Count projects using this user's token (OAuth connection, not PAT)
        var projectsUsingToken = await _db.Projects
            .Where(p => p.GitHubConnectedByUserId == userId &&
                        p.GitHubAccessTokenEncrypted == null &&  // Using OAuth, not PAT
                        !string.IsNullOrEmpty(p.GitHubRepo))
            .CountAsync();

        return Success(new UserGitHubStatus(
            Connected: valid,
            GitHubLogin: user.GitHubId?.ToString(),  // TODO: Store actual login
            AvatarUrl: null,  // TODO: Store avatar URL
            ConnectedAt: null,  // TODO: Store connection timestamp
            HasRepoScope: valid,
            ProjectsUsingToken: projectsUsingToken
        ));
    }

    /// <summary>
    /// Get projects using the current user's GitHub token.
    /// </summary>
    [HttpGet("usage")]
    public async Task<ActionResult<ApiResponse<GitHubTokenUsage>>> GetUserTokenUsage()
    {
        var userId = GetUserId();

        var projects = await _db.Projects
            .Where(p => p.GitHubConnectedByUserId == userId &&
                        !string.IsNullOrEmpty(p.GitHubRepo))
            .Select(p => new ProjectTokenUsage(
                p.Id,
                p.Name,
                p.GitHubRepo,
                p.GitHubAccessTokenEncrypted != null ? "project_pat" : "user"
            ))
            .ToListAsync();

        return Success(new GitHubTokenUsage(projects));
    }

    // ============================================================================
    // Organization GitHub Endpoints
    // ============================================================================

    /// <summary>
    /// Get GitHub status for an organization.
    /// </summary>
    [HttpGet("/api/organizations/{organizationId}/github/status")]
    public async Task<ActionResult<ApiResponse<OrganizationGitHubStatus>>> GetOrganizationGitHubStatus(int organizationId)
    {
        var userId = GetUserId();

        // Verify user is member of organization
        var isMember = await _db.OrganizationMembers.AnyAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId);

        if (!isMember)
            return Forbidden("forbidden", "You are not a member of this organization");

        var org = await _db.Organizations
            .Include(o => o.GitHubConnectedByUser)
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        if (org == null)
            return NotFound("not_found", "Organization not found");

        var connected = !string.IsNullOrEmpty(org.GitHubAccessTokenEncrypted);

        // Count projects using org token
        var projectsUsingToken = connected ? await _db.Projects
            .Where(p => p.OrganizationId == organizationId &&
                        p.GitHubAccessTokenEncrypted == null &&  // Not using project PAT
                        p.GitHubConnectedByUserId == null &&     // Not using user OAuth
                        !string.IsNullOrEmpty(p.GitHubRepo))
            .CountAsync() : 0;

        return Success(new OrganizationGitHubStatus(
            Connected: connected,
            ConnectedByUsername: org.GitHubConnectedByUser?.Username,
            ConnectedByUserId: org.GitHubConnectedByUserId,
            ConnectedAt: null,  // TODO: Store connection timestamp
            ProjectsUsingToken: projectsUsingToken
        ));
    }

    /// <summary>
    /// Connect organization GitHub by copying current user's token.
    /// </summary>
    [HttpPost("/api/organizations/{organizationId}/github/connect")]
    public async Task<ActionResult<ApiResponse<OrganizationGitHubStatus>>> ConnectOrganizationGitHub(
        int organizationId, [FromBody] ConnectOrganizationGitHubRequest request)
    {
        var userId = GetUserId();

        // Verify user is admin of organization
        var membership = await _db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId);

        if (membership == null)
            return Forbidden("forbidden", "You are not a member of this organization");

        if (membership.Role != "owner" && membership.Role != "admin")
            return Forbidden("forbidden", "Only organization owners and admins can connect GitHub");

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org == null)
            return NotFound("not_found", "Organization not found");

        // Get current user's GitHub token
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted))
            return BadRequest("no_github", "You must connect your personal GitHub account first");

        // Copy user's token to organization
        org.GitHubAccessTokenEncrypted = user.GitHubAccessTokenEncrypted;
        org.GitHubConnectedByUserId = userId;
        org.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Connected GitHub to organization {OrgId} by user {UserId}", organizationId, userId);

        return Success(new OrganizationGitHubStatus(
            Connected: true,
            ConnectedByUsername: user.Username,
            ConnectedByUserId: userId,
            ConnectedAt: DateTime.UtcNow,
            ProjectsUsingToken: 0
        ));
    }

    /// <summary>
    /// Disconnect GitHub from organization.
    /// </summary>
    [HttpPost("/api/organizations/{organizationId}/github/disconnect")]
    public async Task<ActionResult<ApiResponse<bool>>> DisconnectOrganizationGitHub(int organizationId)
    {
        var userId = GetUserId();

        // Verify user is admin of organization
        var membership = await _db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId);

        if (membership == null)
            return Forbidden("forbidden", "You are not a member of this organization");

        if (membership.Role != "owner" && membership.Role != "admin")
            return Forbidden("forbidden", "Only organization owners and admins can disconnect GitHub");

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org == null)
            return NotFound("not_found", "Organization not found");

        // Check if any projects are using the org token
        var projectsUsingToken = await _db.Projects
            .Where(p => p.OrganizationId == organizationId &&
                        p.GitHubAccessTokenEncrypted == null &&
                        p.GitHubConnectedByUserId == null &&
                        !string.IsNullOrEmpty(p.GitHubRepo))
            .CountAsync();

        if (projectsUsingToken > 0)
            return BadRequest("token_in_use",
                $"Cannot disconnect: {projectsUsingToken} project(s) are using the organization GitHub token. " +
                "Please reconfigure these projects to use a different token source first.");

        org.GitHubAccessTokenEncrypted = null;
        org.GitHubConnectedByUserId = null;
        org.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Disconnected GitHub from organization {OrgId} by user {UserId}", organizationId, userId);

        return Success(true);
    }

    /// <summary>
    /// Get projects using the organization's GitHub token.
    /// </summary>
    [HttpGet("/api/organizations/{organizationId}/github/usage")]
    public async Task<ActionResult<ApiResponse<GitHubTokenUsage>>> GetOrganizationTokenUsage(int organizationId)
    {
        var userId = GetUserId();

        // Verify user is member of organization
        var isMember = await _db.OrganizationMembers.AnyAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId);

        if (!isMember)
            return Forbidden("forbidden", "You are not a member of this organization");

        var projects = await _db.Projects
            .Where(p => p.OrganizationId == organizationId &&
                        !string.IsNullOrEmpty(p.GitHubRepo))
            .Select(p => new ProjectTokenUsage(
                p.Id,
                p.Name,
                p.GitHubRepo,
                p.GitHubAccessTokenEncrypted != null ? "project_pat" :
                    p.GitHubConnectedByUserId != null ? "user" : "organization"
            ))
            .ToListAsync();

        return Success(new GitHubTokenUsage(projects));
    }

    /// <summary>
    /// Get available token sources for connecting a project.
    /// </summary>
    [HttpGet("/api/projects/{projectId}/github/available-tokens")]
    public async Task<ActionResult<ApiResponse<AvailableTokenSources>>> GetAvailableTokenSources(int projectId)
    {
        var userId = GetUserId();

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbidden("forbidden", "You don't have access to this project");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null)
            return NotFound("not_found", "Project not found");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var userTokenAvailable = user != null && !string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted);

        string? orgName = null;
        string? orgConnectedBy = null;
        var orgTokenAvailable = false;

        if (project.OrganizationId.HasValue)
        {
            var org = await _db.Organizations
                .Include(o => o.GitHubConnectedByUser)
                .FirstOrDefaultAsync(o => o.Id == project.OrganizationId);

            if (org != null)
            {
                orgName = org.Name;
                orgTokenAvailable = !string.IsNullOrEmpty(org.GitHubAccessTokenEncrypted);
                orgConnectedBy = org.GitHubConnectedByUser?.Username;
            }
        }

        return Success(new AvailableTokenSources(
            UserTokenAvailable: userTokenAvailable,
            UserGitHubLogin: userTokenAvailable ? user?.GitHubId?.ToString() : null,
            OrganizationTokenAvailable: orgTokenAvailable,
            OrganizationName: orgName,
            OrganizationConnectedBy: orgConnectedBy
        ));
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task<bool> HasProjectAccessAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return false;

        // Personal project
        if (project.UserId == userId) return true;

        // Organization project - check membership
        if (project.OrganizationId.HasValue)
        {
            return await _db.OrganizationMembers.AnyAsync(
                m => m.OrganizationId == project.OrganizationId && m.UserId == userId);
        }

        return false;
    }
}
