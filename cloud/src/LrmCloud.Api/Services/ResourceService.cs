using LrmCloud.Api.Data;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Resources;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using LocalizationManager.Core.Validation;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LrmCloud.Api.Services;

public class ResourceService : IResourceService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ISyncHistoryService _historyService;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        AppDbContext db,
        IProjectService projectService,
        ISyncHistoryService historyService,
        ILogger<ResourceService> logger)
    {
        _db = db;
        _projectService = projectService;
        _historyService = historyService;
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

        // Get project for default language resolution
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
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

        return MapToResourceKeyDetailDto(key, project.DefaultLanguage);
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

        // Get project for default language resolution
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
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
            Items = keys.Select(k => MapToResourceKeyDetailDto(k, project.DefaultLanguage)).ToList(),
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
                    Comment = request.Comment,
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
                translation.Comment = request.Comment;
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

        // Get project for default language resolution
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
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
        // Resolve empty string to project's default language for display
        var allLanguages = keys
            .SelectMany(k => k.Translations)
            .Select(t => string.IsNullOrEmpty(t.LanguageCode) ? project.DefaultLanguage : t.LanguageCode)
            .Distinct()
            .ToList();

        foreach (var languageCode in allLanguages)
        {
            // Count keys that have a translation for this language
            // For plural keys: count as translated if at least one plural form has a value
            // For non-plural keys: count as translated if the value is non-empty
            var translatedKeysCount = 0;
            var pendingKeysCount = 0;

            // Check if this is the resolved default language - need to also match empty string in DB
            var isDefaultLang = languageCode == project.DefaultLanguage;

            foreach (var key in keys)
            {
                // Match translations: exact language code OR empty string if this is default language
                var translationsForLang = key.Translations
                    .Where(t => t.LanguageCode == languageCode ||
                                (isDefaultLang && string.IsNullOrEmpty(t.LanguageCode)))
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
        return await ValidateProjectAsync(projectId, userId, forceRefresh: false);
    }

    public async Task<ValidationResultDto> ValidateProjectAsync(int projectId, int userId, bool forceRefresh)
    {
        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            return new ValidationResultDto { IsValid = false };
        }

        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            var errorResult = new ValidationResultDto { IsValid = false };
            errorResult.Issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Project not found",
                Category = ValidationCategory.Other
            });
            return errorResult;
        }

        // Check cache first (unless force refresh)
        if (!forceRefresh && project.ValidationCacheJson != null && project.ValidationCachedAt != null)
        {
            // Consider cache valid for 5 minutes
            var cacheAge = DateTime.UtcNow - project.ValidationCachedAt.Value;
            if (cacheAge.TotalMinutes < 5)
            {
                try
                {
                    var cachedResult = JsonSerializer.Deserialize<ValidationResultDto>(project.ValidationCacheJson);
                    if (cachedResult != null)
                    {
                        cachedResult.IsCached = true;
                        cachedResult.ValidatedAt = project.ValidationCachedAt;
                        return cachedResult;
                    }
                }
                catch
                {
                    // Cache corrupted, recompute
                }
            }
        }

        // Compute fresh validation
        var result = await ComputeValidationAsync(project);

        // Cache the result
        try
        {
            project.ValidationCacheJson = JsonSerializer.Serialize(result);
            project.ValidationCachedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache validation result for project {ProjectId}", projectId);
        }

        result.ValidatedAt = project.ValidationCachedAt ?? DateTime.UtcNow;
        result.IsCached = false;
        return result;
    }

    public async Task InvalidateValidationCacheAsync(int projectId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project != null)
        {
            project.ValidationCacheJson = null;
            project.ValidationCachedAt = null;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Computes validation for a project (the actual validation logic).
    /// </summary>
    private async Task<ValidationResultDto> ComputeValidationAsync(Project project)
    {
        var result = new ValidationResultDto { IsValid = true };

        // Load resource keys with translations
        var keys = await _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == project.Id)
            .ToListAsync();

        if (keys.Count == 0)
        {
            return result; // No keys, validation passes
        }

        // Get all languages in the project
        var languages = keys
            .SelectMany(k => k.Translations)
            .Select(t => string.IsNullOrEmpty(t.LanguageCode) ? project.DefaultLanguage : t.LanguageCode)
            .Distinct()
            .ToList();

        // Identify default language translations
        var defaultLangKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var defaultTrans = key.Translations.FirstOrDefault(t =>
                t.LanguageCode == project.DefaultLanguage ||
                string.IsNullOrEmpty(t.LanguageCode));
            if (defaultTrans != null && !string.IsNullOrWhiteSpace(defaultTrans.Value))
            {
                defaultLangKeys.Add(key.KeyName);
            }
        }

        // ============================================================
        // 1. Duplicate Keys (case-insensitive)
        // ============================================================
        var duplicateGroups = keys
            .GroupBy(k => k.KeyName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            result.IsValid = false;
            var keyNames = string.Join(", ", group.Select(k => k.KeyName).Distinct());
            result.Issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = $"Duplicate key (case-insensitive): {group.Count()} occurrences",
                Category = ValidationCategory.Duplicate,
                KeyName = group.First().KeyName,
                Details = $"Keys: {keyNames}"
            });
        }

        // ============================================================
        // 2. Check each key for issues
        // ============================================================
        foreach (var key in keys)
        {
            // Empty key names
            if (string.IsNullOrWhiteSpace(key.KeyName))
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    Message = "Resource key has empty name",
                    Category = ValidationCategory.Other,
                    KeyName = key.KeyName
                });
                continue;
            }

            // Get default language value for placeholder comparison
            var defaultTranslation = key.Translations.FirstOrDefault(t =>
                t.LanguageCode == project.DefaultLanguage ||
                string.IsNullOrEmpty(t.LanguageCode));
            var defaultValue = defaultTranslation?.Value;

            foreach (var language in languages)
            {
                // Skip default language for missing/empty checks against itself
                var isDefaultLang = language == project.DefaultLanguage;

                var translation = key.Translations.FirstOrDefault(t =>
                    t.LanguageCode == language ||
                    (isDefaultLang && string.IsNullOrEmpty(t.LanguageCode)));

                // Missing translation
                if (translation == null)
                {
                    if (!isDefaultLang) // Don't flag missing default as "missing translation"
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            Message = $"Missing translation for language '{language}'",
                            Category = ValidationCategory.Missing,
                            KeyName = key.KeyName,
                            LanguageCode = language
                        });
                    }
                    continue;
                }

                // Empty value
                if (string.IsNullOrWhiteSpace(translation.Value))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = "warning",
                        Message = $"Empty value for language '{language}'",
                        Category = ValidationCategory.Empty,
                        KeyName = key.KeyName,
                        LanguageCode = language
                    });
                    continue;
                }

                // Placeholder validation (only for non-default languages)
                if (!isDefaultLang && !string.IsNullOrEmpty(defaultValue))
                {
                    var placeholderResult = PlaceholderValidator.Validate(defaultValue, translation.Value);
                    if (!placeholderResult.IsValid)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            Message = $"Placeholder mismatch in '{language}'",
                            Category = ValidationCategory.Placeholder,
                            KeyName = key.KeyName,
                            LanguageCode = language,
                            Details = placeholderResult.GetSummary()
                        });
                    }
                }

                // Pending review status
                if (translation.Status == TranslationStatus.Pending)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        Message = $"Pending review for language '{language}'",
                        Category = ValidationCategory.Pending,
                        KeyName = key.KeyName,
                        LanguageCode = language
                    });
                }
            }
        }

        // ============================================================
        // 3. Compute Summary
        // ============================================================
        result.Summary = new ValidationSummary
        {
            TotalIssues = result.Issues.Count,
            Errors = result.Issues.Count(i => i.Severity == "error"),
            Warnings = result.Issues.Count(i => i.Severity == "warning"),
            Info = result.Issues.Count(i => i.Severity == "info"),
            DuplicateKeys = result.Issues.Count(i => i.Category == ValidationCategory.Duplicate),
            MissingTranslations = result.Issues.Count(i => i.Category == ValidationCategory.Missing),
            EmptyValues = result.Issues.Count(i => i.Category == ValidationCategory.Empty),
            PlaceholderMismatches = result.Issues.Count(i => i.Category == ValidationCategory.Placeholder),
            ExtraKeys = result.Issues.Count(i => i.Category == ValidationCategory.Extra),
            PendingReview = result.Issues.Count(i => i.Category == ValidationCategory.Pending)
        };

        // Mark as invalid if there are any errors
        if (result.Summary.Errors > 0)
        {
            result.IsValid = false;
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
            SourcePluralText = key.SourcePluralText,
            Comment = key.Comment,
            Version = key.Version,
            TranslationCount = key.Translations.Count,
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt
        };
    }

    private ResourceKeyDetailDto MapToResourceKeyDetailDto(ResourceKey key)
    {
        return MapToResourceKeyDetailDto(key, null);
    }

    private ResourceKeyDetailDto MapToResourceKeyDetailDto(ResourceKey key, string? defaultLanguage)
    {
        return new ResourceKeyDetailDto
        {
            Id = key.Id,
            KeyName = key.KeyName,
            KeyPath = key.KeyPath,
            IsPlural = key.IsPlural,
            SourcePluralText = key.SourcePluralText,
            Comment = key.Comment,
            Version = key.Version,
            TranslationCount = key.Translations.Count,
            CreatedAt = key.CreatedAt,
            UpdatedAt = key.UpdatedAt,
            Translations = key.Translations.Select(t => MapToTranslationDto(t, defaultLanguage)).ToList()
        };
    }

    private TranslationDto MapToTranslationDto(Shared.Entities.Translation translation)
    {
        return MapToTranslationDto(translation, null);
    }

    private TranslationDto MapToTranslationDto(Shared.Entities.Translation translation, string? defaultLanguage)
    {
        // Resolve empty language code to project's default language for display
        var langCode = string.IsNullOrEmpty(translation.LanguageCode) && !string.IsNullOrEmpty(defaultLanguage)
            ? defaultLanguage
            : translation.LanguageCode;

        return new TranslationDto
        {
            Id = translation.Id,
            LanguageCode = langCode,
            Value = translation.Value,
            PluralForm = translation.PluralForm,
            Status = translation.Status,
            TranslatedBy = null, // TODO: Phase 3 - track user who translated
            ReviewedBy = null,   // TODO: Phase 3 - track reviewer
            Version = translation.Version,
            UpdatedAt = translation.UpdatedAt,
            Comment = translation.Comment
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

        // Resolve empty language codes to project's default language for display
        return languageData.Select(l => {
            var resolvedLangCode = string.IsNullOrEmpty(l.LanguageCode) ? project.DefaultLanguage : l.LanguageCode;
            return new ProjectLanguageDto
            {
                LanguageCode = resolvedLangCode,
                DisplayName = GetLanguageDisplayName(resolvedLangCode),
                IsDefault = resolvedLangCode == project.DefaultLanguage,
                TranslatedCount = l.TranslatedCount,
                TotalKeys = totalKeys,
                CompletionPercentage = l.TotalCount > 0 ? Math.Round((double)l.TranslatedCount / l.TotalCount * 100, 1) : 0,
                LastUpdated = l.LastUpdated
            };
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

    // ============================================================
    // Batch Save with History
    // ============================================================

    public async Task<BatchSaveResponse> BatchSaveWithHistoryAsync(
        int projectId, int userId, BatchSaveRequest request)
    {
        // Check permission
        if (!await _projectService.CanManageResourcesAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to manage resources in this project");
        }

        var response = new BatchSaveResponse();
        var changes = new List<SyncChangeEntry>();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // Process key metadata changes (comments)
            foreach (var keyChange in request.KeyChanges)
            {
                var resourceKey = await _db.ResourceKeys
                    .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyChange.KeyName);

                if (resourceKey == null)
                {
                    _logger.LogWarning("Resource key {KeyName} not found for batch save", keyChange.KeyName);
                    continue;
                }

                var beforeComment = resourceKey.Comment;

                // Only record if comment actually changed
                if (beforeComment != keyChange.Comment)
                {
                    resourceKey.Comment = keyChange.Comment;
                    resourceKey.UpdatedAt = DateTime.UtcNow;
                    resourceKey.Version++;
                    response.KeysModified++;

                    // Record the comment change (use empty lang to indicate key-level change)
                    changes.Add(new SyncChangeEntry
                    {
                        Key = keyChange.KeyName,
                        Lang = "",
                        ChangeType = "modified",
                        BeforeComment = beforeComment,
                        AfterComment = keyChange.Comment
                    });
                }
            }

            // Process translation changes
            foreach (var translationChange in request.TranslationChanges)
            {
                var resourceKey = await _db.ResourceKeys
                    .Include(k => k.Translations)
                    .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == translationChange.KeyName);

                if (resourceKey == null)
                {
                    _logger.LogWarning("Resource key {KeyName} not found for batch save", translationChange.KeyName);
                    continue;
                }

                var pluralForm = translationChange.PluralForm ?? "";
                var translation = resourceKey.Translations
                    .FirstOrDefault(t => t.LanguageCode == translationChange.LanguageCode && t.PluralForm == pluralForm);

                string? beforeValue = translation?.Value;
                string? beforeStatus = translation?.Status;

                // Determine change type
                string changeType;
                if (translation == null)
                {
                    // New translation
                    changeType = "added";
                    var newValue = translationChange.Value ?? "";
                    translation = new Shared.Entities.Translation
                    {
                        ResourceKeyId = resourceKey.Id,
                        LanguageCode = translationChange.LanguageCode,
                        PluralForm = pluralForm,
                        Value = newValue,
                        Status = translationChange.Status ?? TranslationStatus.Translated,
                        Comment = translationChange.Comment,
                        Version = 1,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Hash = ComputeHash(newValue, translationChange.Comment)
                    };
                    _db.Translations.Add(translation);
                }
                else
                {
                    // Existing translation - check if anything changed
                    var valueChanged = translationChange.Value != null && translation.Value != translationChange.Value;
                    var statusChanged = translationChange.Status != null && translation.Status != translationChange.Status;
                    var commentChanged = translationChange.Comment != null && translation.Comment != translationChange.Comment;

                    if (!valueChanged && !statusChanged && !commentChanged)
                    {
                        continue; // No actual change
                    }

                    changeType = "modified";

                    if (translationChange.Value != null)
                    {
                        translation.Value = translationChange.Value;
                    }
                    if (translationChange.Status != null)
                    {
                        translation.Status = translationChange.Status;
                    }
                    if (translationChange.Comment != null)
                    {
                        translation.Comment = translationChange.Comment;
                    }
                    // Update hash when value or comment changes
                    translation.Hash = ComputeHash(translation.Value ?? "", translation.Comment);
                    translation.UpdatedAt = DateTime.UtcNow;
                    translation.Version++;
                }

                response.TranslationsModified++;

                // Record the change
                changes.Add(new SyncChangeEntry
                {
                    Key = translationChange.KeyName,
                    Lang = translationChange.LanguageCode,
                    ChangeType = changeType,
                    BeforeValue = beforeValue,
                    AfterValue = translation.Value,
                    BeforeHash = null, // Not computing hash for web edits
                    AfterHash = null
                });
            }

            await _db.SaveChangesAsync();

            // Record sync history if there were changes
            if (changes.Count > 0)
            {
                var message = request.Message ?? "Updated via web editor";
                var history = await _historyService.RecordPushAsync(
                    projectId,
                    userId,
                    message,
                    changes,
                    "web-edit" // Operation type to distinguish from CLI push
                );

                response.HistoryId = history.HistoryId;

                _logger.LogInformation(
                    "User {UserId} batch saved {KeyChanges} key changes and {TranslationChanges} translation changes to project {ProjectId}, history {HistoryId}",
                    userId, response.KeysModified, response.TranslationsModified, projectId, response.HistoryId);
            }

            await transaction.CommitAsync();
            response.Applied = changes.Count;

            // Invalidate validation cache if there were changes
            if (changes.Count > 0)
            {
                await InvalidateValidationCacheAsync(projectId);
            }

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error in batch save for project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Computes a hash for a translation value and optional comment.
    /// Must match the CLI's EntryHasher.ComputeHash algorithm.
    /// </summary>
    private static string ComputeHash(string value, string? comment)
    {
        var normalizedValue = value.Normalize(NormalizationForm.FormC);
        var normalizedComment = comment?.Normalize(NormalizationForm.FormC);

        var sb = new StringBuilder();
        sb.Append(normalizedValue);
        sb.Append('\0');
        sb.Append(normalizedComment ?? "");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
