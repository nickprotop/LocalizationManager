using System.Text;
using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Snapshots;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing project snapshots (point-in-time backups).
/// Snapshots store database state (dbstate.json) in MinIO for restore capability.
/// Resource files are NOT stored - they can be regenerated from the database.
/// </summary>
public class SnapshotService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storageService;
    private readonly ILogger<SnapshotService> _logger;
    private readonly LimitsConfiguration _limits;

    public SnapshotService(
        AppDbContext db,
        IStorageService storageService,
        ILogger<SnapshotService> logger,
        CloudConfiguration config)
    {
        _db = db;
        _storageService = storageService;
        _logger = logger;
        _limits = config.Limits;
    }

    /// <summary>
    /// Generates a unique 8-character snapshot ID.
    /// </summary>
    private static string GenerateSnapshotId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Creates a new snapshot of the current project state.
    /// Stores database state as dbstate.json in MinIO.
    /// </summary>
    public async Task<Snapshot> CreateSnapshotAsync(
        int projectId,
        int? userId,
        string snapshotType,
        string? description = null)
    {
        // Get project with user to check plan limits
        var project = await _db.Projects
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new InvalidOperationException($"Project {projectId} not found");
        }

        var plan = project.User?.Plan ?? "free";
        var maxSnapshots = _limits.GetMaxSnapshots(plan);

        // Check snapshot count limit BEFORE creating
        var currentSnapshotCount = await _db.Set<Snapshot>()
            .CountAsync(s => s.ProjectId == projectId);

        if (currentSnapshotCount >= maxSnapshots)
        {
            throw new InvalidOperationException(
                $"Snapshot limit reached ({currentSnapshotCount}/{maxSnapshots}). " +
                "Delete older snapshots or upgrade your plan.");
        }

        var snapshotId = GenerateSnapshotId();
        var storagePath = $"snapshots/{snapshotId}/";

        // Create database state and save to MinIO
        var dbState = await CreateDbStateAsync(projectId, snapshotId);
        await SaveDbStateToSnapshotAsync(projectId, snapshotId, dbState);

        // Get language count from translations
        var languageCodes = dbState.Keys
            .SelectMany(k => k.Translations.Select(t => t.LanguageCode))
            .Distinct()
            .ToList();

        // Create snapshot record
        var snapshot = new Snapshot
        {
            ProjectId = projectId,
            SnapshotId = snapshotId,
            CreatedByUserId = userId,
            Description = description,
            StoragePath = storagePath,
            FileCount = languageCodes.Count, // Number of languages (conceptual files)
            KeyCount = dbState.Keys.Count,
            TranslationCount = dbState.Keys.Sum(k => k.Translations.Count),
            SnapshotType = snapshotType,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<Snapshot>().Add(snapshot);
        await _db.SaveChangesAsync();

        // Apply plan-based retention policy
        var retentionDays = _limits.GetSnapshotRetentionDays(plan);
        await ApplyRetentionPolicyAsync(projectId, retentionDays, maxSnapshots);

        _logger.LogInformation("Created snapshot {SnapshotId} for project {ProjectId} with {Keys} keys by user {UserId}",
            snapshotId, projectId, dbState.Keys.Count, userId);

        return snapshot;
    }

    /// <summary>
    /// Creates a database state object for the current project state.
    /// </summary>
    private async Task<SnapshotDbState> CreateDbStateAsync(int projectId, string snapshotId)
    {
        var keys = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .Include(k => k.Translations)
            .OrderBy(k => k.KeyName)
            .ToListAsync();

        return new SnapshotDbState
        {
            Version = 1,
            ProjectId = projectId,
            SnapshotId = snapshotId,
            CreatedAt = DateTime.UtcNow,
            Keys = keys.Select(k => new SnapshotKeyState
            {
                KeyName = k.KeyName,
                KeyPath = k.KeyPath,
                IsPlural = k.IsPlural,
                Comment = k.Comment,
                Translations = k.Translations.Select(t => new SnapshotTranslationState
                {
                    LanguageCode = t.LanguageCode,
                    Value = t.Value,
                    Comment = t.Comment,
                    PluralForm = t.PluralForm,
                    Status = t.Status,
                    TranslatedBy = t.TranslatedBy
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Saves database state as JSON to the snapshot in storage.
    /// </summary>
    private async Task SaveDbStateToSnapshotAsync(int projectId, string snapshotId, SnapshotDbState dbState)
    {
        var json = JsonSerializer.Serialize(dbState, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        await _storageService.UploadSnapshotFileAsync(projectId, snapshotId, "dbstate.json", stream);
    }

    /// <summary>
    /// Loads database state from a snapshot.
    /// </summary>
    private async Task<SnapshotDbState?> LoadDbStateFromSnapshotAsync(int projectId, string snapshotId)
    {
        var stream = await _storageService.DownloadSnapshotFileAsync(projectId, snapshotId, "dbstate.json");
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<SnapshotDbState>(json);
    }

    /// <summary>
    /// Lists all snapshots for a project.
    /// </summary>
    public async Task<PagedResult<SnapshotDto>> ListSnapshotsAsync(
        int projectId,
        int page = 1,
        int pageSize = 20)
    {
        var query = _db.Set<Snapshot>()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync();

        var snapshots = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SnapshotDto
            {
                Id = s.Id,
                SnapshotId = s.SnapshotId,
                ProjectId = s.ProjectId,
                Description = s.Description,
                SnapshotType = s.SnapshotType,
                FileCount = s.FileCount,
                KeyCount = s.KeyCount,
                TranslationCount = s.TranslationCount,
                CreatedByUserId = s.CreatedByUserId,
                CreatedByUsername = s.CreatedBy != null ? s.CreatedBy.Username : null,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<SnapshotDto>
        {
            Items = snapshots,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Gets a snapshot with its details.
    /// </summary>
    public async Task<SnapshotDetailDto?> GetSnapshotAsync(int projectId, string snapshotId)
    {
        var snapshot = await _db.Set<Snapshot>()
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == snapshotId);

        if (snapshot == null)
            return null;

        // Load dbstate to get language info
        var dbState = await LoadDbStateFromSnapshotAsync(projectId, snapshotId);
        var languages = dbState?.Keys
            .SelectMany(k => k.Translations.Select(t => t.LanguageCode))
            .Distinct()
            .OrderBy(l => l)
            .ToList() ?? new List<string>();

        // Create file list from languages (conceptual - files can be regenerated)
        var files = languages.Select(lang => new SnapshotFileDto
        {
            Path = lang,
            LanguageCode = lang,
            Size = 0
        }).ToList();

        return new SnapshotDetailDto
        {
            Id = snapshot.Id,
            SnapshotId = snapshot.SnapshotId,
            ProjectId = snapshot.ProjectId,
            Description = snapshot.Description,
            SnapshotType = snapshot.SnapshotType,
            FileCount = snapshot.FileCount,
            KeyCount = snapshot.KeyCount,
            TranslationCount = snapshot.TranslationCount,
            CreatedByUserId = snapshot.CreatedByUserId,
            CreatedByUsername = snapshot.CreatedBy?.Username,
            CreatedAt = snapshot.CreatedAt,
            Files = files
        };
    }

    /// <summary>
    /// Restores a project from a snapshot.
    /// Restores database state from dbstate.json.
    /// </summary>
    public async Task<Snapshot?> RestoreSnapshotAsync(
        int projectId,
        string snapshotId,
        int userId,
        bool createBackup = true,
        string? message = null)
    {
        var snapshot = await _db.Set<Snapshot>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == snapshotId);

        if (snapshot == null)
            return null;

        // Create backup before restore if requested
        if (createBackup)
        {
            await CreateSnapshotAsync(projectId, userId, "pre-restore",
                $"Backup before restore to {snapshotId}");
        }

        // Restore database state from snapshot
        var dbState = await LoadDbStateFromSnapshotAsync(projectId, snapshotId);
        if (dbState != null)
        {
            await RestoreDbStateAsync(projectId, dbState);
            _logger.LogInformation("Restored database state with {Keys} keys from snapshot {SnapshotId}",
                dbState.Keys.Count, snapshotId);
        }
        else
        {
            _logger.LogWarning("No database state found in snapshot {SnapshotId}", snapshotId);
            throw new InvalidOperationException($"Snapshot {snapshotId} has no database state");
        }

        _logger.LogInformation("Restored snapshot {SnapshotId} for project {ProjectId} by user {UserId}",
            snapshotId, projectId, userId);

        // Create a new snapshot record for the restore event
        var restoreSnapshot = await CreateSnapshotAsync(projectId, userId, "restore",
            message ?? $"Restored from snapshot {snapshotId}");

        return restoreSnapshot;
    }

    /// <summary>
    /// Restores database state from a snapshot.
    /// Replaces all keys and translations with the state from the snapshot.
    /// </summary>
    private async Task RestoreDbStateAsync(int projectId, SnapshotDbState dbState)
    {
        // Use a transaction to ensure consistency
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // Delete existing translations for this project
            var existingTranslations = await _db.Translations
                .Where(t => t.ResourceKey != null && t.ResourceKey.ProjectId == projectId)
                .ToListAsync();
            _db.Translations.RemoveRange(existingTranslations);
            await _db.SaveChangesAsync();

            // Delete existing keys for this project
            var existingKeys = await _db.ResourceKeys
                .Where(k => k.ProjectId == projectId)
                .ToListAsync();
            _db.ResourceKeys.RemoveRange(existingKeys);
            await _db.SaveChangesAsync();

            // Restore keys and translations from snapshot
            foreach (var keyState in dbState.Keys)
            {
                var key = new ResourceKey
                {
                    ProjectId = projectId,
                    KeyName = keyState.KeyName,
                    KeyPath = keyState.KeyPath,
                    IsPlural = keyState.IsPlural,
                    Comment = keyState.Comment,
                    Version = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ResourceKeys.Add(key);
                await _db.SaveChangesAsync();

                // Add translations for this key
                foreach (var translationState in keyState.Translations)
                {
                    var translation = new LrmCloud.Shared.Entities.Translation
                    {
                        ResourceKeyId = key.Id,
                        LanguageCode = translationState.LanguageCode,
                        Value = translationState.Value,
                        Comment = translationState.Comment,
                        PluralForm = translationState.PluralForm,
                        Status = translationState.Status,
                        TranslatedBy = translationState.TranslatedBy,
                        Version = 1,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.Translations.Add(translation);
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    public async Task<bool> DeleteSnapshotAsync(int projectId, string snapshotId)
    {
        var snapshot = await _db.Set<Snapshot>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == snapshotId);

        if (snapshot == null)
            return false;

        // Delete from storage
        await _storageService.DeleteSnapshotAsync(projectId, snapshotId);

        // Delete from database
        _db.Set<Snapshot>().Remove(snapshot);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted snapshot {SnapshotId} for project {ProjectId}", snapshotId, projectId);

        return true;
    }

    /// <summary>
    /// Compares two snapshots using their database states.
    /// </summary>
    public async Task<SnapshotDiffDto?> DiffSnapshotsAsync(
        int projectId,
        string fromSnapshotId,
        string toSnapshotId)
    {
        // Verify both snapshots exist
        var fromSnapshot = await _db.Set<Snapshot>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == fromSnapshotId);
        var toSnapshot = await _db.Set<Snapshot>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == toSnapshotId);

        if (fromSnapshot == null || toSnapshot == null)
            return null;

        // Load database states
        var fromState = await LoadDbStateFromSnapshotAsync(projectId, fromSnapshotId);
        var toState = await LoadDbStateFromSnapshotAsync(projectId, toSnapshotId);

        if (fromState == null || toState == null)
            return null;

        // Compare keys
        var fromKeys = fromState.Keys.Select(k => k.KeyName).ToHashSet();
        var toKeys = toState.Keys.Select(k => k.KeyName).ToHashSet();

        var addedKeys = toKeys.Except(fromKeys).ToList();
        var removedKeys = fromKeys.Except(toKeys).ToList();
        var commonKeys = fromKeys.Intersect(toKeys).ToList();

        // Count modified keys (same key name but different content)
        var modifiedCount = 0;
        foreach (var keyName in commonKeys)
        {
            var fromKey = fromState.Keys.First(k => k.KeyName == keyName);
            var toKey = toState.Keys.First(k => k.KeyName == keyName);

            // Compare translations
            var fromTranslations = fromKey.Translations
                .ToDictionary(t => t.LanguageCode, t => t.Value);
            var toTranslations = toKey.Translations
                .ToDictionary(t => t.LanguageCode, t => t.Value);

            if (!fromTranslations.SequenceEqual(toTranslations))
                modifiedCount++;
        }

        // Create conceptual file diff based on languages
        var fromLanguages = fromState.Keys.SelectMany(k => k.Translations.Select(t => t.LanguageCode)).Distinct().ToHashSet();
        var toLanguages = toState.Keys.SelectMany(k => k.Translations.Select(t => t.LanguageCode)).Distinct().ToHashSet();

        var diffFiles = new List<SnapshotDiffFileDto>();

        foreach (var lang in toLanguages.Except(fromLanguages))
        {
            diffFiles.Add(new SnapshotDiffFileDto { Path = lang, ChangeType = "added" });
        }

        foreach (var lang in fromLanguages.Except(toLanguages))
        {
            diffFiles.Add(new SnapshotDiffFileDto { Path = lang, ChangeType = "removed" });
        }

        foreach (var lang in fromLanguages.Intersect(toLanguages))
        {
            diffFiles.Add(new SnapshotDiffFileDto { Path = lang, ChangeType = "modified" });
        }

        return new SnapshotDiffDto
        {
            FromSnapshotId = fromSnapshotId,
            ToSnapshotId = toSnapshotId,
            Files = diffFiles,
            KeysAdded = addedKeys.Count,
            KeysRemoved = removedKeys.Count,
            KeysModified = modifiedCount
        };
    }

    /// <summary>
    /// Applies retention policy by deleting old snapshots.
    /// </summary>
    private async Task ApplyRetentionPolicyAsync(int projectId, int retentionDays, int maxSnapshots)
    {
        // Delete snapshots older than retention period
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var oldSnapshots = await _db.Set<Snapshot>()
            .Where(s => s.ProjectId == projectId && s.CreatedAt < cutoffDate)
            .ToListAsync();

        foreach (var snapshot in oldSnapshots)
        {
            await _storageService.DeleteSnapshotAsync(projectId, snapshot.SnapshotId);
            _db.Set<Snapshot>().Remove(snapshot);
        }

        if (oldSnapshots.Count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} old snapshots for project {ProjectId} (retention: {Days} days)",
                oldSnapshots.Count, projectId, retentionDays);
        }

        // Keep only maxSnapshots most recent
        var excessSnapshots = await _db.Set<Snapshot>()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(maxSnapshots)
            .ToListAsync();

        foreach (var snapshot in excessSnapshots)
        {
            await _storageService.DeleteSnapshotAsync(projectId, snapshot.SnapshotId);
            _db.Set<Snapshot>().Remove(snapshot);
        }

        if (excessSnapshots.Count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} excess snapshots for project {ProjectId} (max: {Max})",
                excessSnapshots.Count, projectId, maxSnapshots);
        }
    }

    /// <summary>
    /// Checks if there are changes since the last snapshot.
    /// </summary>
    public async Task<UnsnapshotedChangesDto> CheckUnsnapshotedChangesAsync(int projectId)
    {
        // Get the latest snapshot date for this project
        var latestSnapshotDate = await _db.Set<Snapshot>()
            .Where(s => s.ProjectId == projectId)
            .MaxAsync(s => (DateTime?)s.CreatedAt);

        // Get the latest translation update date
        var latestTranslationDate = await _db.Translations
            .Where(t => t.ResourceKey != null && t.ResourceKey.ProjectId == projectId)
            .MaxAsync(t => (DateTime?)t.UpdatedAt);

        // Get the latest resource key update date
        var latestKeyDate = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .MaxAsync(k => (DateTime?)k.UpdatedAt);

        // Use the most recent change date
        var latestChangeDate = new[] { latestTranslationDate, latestKeyDate }
            .Where(d => d.HasValue)
            .DefaultIfEmpty(null)
            .Max();

        var hasChanges = latestChangeDate.HasValue &&
            (!latestSnapshotDate.HasValue || latestChangeDate > latestSnapshotDate);

        return new UnsnapshotedChangesDto
        {
            HasUnsnapshotedChanges = hasChanges,
            LastSnapshotAt = latestSnapshotDate,
            LastChangeAt = latestChangeDate
        };
    }
}
