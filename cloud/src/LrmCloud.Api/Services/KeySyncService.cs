// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Text.Json;
using LocalizationManager.Core.Cloud;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Sync;
using Microsoft.EntityFrameworkCore;
using Entities = LrmCloud.Shared.Entities;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for key-level synchronization with three-way merge support.
/// </summary>
public class KeySyncService : IKeySyncService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ISyncHistoryService _historyService;
    private readonly IResourceService _resourceService;
    private readonly ILogger<KeySyncService> _logger;

    public KeySyncService(
        AppDbContext db,
        IProjectService projectService,
        ISyncHistoryService historyService,
        IResourceService resourceService,
        ILogger<KeySyncService> logger)
    {
        _db = db;
        _projectService = projectService;
        _historyService = historyService;
        _resourceService = resourceService;
        _logger = logger;
    }

    /// <summary>
    /// Handles key-level push with conflict detection.
    /// </summary>
    public async Task<KeySyncPushResponse> PushAsync(
        int projectId,
        int userId,
        KeySyncPushRequest request,
        CancellationToken ct = default)
    {
        var response = new KeySyncPushResponse();
        var historyChanges = new List<Entities.SyncChangeEntry>();

        // Check permission
        if (!await _projectService.CanManageResourcesAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to push to this project");
        }

        // Use a transaction for atomicity
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Process entry changes
            foreach (var entry in request.Entries)
            {
                var result = await ApplyEntryChangeAsync(projectId, entry, ct);

                if (result.IsConflict)
                {
                    response.Conflicts.Add(result.Conflict!);
                }
                else if (result.Applied)
                {
                    response.Applied++;

                    if (!response.NewEntryHashes.ContainsKey(entry.Key))
                    {
                        response.NewEntryHashes[entry.Key] = new Dictionary<string, string>();
                    }
                    response.NewEntryHashes[entry.Key][entry.Lang] = result.NewHash!;

                    // Track change for history
                    historyChanges.Add(new Entities.SyncChangeEntry
                    {
                        Key = entry.Key,
                        Lang = entry.Lang,
                        ChangeType = result.WasNew ? "added" : "modified",
                        BeforeValue = result.BeforeValue,
                        BeforeHash = result.BeforeHash,
                        BeforeComment = result.BeforeComment,
                        AfterValue = entry.Value,
                        AfterHash = result.NewHash,
                        AfterComment = entry.Comment
                    });
                }
            }

            // Process deletions
            foreach (var deletion in request.Deletions)
            {
                var result = await ApplyEntryDeletionAsync(projectId, deletion, ct);

                if (result.IsConflict)
                {
                    response.Conflicts.Add(result.Conflict!);
                }
                else if (result.Applied)
                {
                    response.Deleted++;

                    // Track deletion for history
                    historyChanges.Add(new Entities.SyncChangeEntry
                    {
                        Key = deletion.Key,
                        Lang = deletion.Lang ?? result.DeletedLang ?? "all",
                        ChangeType = "deleted",
                        BeforeValue = result.DeletedValue,
                        BeforeHash = result.DeletedHash,
                        BeforeComment = result.DeletedComment,
                        AfterValue = null,
                        AfterHash = null,
                        AfterComment = null
                    });
                }
            }

            // If there are conflicts, rollback
            if (response.Conflicts.Count > 0)
            {
                await transaction.RollbackAsync(ct);
                return response;
            }

            // Note: Config sync removed - CLI manages lrm.json locally (client-agnostic API)

            await _db.SaveChangesAsync(ct);

            // Record push in history (only if there were changes)
            if (historyChanges.Count > 0)
            {
                await _historyService.RecordPushAsync(projectId, userId, request.Message, historyChanges, operationType: "push", source: "cli", ct: ct);
            }

            await transaction.CommitAsync(ct);

            // Invalidate validation cache after successful push
            if (response.Applied > 0 || response.Deleted > 0)
            {
                await _resourceService.InvalidateValidationCacheAsync(projectId);
            }

            _logger.LogInformation(
                "User {UserId} pushed {Applied} entries, deleted {Deleted} to project {ProjectId}",
                userId, response.Applied, response.Deleted, projectId);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to push changes to project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Handles key-level pull with all entries and their hashes.
    /// </summary>
    public async Task<KeySyncPullResponse> PullAsync(
        int projectId,
        int userId,
        DateTime? since = null,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)
    {
        var response = new KeySyncPullResponse
        {
            SyncTimestamp = DateTime.UtcNow
        };

        // Check permission
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to access this project");
        }

        // Get project for config
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // Build query for resource keys
        var query = _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId);

        // Apply delta filter if 'since' is provided
        if (since.HasValue)
        {
            query = query.Where(k => k.UpdatedAt > since.Value ||
                k.Translations.Any(t => t.UpdatedAt > since.Value));
            response.IsIncremental = true;
        }

        // Get total count
        response.Total = await query.CountAsync(ct);

        // Apply pagination if provided
        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }
        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
            response.HasMore = response.Total > (offset ?? 0) + limit.Value;
        }

        // Get keys with translations
        var keys = await query.OrderBy(k => k.KeyName).ToListAsync(ct);

        // Map to DTOs
        foreach (var key in keys)
        {
            var entryData = new EntryDataDto
            {
                Key = key.KeyName,
                Comment = key.Comment,
                IsPlural = key.IsPlural,
                SourcePluralText = key.SourcePluralText
            };

            if (key.IsPlural)
            {
                // For plural keys, group translations by language and build plural forms dictionary
                var translationsByLang = key.Translations
                    .GroupBy(t => t.LanguageCode ?? "")
                    .ToList();

                foreach (var langGroup in translationsByLang)
                {
                    var langCode = langGroup.Key;
                    var translations = langGroup.ToList();

                    // Build plural forms dictionary from all translations for this language
                    var pluralForms = translations
                        .Where(t => !string.IsNullOrEmpty(t.PluralForm))
                        .ToDictionary(t => t.PluralForm!, t => t.Value ?? "");

                    // If we have plural forms, use "other" as the main value
                    var mainValue = pluralForms.GetValueOrDefault("other") ??
                                   translations.FirstOrDefault()?.Value ?? "";

                    // Compute hash from all plural forms
                    var hash = pluralForms.Count > 0
                        ? EntryHasher.ComputePluralHash(pluralForms, translations.FirstOrDefault()?.Comment)
                        : EntryHasher.ComputeHash(mainValue, translations.FirstOrDefault()?.Comment);

                    entryData.Translations[langCode] = new TranslationDataDto
                    {
                        Value = mainValue,
                        Comment = translations.FirstOrDefault()?.Comment,
                        Hash = hash,
                        Status = translations.FirstOrDefault()?.Status ?? "translated",
                        UpdatedAt = translations.Max(t => t.UpdatedAt),
                        PluralForms = pluralForms.Count > 0 ? pluralForms : null
                    };
                }
            }
            else
            {
                // Regular (non-plural) entries
                foreach (var translation in key.Translations)
                {
                    // Compute hash if not stored
                    var hash = translation.Hash ?? EntryHasher.ComputeHash(translation.Value ?? "", translation.Comment);

                    // Use raw language code from database - do NOT resolve empty string here
                    // Sync operations need to match exactly what was pushed (empty = default lang)
                    // Resolution to actual code is only for display purposes (UI endpoints)
                    var langCode = translation.LanguageCode ?? "";

                    entryData.Translations[langCode] = new TranslationDataDto
                    {
                        Value = translation.Value ?? "",
                        Comment = translation.Comment,
                        Hash = hash,
                        Status = translation.Status,
                        UpdatedAt = translation.UpdatedAt,
                        PluralForms = null
                    };
                }
            }

            response.Entries.Add(entryData);
        }

        // Add project's default language
        response.DefaultLanguage = project.DefaultLanguage;

        // Note: Config sync removed - CLI manages lrm.json locally (client-agnostic API)

        return response;
    }

    /// <summary>
    /// Resolves conflicts after push.
    /// </summary>
    public async Task<ConflictResolutionResponse> ResolveConflictsAsync(
        int projectId,
        int userId,
        ConflictResolutionRequest request,
        CancellationToken ct = default)
    {
        var response = new ConflictResolutionResponse();

        // Check permission
        if (!await _projectService.CanManageResourcesAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to resolve conflicts in this project");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            foreach (var resolution in request.Resolutions)
            {
                if (resolution.TargetType == "Entry" && resolution.Lang != null)
                {
                    await ApplyEntryResolutionAsync(projectId, resolution, response, ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "User {UserId} resolved {Count} conflicts in project {ProjectId}",
                userId, response.Applied, projectId);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to resolve conflicts in project {ProjectId}", projectId);
            throw;
        }
    }

    #region Private Methods

    private async Task<EntryChangeResult> ApplyEntryChangeAsync(
        int projectId,
        EntryChangeDto entry,
        CancellationToken ct)
    {
        // Find or create the resource key
        var resourceKey = await _db.ResourceKeys
            .Include(k => k.Translations)
            .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == entry.Key, ct);

        if (resourceKey == null)
        {
            // Create new resource key
            resourceKey = new Entities.ResourceKey
            {
                ProjectId = projectId,
                KeyName = entry.Key,
                IsPlural = entry.IsPlural,
                Comment = entry.Comment,
                // For plural keys, store source plural text (PO msgid_plural or "other" form)
                SourcePluralText = entry.IsPlural ? entry.SourcePluralText : null,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ResourceKeys.Add(resourceKey);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Update key's IsPlural flag if changed
            if (resourceKey.IsPlural != entry.IsPlural)
            {
                resourceKey.IsPlural = entry.IsPlural;
                resourceKey.UpdatedAt = DateTime.UtcNow;
            }
            // Update SourcePluralText if not set yet
            if (entry.IsPlural && resourceKey.SourcePluralText == null && entry.SourcePluralText != null)
            {
                resourceKey.SourcePluralText = entry.SourcePluralText;
                resourceKey.UpdatedAt = DateTime.UtcNow;
            }
        }

        // For plural entries, store each plural form as a separate Translation row
        if (entry.IsPlural && entry.PluralForms != null && entry.PluralForms.Count > 0)
        {
            return await ApplyPluralEntryChangeAsync(resourceKey, entry, ct);
        }

        // Regular (non-plural) entry handling
        var translation = resourceKey.Translations
            .FirstOrDefault(t => t.LanguageCode == entry.Lang && (t.PluralForm == "" || t.PluralForm == null));

        var newHash = EntryHasher.ComputeHash(entry.Value, entry.Comment);

        if (translation == null)
        {
            // New translation - no conflict possible
            translation = new Entities.Translation
            {
                ResourceKeyId = resourceKey.Id,
                LanguageCode = entry.Lang,
                Value = entry.Value,
                Comment = entry.Comment,
                Hash = newHash,
                Status = "translated",
                PluralForm = "",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Translations.Add(translation);
            return new EntryChangeResult
            {
                Applied = true,
                WasNew = true,
                NewHash = newHash
            };
        }

        // Check for conflict
        if (entry.BaseHash != null && translation.Hash != null && entry.BaseHash != translation.Hash)
        {
            // CONFLICT: Remote changed since last sync
            return new EntryChangeResult
            {
                IsConflict = true,
                Conflict = new EntryConflictDto
                {
                    Key = entry.Key,
                    Lang = entry.Lang,
                    Type = "BothModified",
                    LocalValue = entry.Value,
                    RemoteValue = translation.Value,
                    RemoteHash = translation.Hash,
                    RemoteUpdatedAt = translation.UpdatedAt
                }
            };
        }

        // Capture before state for history
        var beforeValue = translation.Value;
        var beforeHash = translation.Hash;
        var beforeComment = translation.Comment;

        // Apply update
        translation.Value = entry.Value;
        translation.Comment = entry.Comment;
        translation.Hash = newHash;
        translation.Version++;
        translation.UpdatedAt = DateTime.UtcNow;

        return new EntryChangeResult
        {
            Applied = true,
            WasNew = false,
            NewHash = newHash,
            BeforeValue = beforeValue,
            BeforeHash = beforeHash,
            BeforeComment = beforeComment
        };
    }

    /// <summary>
    /// Applies changes for plural entries, storing each plural form as a separate Translation row.
    /// </summary>
    private async Task<EntryChangeResult> ApplyPluralEntryChangeAsync(
        Entities.ResourceKey resourceKey,
        EntryChangeDto entry,
        CancellationToken ct)
    {
        // Get existing translations for this key/language
        var existingTranslations = resourceKey.Translations
            .Where(t => t.LanguageCode == entry.Lang)
            .ToList();

        // Compute hash from all plural forms
        var newHash = EntryHasher.ComputePluralHash(entry.PluralForms!, entry.Comment);

        // Check for conflict using the main entry's BaseHash
        if (entry.BaseHash != null)
        {
            // Compute existing hash from current plural forms
            var existingPluralForms = existingTranslations
                .Where(t => !string.IsNullOrEmpty(t.PluralForm))
                .ToDictionary(t => t.PluralForm!, t => t.Value ?? "");

            if (existingPluralForms.Count > 0)
            {
                var existingHash = EntryHasher.ComputePluralHash(existingPluralForms, existingTranslations.FirstOrDefault()?.Comment);
                if (entry.BaseHash != existingHash)
                {
                    // CONFLICT: Remote changed since last sync
                    return new EntryChangeResult
                    {
                        IsConflict = true,
                        Conflict = new EntryConflictDto
                        {
                            Key = entry.Key,
                            Lang = entry.Lang,
                            Type = "BothModified",
                            LocalValue = entry.Value,
                            RemoteValue = existingTranslations.FirstOrDefault(t => t.PluralForm == "other")?.Value ?? "",
                            RemoteHash = existingHash,
                            RemoteUpdatedAt = existingTranslations.Max(t => t.UpdatedAt)
                        }
                    };
                }
            }
        }

        var wasNew = !existingTranslations.Any(t => !string.IsNullOrEmpty(t.PluralForm));
        var now = DateTime.UtcNow;

        // Remove existing translations that are no longer in PluralForms
        var existingForms = existingTranslations
            .Where(t => !string.IsNullOrEmpty(t.PluralForm))
            .ToList();

        foreach (var existing in existingForms)
        {
            if (!entry.PluralForms!.ContainsKey(existing.PluralForm!))
            {
                _db.Translations.Remove(existing);
            }
        }

        // Also remove any non-plural translation (PluralForm = "") as we're now storing plural forms
        var nonPluralTranslation = existingTranslations.FirstOrDefault(t => t.PluralForm == "" || t.PluralForm == null);
        if (nonPluralTranslation != null)
        {
            _db.Translations.Remove(nonPluralTranslation);
        }

        // Add or update plural form translations
        foreach (var (category, value) in entry.PluralForms!)
        {
            var existing = existingTranslations.FirstOrDefault(t => t.PluralForm == category);

            if (existing != null)
            {
                // Update existing
                existing.Value = value;
                existing.Comment = entry.Comment;
                existing.Version++;
                existing.UpdatedAt = now;
            }
            else
            {
                // Create new plural form translation
                var translation = new Entities.Translation
                {
                    ResourceKeyId = resourceKey.Id,
                    LanguageCode = entry.Lang,
                    Value = value,
                    Comment = entry.Comment,
                    PluralForm = category,
                    Status = "translated",
                    Version = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Translations.Add(translation);
            }
        }

        return new EntryChangeResult
        {
            Applied = true,
            WasNew = wasNew,
            NewHash = newHash
        };
    }

    private class EntryChangeResult
    {
        public bool Applied { get; set; }
        public bool IsConflict { get; set; }
        public bool WasNew { get; set; }
        public EntryConflictDto? Conflict { get; set; }
        public string? NewHash { get; set; }
        public string? BeforeValue { get; set; }
        public string? BeforeHash { get; set; }
        public string? BeforeComment { get; set; }
    }

    private async Task<EntryDeletionResult> ApplyEntryDeletionAsync(
        int projectId,
        EntryDeletionDto deletion,
        CancellationToken ct)
    {
        var resourceKey = await _db.ResourceKeys
            .Include(k => k.Translations)
            .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == deletion.Key, ct);

        if (resourceKey == null)
        {
            // Already deleted, no-op
            return new EntryDeletionResult { Applied = true };
        }

        if (deletion.Lang == null)
        {
            // Delete entire key
            var anyTranslation = resourceKey.Translations.FirstOrDefault();
            if (anyTranslation != null && anyTranslation.Hash != null && deletion.BaseHash != anyTranslation.Hash)
            {
                // CONFLICT: Remote modified since last sync
                return new EntryDeletionResult
                {
                    IsConflict = true,
                    Conflict = new EntryConflictDto
                    {
                        Key = deletion.Key,
                        Lang = anyTranslation.LanguageCode,
                        Type = "DeletedLocallyModifiedRemotely",
                        LocalValue = null,
                        RemoteValue = anyTranslation.Value,
                        RemoteHash = anyTranslation.Hash,
                        RemoteUpdatedAt = anyTranslation.UpdatedAt
                    }
                };
            }

            // Capture deleted state for history (use first translation for now)
            var deletedValue = anyTranslation?.Value;
            var deletedHash = anyTranslation?.Hash;
            var deletedComment = anyTranslation?.Comment;
            var deletedLang = anyTranslation?.LanguageCode;

            _db.ResourceKeys.Remove(resourceKey);

            return new EntryDeletionResult
            {
                Applied = true,
                DeletedValue = deletedValue,
                DeletedHash = deletedHash,
                DeletedComment = deletedComment,
                DeletedLang = deletedLang
            };
        }
        else
        {
            // Delete specific language translation
            var translation = resourceKey.Translations
                .FirstOrDefault(t => t.LanguageCode == deletion.Lang && t.PluralForm == "");

            if (translation == null)
            {
                return new EntryDeletionResult { Applied = true };
            }

            if (translation.Hash != null && deletion.BaseHash != translation.Hash)
            {
                // CONFLICT
                return new EntryDeletionResult
                {
                    IsConflict = true,
                    Conflict = new EntryConflictDto
                    {
                        Key = deletion.Key,
                        Lang = deletion.Lang,
                        Type = "DeletedLocallyModifiedRemotely",
                        LocalValue = null,
                        RemoteValue = translation.Value,
                        RemoteHash = translation.Hash,
                        RemoteUpdatedAt = translation.UpdatedAt
                    }
                };
            }

            // Capture deleted state for history
            var deletedValue = translation.Value;
            var deletedHash = translation.Hash;
            var deletedComment = translation.Comment;

            _db.Translations.Remove(translation);

            return new EntryDeletionResult
            {
                Applied = true,
                DeletedValue = deletedValue,
                DeletedHash = deletedHash,
                DeletedComment = deletedComment,
                DeletedLang = deletion.Lang
            };
        }
    }

    private class EntryDeletionResult
    {
        public bool Applied { get; set; }
        public bool IsConflict { get; set; }
        public EntryConflictDto? Conflict { get; set; }
        public string? DeletedValue { get; set; }
        public string? DeletedHash { get; set; }
        public string? DeletedComment { get; set; }
        public string? DeletedLang { get; set; }
    }

    private async Task ApplyEntryResolutionAsync(
        int projectId,
        ConflictResolutionDto resolution,
        ConflictResolutionResponse response,
        CancellationToken ct)
    {
        var resourceKey = await _db.ResourceKeys
            .Include(k => k.Translations)
            .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == resolution.Key, ct);

        if (resourceKey == null)
        {
            return;
        }

        var translation = resourceKey.Translations
            .FirstOrDefault(t => t.LanguageCode == resolution.Lang && t.PluralForm == "");

        switch (resolution.Resolution)
        {
            case "Local":
                // Overwrite with local value (EditedValue contains the local value)
                if (resolution.EditedValue != null && translation != null)
                {
                    translation.Value = resolution.EditedValue;
                    translation.Hash = EntryHasher.ComputeHash(resolution.EditedValue, translation.Comment);
                    translation.Version++;
                    translation.UpdatedAt = DateTime.UtcNow;
                    response.Applied++;

                    AddToHashes(response.NewHashes, resolution.Key, resolution.Lang!, translation.Hash);
                }
                break;

            case "Remote":
                // Keep remote value (already in DB)
                if (translation != null)
                {
                    response.Applied++;
                    AddToHashes(response.NewHashes, resolution.Key, resolution.Lang!, translation.Hash ?? "");
                }
                break;

            case "Edit":
                // Use custom edited value
                if (resolution.EditedValue != null)
                {
                    if (translation == null)
                    {
                        translation = new Entities.Translation
                        {
                            ResourceKeyId = resourceKey.Id,
                            LanguageCode = resolution.Lang!,
                            Value = resolution.EditedValue,
                            Hash = EntryHasher.ComputeHash(resolution.EditedValue, null),
                            Status = "translated",
                            PluralForm = "",
                            Version = 1,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.Translations.Add(translation);
                    }
                    else
                    {
                        translation.Value = resolution.EditedValue;
                        translation.Hash = EntryHasher.ComputeHash(resolution.EditedValue, translation.Comment);
                        translation.Version++;
                        translation.UpdatedAt = DateTime.UtcNow;
                    }
                    response.Applied++;
                    AddToHashes(response.NewHashes, resolution.Key, resolution.Lang!, translation.Hash!);
                }
                break;
        }
    }

    private static void AddToHashes(Dictionary<string, Dictionary<string, string>> hashes, string key, string lang, string hash)
    {
        if (!hashes.ContainsKey(key))
        {
            hashes[key] = new Dictionary<string, string>();
        }
        hashes[key][lang] = hash;
    }

    #endregion
}

/// <summary>
/// Interface for key-level sync service.
/// </summary>
public interface IKeySyncService
{
    Task<KeySyncPushResponse> PushAsync(int projectId, int userId, KeySyncPushRequest request, CancellationToken ct = default);
    Task<KeySyncPullResponse> PullAsync(int projectId, int userId, DateTime? since = null, int? limit = null, int? offset = null, CancellationToken ct = default);
    Task<ConflictResolutionResponse> ResolveConflictsAsync(int projectId, int userId, ConflictResolutionRequest request, CancellationToken ct = default);
}
