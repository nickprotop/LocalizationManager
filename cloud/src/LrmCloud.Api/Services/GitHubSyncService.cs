using LocalizationManager.Core.Cloud;
using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.GitHub;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Octokit;

using Project = LrmCloud.Shared.Entities.Project;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for syncing project translations to GitHub repositories.
/// Implements token hierarchy: Project PAT → Organization Token → User OAuth.
/// </summary>
public class GitHubSyncService : IGitHubSyncService
{
    private readonly AppDbContext _db;
    private readonly IFileExportService _exportService;
    private readonly IGitHubFormatResolver _formatResolver;
    private readonly CloudConfiguration _config;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        AppDbContext db,
        IFileExportService exportService,
        IGitHubFormatResolver formatResolver,
        CloudConfiguration config,
        ILogger<GitHubSyncService> logger)
    {
        _db = db;
        _exportService = exportService;
        _formatResolver = formatResolver;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubSyncResult> SyncToGitHubAsync(
        int projectId,
        int userId,
        string? commitMessage = null,
        bool createPr = true)
    {
        try
        {
            // Get project with related entities
            var project = await _db.Projects
                .Include(p => p.Organization)
                .Include(p => p.GitHubConnectedByUser)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return new GitHubSyncResult(false, null, 0, "Project not found");

            // Validate GitHub is connected
            if (string.IsNullOrEmpty(project.GitHubRepo))
                return new GitHubSyncResult(false, null, 0, "Project is not connected to a GitHub repository");

            // Resolve GitHub client with token hierarchy
            var (client, tokenSource, error) = await ResolveGitHubClientAsync(project, userId);
            if (client == null)
                return new GitHubSyncResult(false, null, 0, error ?? "Unable to get GitHub access");

            // Parse repo owner/name
            var repoParts = project.GitHubRepo.Split('/');
            if (repoParts.Length != 2)
                return new GitHubSyncResult(false, null, 0, $"Invalid GitHub repository format: {project.GitHubRepo}");

            var owner = repoParts[0];
            var repo = repoParts[1];
            var basePath = project.GitHubBasePath ?? ".";
            var baseBranch = project.GitHubDefaultBranch;

            // Resolve format for GitHub operations
            // Priority: GitHubFormat explicit > lrm.json in repo > auto-detect from files
            var format = await _formatResolver.ResolveFormatAsync(project, userId, owner, repo, baseBranch, basePath);

            // Export translations to file content
            var files = await _exportService.ExportProjectAsync(projectId, basePath, format);

            if (files.Count == 0)
                return new GitHubSyncResult(false, null, 0, "No translation files to sync");

            _logger.LogInformation(
                "Syncing {FileCount} files to {Owner}/{Repo} for project {ProjectId}",
                files.Count, owner, repo, projectId);

            // Update project status
            project.SyncStatus = "syncing";
            project.SyncError = null;
            await _db.SaveChangesAsync();

            // Create branch name with timestamp including milliseconds to prevent collision
            var branchName = $"lrm/translations-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";

            try
            {
                // Create branch from default branch
                var sourceBranch = await client.Repository.Branch.Get(owner, repo, baseBranch);
                var baseSha = sourceBranch.Commit.Sha;
                var newReference = new NewReference($"refs/heads/{branchName}", baseSha);
                await client.Git.Reference.Create(owner, repo, newReference);

                _logger.LogInformation("Created branch {Branch} from {BaseBranch}", branchName, baseBranch);

                // Track the latest commit SHA (will be updated as files are committed)
                string latestCommitSha = baseSha;

                try
                {
                    // Commit each file
                    var message = commitMessage ?? "Update translations from LRM Cloud";
                    foreach (var (filePath, content) in files)
                    {
                        // Try to get existing file SHA
                        string? fileSha = null;
                        try
                        {
                            var existing = await client.Repository.Content.GetAllContentsByRef(owner, repo, filePath, branchName);
                            fileSha = existing.FirstOrDefault()?.Sha;
                        }
                        catch (NotFoundException)
                        {
                            // File doesn't exist yet
                        }

                        // Create or update file and capture the new commit SHA
                        if (fileSha != null)
                        {
                            var updateRequest = new UpdateFileRequest(message, content, fileSha, branchName);
                            var updateResult = await client.Repository.Content.UpdateFile(owner, repo, filePath, updateRequest);
                            latestCommitSha = updateResult.Commit.Sha;
                        }
                        else
                        {
                            var createRequest = new CreateFileRequest(message, content, branchName);
                            var createResult = await client.Repository.Content.CreateFile(owner, repo, filePath, createRequest);
                            latestCommitSha = createResult.Commit.Sha;
                        }

                        _logger.LogDebug("Committed file {FilePath}", filePath);
                    }

                    string? prUrl = null;

                    if (createPr)
                    {
                        // Build PR body with summary
                        var prBody = BuildPullRequestBody(project, files, format);

                        var newPr = new NewPullRequest(
                            $"Update translations from LRM Cloud",
                            branchName,
                            baseBranch)
                        {
                            Body = prBody
                        };

                        var pullRequest = await client.PullRequest.Create(owner, repo, newPr);
                        prUrl = pullRequest.HtmlUrl;

                        _logger.LogInformation(
                            "Created PR #{PrNumber} for project {ProjectId}: {PrUrl}",
                            pullRequest.Number, projectId, prUrl);
                    }

                    // Use transaction for all database updates
                    await using var transaction = await _db.Database.BeginTransactionAsync();
                    try
                    {
                        // Update project sync status with the ACTUAL commit SHA
                        project.SyncStatus = "synced";
                        project.LastSyncedAt = DateTime.UtcNow;
                        project.LastSyncedCommit = latestCommitSha;
                        project.SyncError = null;
                        await _db.SaveChangesAsync();

                        // Update GitHub sync state for three-way merge on pull
                        await UpdateGitHubSyncStateAsync(projectId, latestCommitSha);

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }

                    return new GitHubSyncResult(true, prUrl, files.Count, null);
                }
                catch (Exception ex)
                {
                    // Clean up orphaned branch on failure
                    _logger.LogWarning(ex, "Cleaning up orphaned branch {Branch} after failure", branchName);
                    try
                    {
                        await client.Git.Reference.Delete(owner, repo, $"heads/{branchName}");
                        _logger.LogInformation("Deleted orphaned branch {Branch}", branchName);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to delete orphaned branch {Branch}", branchName);
                    }
                    throw;
                }
            }
            catch (RateLimitExceededException ex)
            {
                var resetTime = ex.GetRetryAfterTimeSpan();
                var resetMessage = resetTime.TotalSeconds > 0
                    ? $"GitHub API rate limit exceeded. Try again in {(int)resetTime.TotalMinutes} minutes."
                    : "GitHub API rate limit exceeded. Please wait before retrying.";

                _logger.LogWarning(ex, "Rate limit exceeded for project {ProjectId}. Reset: {Reset}",
                    projectId, ex.Reset);

                project.SyncStatus = "error";
                project.SyncError = resetMessage;
                await _db.SaveChangesAsync();

                return new GitHubSyncResult(false, null, 0, resetMessage);
            }
            catch (AuthorizationException ex)
            {
                _logger.LogWarning(ex, "Authorization failed for project {ProjectId}", projectId);

                project.SyncStatus = "error";
                project.SyncError = "GitHub authorization failed. Please reconnect your GitHub account or check repository permissions.";
                await _db.SaveChangesAsync();

                return new GitHubSyncResult(false, null, 0,
                    "GitHub authorization failed. Please reconnect your GitHub account or check repository permissions.");
            }
            catch (ApiValidationException ex)
            {
                var validationMessage = $"GitHub validation error: {string.Join(", ", ex.ApiError.Errors?.Select(e => e.Message) ?? new[] { ex.Message })}";
                _logger.LogWarning(ex, "API validation failed for project {ProjectId}: {Message}",
                    projectId, validationMessage);

                project.SyncStatus = "error";
                project.SyncError = validationMessage;
                await _db.SaveChangesAsync();

                return new GitHubSyncResult(false, null, 0, validationMessage);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Repository or branch not found for project {ProjectId}", projectId);

                project.SyncStatus = "error";
                project.SyncError = "Repository or branch not found. Please verify the repository exists and you have access.";
                await _db.SaveChangesAsync();

                return new GitHubSyncResult(false, null, 0,
                    "Repository or branch not found. Please verify the repository exists and you have access.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during GitHub sync for project {ProjectId}", projectId);

                // Update project with error
                project.SyncStatus = "error";
                project.SyncError = ex.Message;
                await _db.SaveChangesAsync();

                return new GitHubSyncResult(false, null, 0, $"GitHub API error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error syncing project {ProjectId} to GitHub", projectId);
            return new GitHubSyncResult(false, null, 0, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GitHubSyncStatus> GetSyncStatusAsync(int projectId, int userId)
    {
        var project = await _db.Projects
            .Include(p => p.Organization)
            .Include(p => p.GitHubConnectedByUser)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            return new GitHubSyncStatus(
                false, null, null, null, null, null, null, null);
        }

        var isConnected = !string.IsNullOrEmpty(project.GitHubRepo);

        // Determine connection method
        string? connectionMethod = null;
        string? connectedByUsername = null;

        if (isConnected)
        {
            if (!string.IsNullOrEmpty(project.GitHubAccessTokenEncrypted))
            {
                connectionMethod = "project_pat";
                connectedByUsername = project.GitHubConnectedByUser?.DisplayName;
            }
            else if (project.Organization != null &&
                     !string.IsNullOrEmpty(project.Organization.GitHubAccessTokenEncrypted))
            {
                connectionMethod = "organization";
                connectedByUsername = project.Organization.GitHubConnectedByUser?.DisplayName;
            }
            else
            {
                connectionMethod = "user_oauth";
                connectedByUsername = project.GitHubConnectedByUser?.DisplayName;
            }
        }

        // Note: Last PR URL could be tracked in the future by adding to Project entity
        // For now, we don't persist it between syncs

        return new GitHubSyncStatus(
            isConnected,
            project.GitHubRepo,
            project.GitHubBasePath,
            project.LastSyncedAt,
            null, // Last PR URL not persisted yet
            project.SyncStatus,
            connectionMethod,
            connectedByUsername);
    }

    /// <inheritdoc />
    public async Task<(bool Valid, string? ErrorMessage)> ValidateSyncConfigurationAsync(int projectId, int userId)
    {
        var project = await _db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return (false, "Project not found");

        if (string.IsNullOrEmpty(project.GitHubRepo))
            return (false, "Project is not connected to a GitHub repository");

        // Validate we can get a GitHub client
        var (client, tokenSource, error) = await ResolveGitHubClientAsync(project, userId);
        if (client == null)
            return (false, error ?? "Unable to get GitHub access");

        // Verify repository access
        var repoParts = project.GitHubRepo.Split('/');
        if (repoParts.Length != 2)
            return (false, $"Invalid GitHub repository format: {project.GitHubRepo}");

        try
        {
            var repository = await client.Repository.Get(repoParts[0], repoParts[1]);
            if (repository == null)
                return (false, "Repository not found or access denied");

            // Check if we have push access
            if (!repository.Permissions.Push)
                return (false, "No push access to repository. Please use a token with write permissions.");

            return (true, null);
        }
        catch (NotFoundException)
        {
            return (false, "Repository not found or access denied");
        }
        catch (AuthorizationException)
        {
            return (false, "GitHub token is invalid or expired. Please reconnect your GitHub account.");
        }
        catch (Exception ex)
        {
            return (false, $"Error validating repository access: {ex.Message}");
        }
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    /// <summary>
    /// Resolves the GitHub client using token hierarchy:
    /// 1. Project PAT (if set)
    /// 2. Organization token (if org project and org has token)
    /// 3. User's OAuth token
    /// </summary>
    private async Task<(GitHubClient? Client, string? TokenSource, string? Error)> ResolveGitHubClientAsync(
        Project project, int userId)
    {
        string? accessToken = null;
        string? tokenSource = null;

        // 1. Check project PAT
        if (!string.IsNullOrEmpty(project.GitHubAccessTokenEncrypted))
        {
            accessToken = TokenEncryption.Decrypt(
                project.GitHubAccessTokenEncrypted,
                _config.Encryption.TokenKey);
            tokenSource = "project_pat";
        }
        // 2. Check organization token
        else if (project.OrganizationId != null && project.Organization != null &&
                 !string.IsNullOrEmpty(project.Organization.GitHubAccessTokenEncrypted))
        {
            accessToken = TokenEncryption.Decrypt(
                project.Organization.GitHubAccessTokenEncrypted,
                _config.Encryption.TokenKey);
            tokenSource = "organization";
        }
        // 3. Fall back to user's OAuth token
        else
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null && !string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted))
            {
                accessToken = TokenEncryption.Decrypt(
                    user.GitHubAccessTokenEncrypted,
                    _config.Encryption.TokenKey);
                tokenSource = "user_oauth";
            }
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            return (null, null, "No GitHub access configured. Please connect a GitHub account or add a Personal Access Token.");
        }

        var client = new GitHubClient(new ProductHeaderValue("LrmCloud"))
        {
            Credentials = new Credentials(accessToken)
        };

        return (client, tokenSource, null);
    }

    /// <summary>
    /// Builds the Pull Request body with a summary of changes.
    /// </summary>
    private static string BuildPullRequestBody(Project project, Dictionary<string, string> files, string format)
    {
        var languageFiles = files.Keys
            .Select(path => ExtractLanguageFromPath(path, format))
            .Where(lang => !string.IsNullOrEmpty(lang))
            .Distinct()
            .OrderBy(lang => lang)
            .ToList();

        var body = $"""
            ## Summary

            This PR updates translations from [LRM Cloud](https://lrmcloud.com).

            **Project:** {project.Name}
            **Format:** {format}
            **Files updated:** {files.Count}

            ### Languages

            {string.Join("\n", languageFiles.Select(lang => $"- {lang}"))}

            ---
            *Generated by LRM Cloud*
            """;

        return body;
    }

    /// <summary>
    /// Extracts the language code from a file path based on format.
    /// </summary>
    private static string? ExtractLanguageFromPath(string path, string format)
    {
        var fileName = Path.GetFileName(path);
        var dirName = Path.GetDirectoryName(path)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? "";

        return format.ToLowerInvariant() switch
        {
            // RESX: Resources.fr.resx → fr, Resources.resx → default
            "resx" => fileName.Contains('.')
                ? fileName.Split('.').Length > 2
                    ? fileName.Split('.')[^2]
                    : "default"
                : null,

            // JSON: strings.fr.json → fr, strings.json → default
            "json" => fileName.Contains('.')
                ? fileName.Split('.').Length > 2
                    ? fileName.Split('.')[^2]
                    : "default"
                : null,

            // i18next: en.json → en
            "i18next" => Path.GetFileNameWithoutExtension(fileName),

            // Android: values-es/strings.xml → es, values/strings.xml → default
            "android" => dirName.StartsWith("values-")
                ? dirName[7..]
                : dirName == "values" ? "default" : null,

            // iOS: es.lproj/Localizable.strings → es
            "ios" => dirName.EndsWith(".lproj")
                ? dirName[..^6]
                : null,

            // PO: fr.po → fr, messages.pot → default
            "po" or "gettext" => fileName.EndsWith(".pot", StringComparison.OrdinalIgnoreCase)
                ? "default"
                : Path.GetFileNameWithoutExtension(fileName),

            // XLIFF: messages.fr.xliff → fr, messages.xliff → default
            "xliff" or "xlf" => fileName.Contains('.')
                ? fileName.Split('.').Length > 2
                    ? fileName.Split('.')[^2]
                    : "default"
                : null,

            _ => null
        };
    }

    /// <summary>
    /// Updates the GitHub sync state after a successful push.
    /// This records the current state of all translations as the "base" for three-way merge on pull.
    /// </summary>
    private async Task UpdateGitHubSyncStateAsync(int projectId, string commitSha)
    {
        // Get all translations that were exported
        var keys = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .Include(k => k.Translations)
            .ToListAsync();

        // Bulk load existing sync states to avoid N+1 queries
        var existingStates = await _db.GitHubSyncStates
            .Where(s => s.ProjectId == projectId)
            .ToDictionaryAsync(s => (s.KeyName, s.LanguageCode, s.PluralForm));

        var updateCount = 0;

        foreach (var key in keys)
        {
            foreach (var translation in key.Translations)
            {
                // Compute hash if not already stored
                var hash = translation.Hash;
                if (string.IsNullOrEmpty(hash))
                {
                    hash = EntryHasher.ComputeHash(translation.Value ?? "", translation.Comment);
                }

                var stateKey = (key.KeyName, translation.LanguageCode, translation.PluralForm);

                if (existingStates.TryGetValue(stateKey, out var syncState))
                {
                    // Update existing state
                    syncState.GitHubHash = hash;
                    syncState.GitHubValue = translation.Value;
                    syncState.GitHubComment = translation.Comment;
                    syncState.GitHubCommitSha = commitSha;
                    syncState.SyncedAt = DateTime.UtcNow;
                    syncState.Version++;  // Increment for optimistic locking
                }
                else
                {
                    // Create new state
                    syncState = new GitHubSyncState
                    {
                        ProjectId = projectId,
                        KeyName = key.KeyName,
                        LanguageCode = translation.LanguageCode,
                        PluralForm = translation.PluralForm,
                        GitHubHash = hash,
                        GitHubValue = translation.Value,
                        GitHubComment = translation.Comment,
                        GitHubCommitSha = commitSha,
                        SyncedAt = DateTime.UtcNow
                    };
                    _db.GitHubSyncStates.Add(syncState);
                }

                updateCount++;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Updated GitHub sync state for {Count} translations after push to project {ProjectId}",
            updateCount, projectId);
    }
}
