using LrmCloud.Api.Data;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Resources;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

public class ResourceService : IResourceService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ResourceSyncService _syncService;
    private readonly SnapshotService _snapshotService;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        AppDbContext db,
        IProjectService projectService,
        ResourceSyncService syncService,
        SnapshotService snapshotService,
        ILogger<ResourceService> logger)
    {
        _db = db;
        _projectService = projectService;
        _syncService = syncService;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    // ============================================================
    // Resource Keys
    // ============================================================

    public async Task<List<ResourceKeyDto>> GetResourceKeysAsync(int projectId, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new List<ResourceKeyDto>();
        }

        var keys = await _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId)
            .OrderBy(k => k.KeyName)
            .ToListAsync();

        return keys.Select(MapToResourceKeyDto).ToList();
    }

    public async Task<ResourceKeyDetailDto?> GetResourceKeyAsync(int projectId, string keyName, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return null;
        }

        var key = await _db.ResourceKeys
            .Include(k => k.Translations)
            .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyName);

        if (key == null)
        {
            return null;
        }

        return MapToResourceKeyDetailDto(key);
    }

    public async Task<PagedResult<ResourceKeyDetailDto>> GetResourceKeysPagedAsync(
        int projectId,
        int userId,
        int page,
        int pageSize,
        string? search = null,
        string? sortBy = null,
        bool sortDescending = false)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new PagedResult<ResourceKeyDetailDto> { Page = page, PageSize = pageSize };
        }

        // Build query
        var query = _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(k =>
                k.KeyName.ToLower().Contains(searchLower) ||
                (k.Comment != null && k.Comment.ToLower().Contains(searchLower)) ||
                k.Translations.Any(t => t.Value != null && t.Value.ToLower().Contains(searchLower)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "keyname" => sortDescending
                ? query.OrderByDescending(k => k.KeyName)
                : query.OrderBy(k => k.KeyName),
            "updatedat" => sortDescending
                ? query.OrderByDescending(k => k.UpdatedAt)
                : query.OrderBy(k => k.UpdatedAt),
            "createdat" => sortDescending
                ? query.OrderByDescending(k => k.CreatedAt)
                : query.OrderBy(k => k.CreatedAt),
            _ => query.OrderBy(k => k.KeyName) // Default sort
        };

        // Apply pagination
        var keys = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ResourceKeyDetailDto>
        {
            Items = keys.Select(MapToResourceKeyDetailDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> CreateResourceKeyAsync(
        int projectId, int userId, CreateResourceKeyRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to manage resources in this project");
            }

            // Check if key already exists
            var exists = await _db.ResourceKeys
                .AnyAsync(k => k.ProjectId == projectId && k.KeyName == request.KeyName);

            if (exists)
            {
                return (false, null, $"Resource key '{request.KeyName}' already exists");
            }

            // Create resource key
            var resourceKey = new ResourceKey
            {
                ProjectId = projectId,
                KeyName = request.KeyName,
                KeyPath = request.KeyPath,
                IsPlural = request.IsPlural,
                Comment = request.Comment,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ResourceKeys.Add(resourceKey);
            await _db.SaveChangesAsync();

            // Create default language translation if provided
            if (!string.IsNullOrWhiteSpace(request.DefaultLanguageValue))
            {
                var project = await _db.Projects.FindAsync(projectId);
                if (project != null)
                {
                    var translation = new Shared.Entities.Translation
                    {
                        ResourceKeyId = resourceKey.Id,
                        LanguageCode = project.DefaultLanguage,
                        Value = request.DefaultLanguageValue,
                        PluralForm = "",
                        Status = TranslationStatus.Translated,
                        Version = 1,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _db.Translations.Add(translation);
                    await _db.SaveChangesAsync();

                    // Reload to include the translation
                    await _db.Entry(resourceKey).Collection(k => k.Translations).LoadAsync();
                }
            }

            _logger.LogInformation("Resource key {KeyName} created in project {ProjectId} by user {UserId}",
                request.KeyName, projectId, userId);

            var dto = MapToResourceKeyDto(resourceKey);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resource key in project {ProjectId}", projectId);
            return (false, null, "An error occurred while creating the resource key");
        }
    }

    public async Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> UpdateResourceKeyAsync(
        int projectId, string keyName, int userId, UpdateResourceKeyRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to manage resources in this project");
            }

            var resourceKey = await _db.ResourceKeys
                .Include(k => k.Translations)
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyName);

            if (resourceKey == null)
            {
                return (false, null, "Resource key not found");
            }

            // Update fields
            if (request.KeyPath != null)
            {
                resourceKey.KeyPath = request.KeyPath;
            }

            if (request.IsPlural.HasValue)
            {
                resourceKey.IsPlural = request.IsPlural.Value;
            }

            if (request.Comment != null)
            {
                resourceKey.Comment = request.Comment;
            }

            resourceKey.UpdatedAt = DateTime.UtcNow;
            resourceKey.Version++;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Resource key {KeyName} updated in project {ProjectId} by user {UserId}",
                keyName, projectId, userId);

            var dto = MapToResourceKeyDto(resourceKey);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating resource key {KeyName} in project {ProjectId}", keyName, projectId);
            return (false, null, "An error occurred while updating the resource key");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteResourceKeyAsync(
        int projectId, string keyName, int userId)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, "You don't have permission to manage resources in this project");
            }

            var resourceKey = await _db.ResourceKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyName);

            if (resourceKey == null)
            {
                return (false, "Resource key not found");
            }

            // Hard delete (cascades to translations)
            _db.ResourceKeys.Remove(resourceKey);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Resource key {KeyName} deleted from project {ProjectId} by user {UserId}",
                keyName, projectId, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource key {KeyName} from project {ProjectId}", keyName, projectId);
            return (false, "An error occurred while deleting the resource key");
        }
    }

    // ============================================================
    // Translations
    // ============================================================

    public async Task<(bool Success, TranslationDto? Translation, string? ErrorMessage)> UpdateTranslationAsync(
        int projectId, string keyName, string languageCode, int userId, UpdateTranslationRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to manage resources in this project");
            }

            // Get resource key
            var resourceKey = await _db.ResourceKeys
                .Include(k => k.Translations)
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyName);

            if (resourceKey == null)
            {
                return (false, null, "Resource key not found");
            }

            // Validate status
            if (!TranslationStatus.All.Contains(request.Status))
            {
                return (false, null, $"Invalid status. Must be one of: {string.Join(", ", TranslationStatus.All)}");
            }

            // Find existing translation
            var translation = resourceKey.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode && t.PluralForm == request.PluralForm);

            if (translation == null)
            {
                // Create new translation
                translation = new Shared.Entities.Translation
                {
                    ResourceKeyId = resourceKey.Id,
                    LanguageCode = languageCode,
                    Value = request.Value,
                    PluralForm = request.PluralForm,
                    Status = request.Status,
                    Version = 1,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.Translations.Add(translation);
            }
            else
            {
                // Optimistic locking check
                if (request.Version.HasValue && translation.Version != request.Version.Value)
                {
                    return (false, null, "Translation has been modified by another user. Please refresh and try again.");
                }

                // Update existing translation
                translation.Value = request.Value;
                translation.Status = request.Status;
                translation.UpdatedAt = DateTime.UtcNow;
                translation.Version++;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Translation updated for key {KeyName}, language {LanguageCode} in project {ProjectId} by user {UserId}",
                keyName, languageCode, projectId, userId);

            var dto = MapToTranslationDto(translation);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating translation for key {KeyName}, language {LanguageCode} in project {ProjectId}",
                keyName, languageCode, projectId);
            return (false, null, "An error occurred while updating the translation");
        }
    }

    public async Task<(bool Success, int UpdatedCount, string? ErrorMessage)> BulkUpdateTranslationsAsync(
        int projectId, string languageCode, int userId, List<BulkTranslationUpdate> updates)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, 0, "You don't have permission to manage resources in this project");
            }

            var updatedCount = 0;

            foreach (var update in updates)
            {
                // Get resource key
                var resourceKey = await _db.ResourceKeys
                    .Include(k => k.Translations)
                    .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == update.KeyName);

                if (resourceKey == null)
                {
                    _logger.LogWarning("Resource key {KeyName} not found in bulk update", update.KeyName);
                    continue;
                }

                // Validate status
                if (!TranslationStatus.All.Contains(update.Status))
                {
                    _logger.LogWarning("Invalid status {Status} for key {KeyName} in bulk update", update.Status, update.KeyName);
                    continue;
                }

                // Find or create translation
                var translation = resourceKey.Translations
                    .FirstOrDefault(t => t.LanguageCode == languageCode && t.PluralForm == update.PluralForm);

                if (translation == null)
                {
                    translation = new Shared.Entities.Translation
                    {
                        ResourceKeyId = resourceKey.Id,
                        LanguageCode = languageCode,
                        Value = update.Value,
                        PluralForm = update.PluralForm,
                        Status = update.Status,
                        Version = 1,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _db.Translations.Add(translation);
                }
                else
                {
                    translation.Value = update.Value;
                    translation.Status = update.Status;
                    translation.UpdatedAt = DateTime.UtcNow;
                    translation.Version++;
                }

                updatedCount++;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Bulk updated {Count} translations for language {LanguageCode} in project {ProjectId} by user {UserId}",
                updatedCount, languageCode, projectId, userId);

            return (true, updatedCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error bulk updating translations for language {LanguageCode} in project {ProjectId}",
                languageCode, projectId);
            return (false, 0, "An error occurred while bulk updating translations");
        }
    }

    // ============================================================
    // Stats & Validation
    // ============================================================

    public async Task<ProjectStatsDto> GetProjectStatsAsync(int projectId, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new ProjectStatsDto();
        }

        var keys = await _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId)
            .ToListAsync();

        var stats = new ProjectStatsDto
        {
            TotalKeys = keys.Count
        };

        // Get all unique languages from translations
        var allLanguages = keys
            .SelectMany(k => k.Translations)
            .Select(t => t.LanguageCode)
            .Distinct()
            .ToList();

        foreach (var languageCode in allLanguages)
        {
            // Count keys that have a translation for this language
            // For plural keys: count as translated if at least one plural form has a value
            // For non-plural keys: count as translated if the value is non-empty
            var translatedKeysCount = 0;
            var pendingKeysCount = 0;

            foreach (var key in keys)
            {
                var translationsForLang = key.Translations
                    .Where(t => t.LanguageCode == languageCode)
                    .ToList();

                if (translationsForLang.Count == 0)
                {
                    pendingKeysCount++;
                    continue;
                }

                // A key is considered translated if it has at least one non-empty value
                var hasValue = translationsForLang.Any(t => !string.IsNullOrWhiteSpace(t.Value));
                if (hasValue)
                {
                    translatedKeysCount++;
                }
                else
                {
                    pendingKeysCount++;
                }
            }

            var languageStats = new LanguageStats
            {
                LanguageCode = languageCode,
                TranslatedCount = translatedKeysCount,
                PendingCount = pendingKeysCount,
                ReviewedCount = 0, // TODO: track review status per key, not per translation
                ApprovedCount = 0,
                CompletionPercentage = keys.Count > 0
                    ? Math.Round((double)translatedKeysCount / keys.Count * 100, 2)
                    : 0
            };

            stats.Languages[languageCode] = languageStats;
        }

        // Calculate overall completion
        if (stats.Languages.Count > 0)
        {
            stats.OverallCompletion = Math.Round(
                stats.Languages.Values.Average(l => l.CompletionPercentage), 2);
        }

        return stats;
    }

    public async Task<ValidationResultDto> ValidateProjectAsync(int projectId, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new ValidationResultDto { IsValid = false };
        }

        var result = new ValidationResultDto { IsValid = true };

        var project = await _db.Projects
            .Include(p => p.ResourceKeys)
                .ThenInclude(k => k.Translations)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Project not found"
            });
            return result;
        }

        // Get all languages in the project
        var languages = project.ResourceKeys
            .SelectMany(k => k.Translations)
            .Select(t => t.LanguageCode)
            .Distinct()
            .ToList();

        // Check each resource key
        foreach (var key in project.ResourceKeys)
        {
            // Check for empty key names
            if (string.IsNullOrWhiteSpace(key.KeyName))
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    Message = "Resource key has empty name",
                    KeyName = key.KeyName
                });
            }

            // Check for missing translations
            foreach (var language in languages)
            {
                var translation = key.Translations.FirstOrDefault(t => t.LanguageCode == language);
                if (translation == null || string.IsNullOrWhiteSpace(translation.Value))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = "warning",
                        Message = $"Missing translation for language '{language}'",
                        KeyName = key.KeyName,
                        LanguageCode = language
                    });
                }
            }

            // Check for pending translations
            var pendingCount = key.Translations.Count(t => t.Status == TranslationStatus.Pending);
            if (pendingCount > 0)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = "info",
                    Message = $"{pendingCount} translation(s) pending review",
                    KeyName = key.KeyName
                });
            }
        }

        // Check for duplicate keys (should not happen, but validate anyway)
        var duplicateKeys = project.ResourceKeys
            .GroupBy(k => k.KeyName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dupKey in duplicateKeys)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Duplicate resource key found",
                KeyName = dupKey
            });
        }

        return result;
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private ResourceKeyDto MapToResourceKeyDto(ResourceKey key)
    {
        return new ResourceKeyDto
        {
            Id = key.Id,
            KeyName = key.KeyName,
            KeyPath = key.KeyPath,
            IsPlural = key.IsPlural,
            Comment = key.Comment,
            Version = key.Version,
            TranslationCount = key.Translations.Count,
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt
        };
    }

    private ResourceKeyDetailDto MapToResourceKeyDetailDto(ResourceKey key)
    {
        return new ResourceKeyDetailDto
        {
            Id = key.Id,
            KeyName = key.KeyName,
            KeyPath = key.KeyPath,
            IsPlural = key.IsPlural,
            Comment = key.Comment,
            Version = key.Version,
            TranslationCount = key.Translations.Count,
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt,
            Translations = key.Translations.Select(MapToTranslationDto).ToList()
        };
    }

    private TranslationDto MapToTranslationDto(Shared.Entities.Translation translation)
    {
        return new TranslationDto
        {
            Id = translation.Id,
            LanguageCode = translation.LanguageCode,
            Value = translation.Value,
            PluralForm = translation.PluralForm,
            Status = translation.Status,
            TranslatedBy = null, // TODO: Phase 3 - track user who translated
            ReviewedBy = null,   // TODO: Phase 3 - track reviewer
            Version = translation.Version,
            UpdatedAt = translation.UpdatedAt
        };
    }

    // ============================================================
    // CLI Sync Operations
    // ============================================================

    public async Task<List<ResourceDto>> GetResourcesAsync(int projectId, string? languageCode, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new List<ResourceDto>();
        }

        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return new List<ResourceDto>();
        }

        // Get resource keys with translations
        var query = _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId);

        var keys = await query.ToListAsync();

        // Convert to resource files (JSON format)
        var resources = new List<ResourceDto>();

        // Group translations by language
        var languageGroups = keys
            .SelectMany(k => k.Translations)
            .Where(t => languageCode == null || t.LanguageCode == languageCode)
            .GroupBy(t => t.LanguageCode);

        foreach (var langGroup in languageGroups)
        {
            var lang = langGroup.Key;
            var translationsDict = new Dictionary<string, string>();

            // Build translations dictionary
            foreach (var translation in langGroup)
            {
                var key = keys.FirstOrDefault(k => k.Id == translation.ResourceKeyId);
                if (key != null && !string.IsNullOrWhiteSpace(translation.Value))
                {
                    translationsDict[key.KeyName] = translation.Value;
                }
            }

            if (translationsDict.Any())
            {
                // Serialize to JSON
                var content = System.Text.Json.JsonSerializer.Serialize(translationsDict, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                resources.Add(new ResourceDto
                {
                    Path = $"strings.{lang}.json",
                    Content = content,
                    LanguageCode = lang
                });
            }
        }

        return resources;
    }

    // ============================================================
    // NEW: File-based Cloud Sync (using Core backends)
    // ============================================================

    /// <summary>
    /// Pushes resources using file-based sync with Core backends.
    /// Supports incremental sync (modified + deleted files).
    /// </summary>
    public async Task<(bool Success, PushResponse? Response, string? ErrorMessage)> PushResourcesAsync(
        int projectId, int userId, PushRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to push resources to this project");
            }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
            {
                return (false, null, "Project not found");
            }

            // Validate file naming consistency with project format
            if (request.ModifiedFiles.Count > 0)
            {
                var config = project.ConfigJson != null
                    ? System.Text.Json.JsonSerializer.Deserialize<LocalizationManager.Core.Configuration.ConfigurationModel>(project.ConfigJson)
                    : null;

                var (isValid, errorMessage) = _syncService.ValidateFileNamingConsistency(
                    request.ModifiedFiles, project.Format, config);

                if (!isValid)
                {
                    return (false, null, errorMessage);
                }
            }

            // Snapshot will be created after files are uploaded
            Snapshot? snapshot = null;

            // Update configuration if provided
            if (request.Configuration != null)
            {
                project.ConfigJson = request.Configuration;
                project.ConfigUpdatedAt = DateTime.UtcNow;
                project.ConfigUpdatedBy = userId;
            }

            // Handle deleted files - remove languages from database
            if (request.DeletedFiles.Count > 0)
            {
                var config = project.ConfigJson != null
                    ? System.Text.Json.JsonSerializer.Deserialize<LocalizationManager.Core.Configuration.ConfigurationModel>(project.ConfigJson)
                    : new LocalizationManager.Core.Configuration.ConfigurationModel();

                foreach (var deletedPath in request.DeletedFiles)
                {
                    // Extract language code from deleted file path
                    var langCode = ExtractLanguageCodeFromPath(deletedPath, config!);
                    await _syncService.DeleteLanguageTranslationsAsync(projectId, langCode);
                }
            }

            // Store modified files to S3/Minio
            if (request.ModifiedFiles.Count > 0)
            {
                await _syncService.StoreUploadedFilesAsync(projectId, request.ModifiedFiles);
            }

            // Create snapshot after files are uploaded to current/ folder
            try
            {
                var description = request.Message ?? "Push from CLI";
                snapshot = await _snapshotService.CreateSnapshotAsync(projectId, userId, "push", description);
                _logger.LogInformation("Created snapshot {SnapshotId} after push for project {ProjectId}",
                    snapshot.SnapshotId, projectId);
            }
            catch (Exception ex)
            {
                // Log but don't fail the push if snapshot creation fails
                _logger.LogWarning(ex, "Failed to create snapshot after push for project {ProjectId}", projectId);
            }

            // Parse modified files to database
            if (request.ModifiedFiles.Count > 0)
            {
                await _syncService.ParseFilesToDatabaseAsync(projectId, request.ModifiedFiles, project.ConfigJson, project.Format);
            }

            // Update project last sync time
            project.LastSyncedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Record sync history
            _db.SyncHistory.Add(new SyncHistory
            {
                ProjectId = projectId,
                SyncType = "push",
                Direction = "to_cloud",
                Status = "completed",
                Message = request.Message,
                SnapshotId = snapshot?.SnapshotId,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "User {UserId} pushed {Modified} modified and {Deleted} deleted files to project {ProjectId}",
                userId, request.ModifiedFiles.Count, request.DeletedFiles.Count, projectId);

            return (true, new PushResponse
            {
                Success = true,
                ModifiedCount = request.ModifiedFiles.Count,
                DeletedCount = request.DeletedFiles.Count,
                Message = $"Successfully pushed {request.ModifiedFiles.Count} modified and {request.DeletedFiles.Count} deleted files"
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing resources to project {ProjectId}", projectId);
            return (false, null, $"An error occurred while pushing resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Pulls resources using file-based sync with Core backends.
    /// </summary>
    public async Task<(bool Success, PullResponse? Response, string? ErrorMessage)> PullResourcesAsync(
        int projectId, int userId)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanViewProjectAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to view this project");
            }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
            {
                return (false, null, "Project not found");
            }

            // Generate files from database using Core's backends
            var files = await _syncService.GenerateFilesFromDatabaseAsync(
                projectId, project.ConfigJson, project.Format, project.DefaultLanguage);

            _logger.LogInformation("User {UserId} pulled {Count} files from project {ProjectId}",
                userId, files.Count, projectId);

            return (true, new PullResponse
            {
                Configuration = project.ConfigJson,
                Files = files
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling resources from project {ProjectId}", projectId);
            return (false, null, $"An error occurred while pulling resources: {ex.Message}");
        }
    }

    private string ExtractLanguageCodeFromPath(string filePath, LocalizationManager.Core.Configuration.ConfigurationModel config)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

        // RESX format: Resources.el.resx → "el", Resources.resx → default language
        if (config.ResourceFormat == "resx")
        {
            var parts = fileName.Split('.');
            if (parts.Length > 1)
            {
                return parts[^1];
            }
            return config.DefaultLanguageCode ?? "en";
        }

        // JSON i18next format: el.json → "el"
        if (config.Json?.I18nextCompatible == true)
        {
            return fileName;
        }

        // JSON standard format: strings.el.json → "el", strings.json → default language
        var jsonParts = fileName.Split('.');
        if (jsonParts.Length > 1)
        {
            return jsonParts[^1];
        }

        return config.DefaultLanguageCode ?? "en";
    }

    // ============================================================
    // Language Management
    // ============================================================

    public async Task<List<ProjectLanguageDto>> GetProjectLanguagesAsync(int projectId, int userId)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new List<ProjectLanguageDto>();
        }

        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return new List<ProjectLanguageDto>();
        }

        var totalKeys = await _db.ResourceKeys.CountAsync(k => k.ProjectId == projectId);

        // Get all translations grouped by language
        // For plural keys, each form counts as a separate translation slot
        var languageData = await _db.Translations
            .Where(t => t.ResourceKey!.ProjectId == projectId)
            .GroupBy(t => t.LanguageCode)
            .Select(g => new
            {
                LanguageCode = g.Key,
                TotalCount = g.Count(),
                TranslatedCount = g.Count(t => !string.IsNullOrWhiteSpace(t.Value)),
                LastUpdated = g.Max(t => (DateTime?)t.UpdatedAt)
            })
            .ToListAsync();

        return languageData.Select(l => new ProjectLanguageDto
        {
            LanguageCode = l.LanguageCode,
            DisplayName = GetLanguageDisplayName(l.LanguageCode),
            IsDefault = l.LanguageCode == project.DefaultLanguage,
            TranslatedCount = l.TranslatedCount,
            TotalKeys = totalKeys,
            CompletionPercentage = l.TotalCount > 0 ? Math.Round((double)l.TranslatedCount / l.TotalCount * 100, 1) : 0,
            LastUpdated = l.LastUpdated
        }).OrderBy(l => !l.IsDefault).ThenBy(l => l.LanguageCode).ToList();
    }

    public async Task<(bool Success, ProjectLanguageDto? Language, string? ErrorMessage)> AddLanguageAsync(
        int projectId, int userId, AddLanguageRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to manage languages in this project");
            }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
            {
                return (false, null, "Project not found");
            }

            // Check if language already exists
            var exists = await _db.Translations
                .AnyAsync(t => t.ResourceKey!.ProjectId == projectId && t.LanguageCode == request.LanguageCode);

            if (exists)
            {
                return (false, null, $"Language '{request.LanguageCode}' already exists in this project");
            }

            // Get all resource keys
            var resourceKeys = await _db.ResourceKeys
                .Where(k => k.ProjectId == projectId)
                .ToListAsync();

            if (resourceKeys.Count == 0)
            {
                // No keys yet, just return success with empty language
                return (true, new ProjectLanguageDto
                {
                    LanguageCode = request.LanguageCode,
                    DisplayName = GetLanguageDisplayName(request.LanguageCode),
                    IsDefault = false,
                    TranslatedCount = 0,
                    TotalKeys = 0,
                    CompletionPercentage = 0,
                    LastUpdated = null
                }, null);
            }

            // Create empty translations for the new language
            var now = DateTime.UtcNow;
            foreach (var key in resourceKeys)
            {
                _db.Translations.Add(new Shared.Entities.Translation
                {
                    ResourceKeyId = key.Id,
                    LanguageCode = request.LanguageCode,
                    Value = "",
                    Status = TranslationStatus.Pending,
                    Version = 1,
                    UpdatedAt = now
                });
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} added language {LanguageCode} to project {ProjectId}",
                userId, request.LanguageCode, projectId);

            return (true, new ProjectLanguageDto
            {
                LanguageCode = request.LanguageCode,
                DisplayName = GetLanguageDisplayName(request.LanguageCode),
                IsDefault = false,
                TranslatedCount = 0,
                TotalKeys = resourceKeys.Count,
                CompletionPercentage = 0,
                LastUpdated = now
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding language {LanguageCode} to project {ProjectId}",
                request.LanguageCode, projectId);
            return (false, null, "An error occurred while adding the language");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RemoveLanguageAsync(
        int projectId, int userId, RemoveLanguageRequest request)
    {
        try
        {
            // Check permission
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                return (false, "You don't have permission to manage languages in this project");
            }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
            {
                return (false, "Project not found");
            }

            // Don't allow removing the default language
            if (request.LanguageCode == project.DefaultLanguage)
            {
                return (false, "Cannot remove the default language. Change the default language first.");
            }

            // Require confirmation
            if (!request.ConfirmDelete)
            {
                return (false, "Please confirm deletion. This will permanently remove all translations for this language.");
            }

            // Get all translations for this language
            var translations = await _db.Translations
                .Where(t => t.ResourceKey!.ProjectId == projectId && t.LanguageCode == request.LanguageCode)
                .ToListAsync();

            if (translations.Count == 0)
            {
                return (false, $"Language '{request.LanguageCode}' not found in this project");
            }

            // Delete all translations
            _db.Translations.RemoveRange(translations);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} removed language {LanguageCode} from project {ProjectId} ({Count} translations deleted)",
                userId, request.LanguageCode, projectId, translations.Count);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing language {LanguageCode} from project {ProjectId}",
                request.LanguageCode, projectId);
            return (false, "An error occurred while removing the language");
        }
    }

    private static string GetLanguageDisplayName(string languageCode)
    {
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(languageCode);
            return culture.EnglishName;
        }
        catch
        {
            return languageCode;
        }
    }
}
