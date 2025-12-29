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
/// Service for pulling translations from GitHub to Cloud.
/// Implements three-way merge with conflict detection.
/// </summary>
public class GitHubPullService : IGitHubPullService
{
    private readonly AppDbContext _db;
    private readonly IGitHubApiService _githubApi;
    private readonly IFileImportService _importService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<GitHubPullService> _logger;

    // File patterns for each format
    private static readonly Dictionary<string, string[]> FilePatterns = new()
    {
        ["resx"] = new[] { ".resx" },
        ["json"] = new[] { ".json" },
        ["i18next"] = new[] { ".json" },
        ["android"] = new[] { "strings.xml" },
        ["ios"] = new[] { "Localizable.strings", "Localizable.stringsdict" },
        ["po"] = new[] { ".po", ".pot" },
        ["gettext"] = new[] { ".po", ".pot" },
        ["xliff"] = new[] { ".xliff", ".xlf" },
        ["xlf"] = new[] { ".xliff", ".xlf" }
    };

    public GitHubPullService(
        AppDbContext db,
        IGitHubApiService githubApi,
        IFileImportService importService,
        CloudConfiguration config,
        ILogger<GitHubPullService> logger)
    {
        _db = db;
        _githubApi = githubApi;
        _importService = importService;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubPullResult> PreviewPullAsync(int projectId, int userId)
    {
        return await PullFromGitHubInternalAsync(projectId, userId, "prompt", previewOnly: true);
    }

    /// <inheritdoc />
    public async Task<GitHubPullResult> PullFromGitHubAsync(int projectId, int userId, string strategy = "prompt")
    {
        return await PullFromGitHubInternalAsync(projectId, userId, strategy, previewOnly: false);
    }

    private async Task<GitHubPullResult> PullFromGitHubInternalAsync(
        int projectId, int userId, string strategy, bool previewOnly)
    {
        try
        {
            // Get project
            var project = await _db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return CreateErrorResult("Project not found");

            if (string.IsNullOrEmpty(project.GitHubRepo))
                return CreateErrorResult("Project is not connected to a GitHub repository");

            // Parse repo owner/name
            var repoParts = project.GitHubRepo.Split('/');
            if (repoParts.Length != 2)
                return CreateErrorResult($"Invalid GitHub repository format: {project.GitHubRepo}");

            var owner = repoParts[0];
            var repo = repoParts[1];
            var branch = project.GitHubDefaultBranch;
            var basePath = project.GitHubBasePath ?? ".";

            // 1. Fetch files from GitHub
            _logger.LogInformation("Fetching files from GitHub {Owner}/{Repo} branch {Branch}", owner, repo, branch);
            var githubFiles = await FetchTranslationFilesAsync(userId, owner, repo, branch, basePath, project.Format);

            if (!githubFiles.Any())
                return CreateErrorResult("No translation files found in the repository");

            // 2. Parse files into entries
            var githubEntries = _importService.ParseFiles(project.Format, githubFiles, project.DefaultLanguage);

            // 3. Load DB entries
            var dbEntries = await LoadDbEntriesAsync(projectId);

            // 4. Load sync state (base hashes)
            var baseHashes = await LoadGitHubSyncStateAsync(projectId);

            // 5. Perform three-way merge
            var mergeResult = PerformThreeWayMerge(githubEntries, dbEntries, baseHashes, project.DefaultLanguage);

            // 6. Apply strategy to conflicts
            if (strategy == "github")
            {
                foreach (var conflict in mergeResult.Conflicts)
                {
                    mergeResult.ToApply.Add(githubEntries[conflict.Key]);
                }
                mergeResult.Conflicts.Clear();
            }
            else if (strategy == "cloud")
            {
                // Keep cloud values, remove conflicts
                mergeResult.Conflicts.Clear();
            }

            // Get commit SHA for tracking
            string? commitSha = null;
            try
            {
                var branches = await _githubApi.ListBranchesAsync(userId, owner, repo);
                commitSha = branches.FirstOrDefault(b => b.Name == branch)?.Commit.Sha;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get commit SHA for branch {Branch} - continuing without it", branch);
            }

            // 7. Apply changes if not preview
            if (!previewOnly && (mergeResult.ToApply.Any() || mergeResult.ToAdd.Any() || mergeResult.ToDelete.Any()))
            {
                // Use transaction to ensure atomicity of all database changes
                await using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    await ApplyChangesAsync(projectId, mergeResult, userId, project.DefaultLanguage);

                    // 8. Update sync state
                    await UpdateGitHubSyncStateAsync(projectId, githubEntries, commitSha);

                    // Update project pull tracking
                    project.LastGitHubPullAt = DateTime.UtcNow;
                    project.LastGitHubPullCommit = commitSha;
                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            // 9. Store conflicts and needs review items for later resolution
            var allPendingItems = mergeResult.Conflicts.Values.Concat(mergeResult.NeedsReview).ToList();
            if (!previewOnly && allPendingItems.Any())
            {
                await StorePendingConflictsAsync(projectId, allPendingItems, commitSha);
            }

            return new GitHubPullResult(
                Success: true,
                ErrorMessage: null,
                EntriesApplied: mergeResult.ToApply.Count,
                EntriesAdded: mergeResult.ToAdd.Count,
                EntriesDeleted: mergeResult.ToDelete.Count,
                EntriesUnchanged: mergeResult.Unchanged,
                EntriesNeedsReview: mergeResult.NeedsReview.Count,
                Conflicts: mergeResult.Conflicts.Select(c => c.Value).Concat(mergeResult.NeedsReview).ToList(),
                CommitSha: commitSha,
                ProcessedFiles: githubFiles.Keys.ToList()
            );
        }
        catch (RateLimitExceededException ex)
        {
            var resetTime = ex.GetRetryAfterTimeSpan();
            var resetMessage = resetTime.TotalSeconds > 0
                ? $"GitHub API rate limit exceeded. Try again in {(int)resetTime.TotalMinutes} minutes."
                : "GitHub API rate limit exceeded. Please wait before retrying.";

            _logger.LogWarning(ex, "Rate limit exceeded for project {ProjectId}. Reset: {Reset}",
                projectId, ex.Reset);

            return CreateErrorResult(resetMessage);
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "Authorization failed for project {ProjectId}", projectId);
            return CreateErrorResult(
                "GitHub authorization failed. Please reconnect your GitHub account or check repository permissions.");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Repository or branch not found for project {ProjectId}", projectId);
            return CreateErrorResult(
                "Repository or branch not found. Please verify the repository exists and you have access.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling from GitHub for project {ProjectId}", projectId);
            return CreateErrorResult($"Error pulling from GitHub: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GitHubPullResult> ResolveConflictsAsync(
        int projectId, int userId, GitHubPullConflictResolutionRequest request)
    {
        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                return CreateErrorResult("Project not found");

            var applied = 0;
            var skipped = 0;

            // Use transaction to ensure atomicity
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var resolution in request.Resolutions)
                {
                    var key = await _db.ResourceKeys
                        .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == resolution.Key);

                    if (key == null && resolution.Resolution != "skip")
                    {
                        // Create key if needed
                        key = new ResourceKey
                        {
                            ProjectId = projectId,
                            KeyName = resolution.Key,
                            IsPlural = !string.IsNullOrEmpty(resolution.PluralForm)
                        };
                        _db.ResourceKeys.Add(key);
                        await _db.SaveChangesAsync();
                    }

                    switch (resolution.Resolution)
                    {
                        case "github":
                            // Get value from sync state (stored during preview)
                            var syncState = await _db.GitHubSyncStates.FirstOrDefaultAsync(s =>
                                s.ProjectId == projectId &&
                                s.KeyName == resolution.Key &&
                                s.LanguageCode == resolution.LanguageCode &&
                                s.PluralForm == resolution.PluralForm);

                            if (syncState != null && key != null)
                            {
                                await UpsertTranslationAsync(key.Id, resolution.LanguageCode, resolution.PluralForm,
                                    syncState.GitHubValue, syncState.GitHubComment, syncState.GitHubHash, userId);
                                applied++;
                            }
                            break;

                        case "cloud":
                            // Keep existing value, nothing to do
                            skipped++;
                            break;

                        case "edit":
                            if (key != null && resolution.EditedValue != null)
                            {
                                var hash = EntryHasher.ComputeHash(resolution.EditedValue, null);
                                await UpsertTranslationAsync(key.Id, resolution.LanguageCode, resolution.PluralForm,
                                    resolution.EditedValue, null, hash, userId);
                                applied++;
                            }
                            break;

                        case "skip":
                            skipped++;
                            break;
                    }
                }

                // Delete resolved conflicts from pending table
                await DeleteResolvedConflictsAsync(projectId, request.Resolutions);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new GitHubPullResult(
                Success: true,
                ErrorMessage: null,
                EntriesApplied: applied,
                EntriesAdded: 0,
                EntriesDeleted: 0,
                EntriesUnchanged: skipped,
                EntriesNeedsReview: 0,
                Conflicts: new List<GitHubPullConflict>(),
                CommitSha: null,
                ProcessedFiles: new List<string>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflicts for project {ProjectId}", projectId);
            return CreateErrorResult($"Error resolving conflicts: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GitHubPullConflictSummary> GetPendingConflictsAsync(int projectId)
    {
        var pendingConflicts = await _db.PendingConflicts
            .Where(pc => pc.ProjectId == projectId)
            .OrderBy(pc => pc.KeyName)
            .ThenBy(pc => pc.LanguageCode)
            .ThenBy(pc => pc.PluralForm)
            .ToListAsync();

        if (!pendingConflicts.Any())
        {
            return new GitHubPullConflictSummary(
                TotalConflicts: 0,
                BothModifiedCount: 0,
                DeletedInGitHubCount: 0,
                DeletedInCloudCount: 0,
                NeedsReviewCount: 0,
                Conflicts: new List<GitHubPullConflict>()
            );
        }

        var conflicts = pendingConflicts.Select(pc => new GitHubPullConflict(
            Key: pc.KeyName,
            LanguageCode: pc.LanguageCode,
            PluralForm: pc.PluralForm,
            ConflictType: pc.ConflictType,
            GitHubValue: pc.GitHubValue,
            CloudValue: pc.CloudValue,
            BaseValue: pc.BaseValue,
            CloudModifiedAt: pc.CloudModifiedAt,
            CloudModifiedBy: pc.CloudModifiedBy
        )).ToList();

        return new GitHubPullConflictSummary(
            TotalConflicts: conflicts.Count,
            BothModifiedCount: conflicts.Count(c => c.ConflictType == "BothModified"),
            DeletedInGitHubCount: conflicts.Count(c => c.ConflictType == "DeletedInGitHub"),
            DeletedInCloudCount: conflicts.Count(c => c.ConflictType == "DeletedInCloud"),
            NeedsReviewCount: conflicts.Count(c => c.ConflictType == "NeedsReview"),
            Conflicts: conflicts
        );
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private static GitHubPullResult CreateErrorResult(string message) =>
        new(false, message, 0, 0, 0, 0, 0, new List<GitHubPullConflict>(), null, new List<string>());

    /// <summary>
    /// Stores pending conflicts in the database for later resolution.
    /// </summary>
    private async Task StorePendingConflictsAsync(
        int projectId,
        IEnumerable<GitHubPullConflict> conflicts,
        string? commitSha)
    {
        // First, clear any existing conflicts for this project
        var existingConflicts = await _db.PendingConflicts
            .Where(pc => pc.ProjectId == projectId)
            .ToListAsync();
        _db.PendingConflicts.RemoveRange(existingConflicts);

        // Add new conflicts
        foreach (var conflict in conflicts)
        {
            _db.PendingConflicts.Add(new PendingConflict
            {
                ProjectId = projectId,
                KeyName = conflict.Key,
                LanguageCode = conflict.LanguageCode,
                PluralForm = conflict.PluralForm,
                ConflictType = conflict.ConflictType,
                GitHubValue = conflict.GitHubValue,
                CloudValue = conflict.CloudValue,
                BaseValue = conflict.BaseValue,
                CloudModifiedAt = conflict.CloudModifiedAt,
                CloudModifiedBy = conflict.CloudModifiedBy,
                CommitSha = commitSha,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Stored {ConflictCount} pending conflicts for project {ProjectId}",
            conflicts.Count(), projectId);
    }

    /// <summary>
    /// Deletes resolved conflicts from the pending table.
    /// </summary>
    private async Task DeleteResolvedConflictsAsync(
        int projectId,
        List<GitHubPullConflictResolution> resolutions)
    {
        var keysToDelete = resolutions
            .Select(r => (r.Key, r.LanguageCode, r.PluralForm))
            .ToHashSet();

        var conflictsToDelete = await _db.PendingConflicts
            .Where(pc => pc.ProjectId == projectId)
            .ToListAsync();

        conflictsToDelete = conflictsToDelete
            .Where(pc => keysToDelete.Contains((pc.KeyName, pc.LanguageCode, pc.PluralForm)))
            .ToList();

        if (conflictsToDelete.Any())
        {
            _db.PendingConflicts.RemoveRange(conflictsToDelete);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted {Count} resolved conflicts for project {ProjectId}",
                conflictsToDelete.Count, projectId);
        }
    }

    /// <summary>
    /// Fetches translation files from GitHub based on project format.
    /// </summary>
    private async Task<Dictionary<string, string>> FetchTranslationFilesAsync(
        int userId, string owner, string repo, string branch, string basePath, string format)
    {
        var files = new Dictionary<string, string>();
        var patterns = FilePatterns.GetValueOrDefault(format.ToLowerInvariant()) ?? Array.Empty<string>();

        // Get directory contents recursively
        var contents = await GetFilesRecursivelyAsync(userId, owner, repo, branch, basePath, format);

        foreach (var file in contents)
        {
            // Check if file matches the format patterns
            if (patterns.Any(p => file.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                var content = await _githubApi.GetFileContentAsync(userId, owner, repo, file, branch);
                if (!string.IsNullOrEmpty(content))
                {
                    files[file] = content;
                }
            }
        }

        _logger.LogInformation("Fetched {FileCount} translation files from GitHub", files.Count);
        return files;
    }

    /// <summary>
    /// Recursively gets file paths from GitHub.
    /// </summary>
    private async Task<List<string>> GetFilesRecursivelyAsync(
        int userId, string owner, string repo, string branch, string path, string format)
    {
        var files = new List<string>();

        try
        {
            var contents = await _githubApi.GetDirectoryContentsAsync(userId, owner, repo, path, branch);

            foreach (var item in contents)
            {
                if (item.Type == ContentType.File)
                {
                    files.Add(item.Path);
                }
                else if (item.Type == ContentType.Dir)
                {
                    // For Android, only descend into values* folders
                    if (format.Equals("android", StringComparison.OrdinalIgnoreCase))
                    {
                        var dirName = Path.GetFileName(item.Path);
                        if (dirName.StartsWith("values", StringComparison.OrdinalIgnoreCase))
                        {
                            var subFiles = await GetFilesRecursivelyAsync(userId, owner, repo, branch, item.Path, format);
                            files.AddRange(subFiles);
                        }
                    }
                    // For iOS, only descend into *.lproj folders
                    else if (format.Equals("ios", StringComparison.OrdinalIgnoreCase))
                    {
                        var dirName = Path.GetFileName(item.Path);
                        if (dirName.EndsWith(".lproj", StringComparison.OrdinalIgnoreCase))
                        {
                            var subFiles = await GetFilesRecursivelyAsync(userId, owner, repo, branch, item.Path, format);
                            files.AddRange(subFiles);
                        }
                    }
                    else
                    {
                        // For other formats, descend into all directories (limited depth)
                        var subFiles = await GetFilesRecursivelyAsync(userId, owner, repo, branch, item.Path, format);
                        files.AddRange(subFiles);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing directory {Path} in {Owner}/{Repo}", path, owner, repo);
        }

        return files;
    }

    /// <summary>
    /// Loads database entries with their hashes for comparison.
    /// </summary>
    private async Task<Dictionary<(string Key, string LanguageCode, string PluralForm), DbEntry>> LoadDbEntriesAsync(int projectId)
    {
        var result = new Dictionary<(string Key, string LanguageCode, string PluralForm), DbEntry>();

        var keys = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .Include(k => k.Translations)
            .AsNoTracking()
            .ToListAsync();

        foreach (var key in keys)
        {
            foreach (var translation in key.Translations)
            {
                result[(key.KeyName, translation.LanguageCode, translation.PluralForm)] = new DbEntry
                {
                    KeyId = key.Id,
                    KeyName = key.KeyName,
                    LanguageCode = translation.LanguageCode,
                    PluralForm = translation.PluralForm,
                    Value = translation.Value,
                    Comment = translation.Comment,
                    Hash = translation.Hash ?? "",
                    IsPlural = key.IsPlural,
                    UpdatedAt = translation.UpdatedAt
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Loads the GitHub sync state (base hashes for three-way merge).
    /// </summary>
    private async Task<Dictionary<(string Key, string LanguageCode, string PluralForm), string>> LoadGitHubSyncStateAsync(int projectId)
    {
        var states = await _db.GitHubSyncStates
            .Where(s => s.ProjectId == projectId)
            .AsNoTracking()
            .ToListAsync();

        return states.ToDictionary(
            s => (s.KeyName, s.LanguageCode, s.PluralForm),
            s => s.GitHubHash);
    }

    /// <summary>
    /// Performs three-way merge between GitHub entries, DB entries, and base sync state.
    /// Adapted from CLI KeyLevelMerger.MergeForPull logic.
    /// </summary>
    private MergeResult PerformThreeWayMerge(
        Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> github,
        Dictionary<(string Key, string LanguageCode, string PluralForm), DbEntry> db,
        Dictionary<(string Key, string LanguageCode, string PluralForm), string> baseHashes,
        string defaultLanguage)
    {
        var result = new MergeResult();

        // Get all unique keys across all sources
        var allKeys = github.Keys.Union(db.Keys).Union(baseHashes.Keys).ToHashSet();

        foreach (var key in allKeys)
        {
            var hasGitHub = github.TryGetValue(key, out var ghEntry);
            var hasDb = db.TryGetValue(key, out var dbEntry);
            var hasBase = baseHashes.TryGetValue(key, out var baseHash);

            if (hasGitHub && hasDb)
            {
                // Both exist
                if (ghEntry!.Hash == dbEntry!.Hash)
                {
                    // Same value - no change needed
                    result.Unchanged++;
                }
                else if (!hasBase)
                {
                    // No base - first sync, needs manual review (not a true conflict)
                    result.NeedsReview.Add(CreateConflict(key, "NeedsReview", ghEntry, dbEntry, null));
                }
                else if (baseHash == dbEntry.Hash)
                {
                    // Only GitHub changed - accept GitHub
                    result.ToApply.Add(ghEntry);
                }
                else if (baseHash == ghEntry.Hash)
                {
                    // Only DB changed - keep DB (no action needed)
                    result.Unchanged++;
                }
                else
                {
                    // Both changed - conflict
                    result.Conflicts[key] = CreateConflict(key, "BothModified", ghEntry, dbEntry, baseHash);
                }
            }
            else if (hasGitHub && !hasDb)
            {
                // Exists in GitHub only
                if (!hasBase)
                {
                    // New in GitHub - add
                    result.ToAdd.Add(ghEntry!);
                }
                else
                {
                    // Was in base but now deleted in DB - conflict (deleted in cloud)
                    result.Conflicts[key] = CreateConflict(key, "DeletedInCloud", ghEntry!, null, baseHash);
                }
            }
            else if (!hasGitHub && hasDb)
            {
                // Exists in DB only
                if (!hasBase)
                {
                    // New in DB only - keep (no action needed)
                    result.Unchanged++;
                }
                else if (baseHash == dbEntry!.Hash)
                {
                    // Deleted in GitHub, unchanged in DB - delete
                    result.ToDelete.Add(key);
                }
                else
                {
                    // Deleted in GitHub but modified in DB - conflict
                    result.Conflicts[key] = CreateConflict(key, "DeletedInGitHub", null, dbEntry, baseHash);
                }
            }
            // If neither has it, nothing to do (was deleted from both)
        }

        _logger.LogInformation(
            "Merge result: {Apply} to apply, {Add} to add, {Delete} to delete, {Unchanged} unchanged, {Conflicts} conflicts, {NeedsReview} needs review",
            result.ToApply.Count, result.ToAdd.Count, result.ToDelete.Count, result.Unchanged, result.Conflicts.Count, result.NeedsReview.Count);

        return result;
    }

    private static GitHubPullConflict CreateConflict(
        (string Key, string LanguageCode, string PluralForm) key,
        string conflictType,
        GitHubEntry? githubEntry,
        DbEntry? dbEntry,
        string? baseHash)
    {
        return new GitHubPullConflict(
            Key: key.Key,
            LanguageCode: key.LanguageCode,
            PluralForm: key.PluralForm,
            ConflictType: conflictType,
            GitHubValue: githubEntry?.Value,
            CloudValue: dbEntry?.Value,
            BaseValue: null, // Could be populated if we stored base values
            CloudModifiedAt: dbEntry?.UpdatedAt,
            CloudModifiedBy: null // Would need to track who modified
        );
    }

    /// <summary>
    /// Applies merge changes to the database.
    /// </summary>
    private async Task ApplyChangesAsync(int projectId, MergeResult result, int userId, string defaultLanguage)
    {
        // Apply updates and additions
        foreach (var entry in result.ToApply.Concat(result.ToAdd))
        {
            // Find or create key
            var key = await _db.ResourceKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == entry.Key);

            if (key == null)
            {
                key = new ResourceKey
                {
                    ProjectId = projectId,
                    KeyName = entry.Key,
                    IsPlural = entry.IsPlural,
                    Comment = entry.Comment,
                    // For plural keys, store the source plural text (PO msgid_plural or "other" form)
                    SourcePluralText = entry.IsPlural ? entry.SourcePluralText : null
                };
                _db.ResourceKeys.Add(key);
                await _db.SaveChangesAsync();
            }
            else if (entry.IsPlural && key.SourcePluralText == null && entry.SourcePluralText != null)
            {
                // Update SourcePluralText if not set yet
                key.SourcePluralText = entry.SourcePluralText;
            }

            // Upsert translation with "pending" status
            await UpsertTranslationAsync(
                key.Id,
                entry.LanguageCode,
                entry.PluralForm,
                entry.Value,
                entry.Comment,
                entry.Hash,
                userId);
        }

        // Apply deletions
        foreach (var key in result.ToDelete)
        {
            var resourceKey = await _db.ResourceKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == key.Key);

            if (resourceKey != null)
            {
                var translation = await _db.Translations
                    .FirstOrDefaultAsync(t =>
                        t.ResourceKeyId == resourceKey.Id &&
                        t.LanguageCode == key.LanguageCode &&
                        t.PluralForm == key.PluralForm);

                if (translation != null)
                {
                    _db.Translations.Remove(translation);
                }
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Applied {Updates} updates, {Adds} additions, {Deletes} deletions",
            result.ToApply.Count, result.ToAdd.Count, result.ToDelete.Count);
    }

    /// <summary>
    /// Upserts a translation with "pending" status for pulled content.
    /// </summary>
    private async Task UpsertTranslationAsync(
        int keyId, string languageCode, string pluralForm,
        string? value, string? comment, string hash, int userId)
    {
        var existing = await _db.Translations
            .FirstOrDefaultAsync(t =>
                t.ResourceKeyId == keyId &&
                t.LanguageCode == languageCode &&
                t.PluralForm == pluralForm);

        if (existing != null)
        {
            existing.Value = value;
            existing.Comment = comment;
            existing.Hash = hash;
            existing.Status = "pending";
            existing.TranslatedBy = "github:pull";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Version++;  // Increment for optimistic locking
        }
        else
        {
            var translation = new LrmCloud.Shared.Entities.Translation
            {
                ResourceKeyId = keyId,
                LanguageCode = languageCode,
                PluralForm = pluralForm,
                Value = value,
                Comment = comment,
                Hash = hash,
                Status = "pending",
                TranslatedBy = "github:pull"
            };
            _db.Translations.Add(translation);
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the GitHub sync state after a successful pull.
    /// </summary>
    private async Task UpdateGitHubSyncStateAsync(
        int projectId,
        Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> githubEntries,
        string? commitSha)
    {
        // Bulk load existing sync states to avoid N+1 queries
        var existingStates = await _db.GitHubSyncStates
            .Where(s => s.ProjectId == projectId)
            .ToDictionaryAsync(s => (s.KeyName, s.LanguageCode, s.PluralForm));

        foreach (var (key, entry) in githubEntries)
        {
            var stateKey = (key.Key, key.LanguageCode, key.PluralForm);

            if (existingStates.TryGetValue(stateKey, out var syncState))
            {
                // Update existing state
                syncState.GitHubHash = entry.Hash;
                syncState.GitHubValue = entry.Value;
                syncState.GitHubComment = entry.Comment;
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
                    KeyName = key.Key,
                    LanguageCode = key.LanguageCode,
                    PluralForm = key.PluralForm,
                    GitHubHash = entry.Hash,
                    GitHubValue = entry.Value,
                    GitHubComment = entry.Comment,
                    GitHubCommitSha = commitSha,
                    SyncedAt = DateTime.UtcNow
                };
                _db.GitHubSyncStates.Add(syncState);
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Updated GitHub sync state for {Count} entries", githubEntries.Count);
    }

    // ============================================================================
    // Internal Types
    // ============================================================================

    private class DbEntry
    {
        public int KeyId { get; init; }
        public required string KeyName { get; init; }
        public required string LanguageCode { get; init; }
        public string PluralForm { get; init; } = "";
        public string? Value { get; init; }
        public string? Comment { get; init; }
        public required string Hash { get; init; }
        public bool IsPlural { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private class MergeResult
    {
        public List<GitHubEntry> ToApply { get; } = new();
        public List<GitHubEntry> ToAdd { get; } = new();
        public List<(string Key, string LanguageCode, string PluralForm)> ToDelete { get; } = new();
        public Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubPullConflict> Conflicts { get; } = new();
        public List<GitHubPullConflict> NeedsReview { get; } = new();
        public int Unchanged { get; set; }
    }
}
