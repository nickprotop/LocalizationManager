using System.Text;
using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Snapshots;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing project snapshots (point-in-time backups).
/// </summary>
public class SnapshotService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storageService;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        AppDbContext db,
        IStorageService storageService,
        ILogger<SnapshotService> logger)
    {
        _db = db;
        _storageService = storageService;
        _logger = logger;
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
    /// Includes both file storage and database state.
    /// </summary>
    public async Task<Snapshot> CreateSnapshotAsync(
        int projectId,
        int? userId,
        string snapshotType,
        string? description = null)
    {
        var snapshotId = GenerateSnapshotId();
        var storagePath = $"snapshots/{snapshotId}/";

        // Create snapshot in MinIO storage (files)
        await _storageService.CreateSnapshotAsync(projectId, snapshotId);

        // Get file count from storage
        var files = await _storageService.ListSnapshotFilesAsync(projectId, snapshotId);

        // Get database state and save as JSON in snapshot
        var dbState = await CreateDbStateAsync(projectId, snapshotId);
        await SaveDbStateToSnapshotAsync(projectId, snapshotId, dbState);

        // Create snapshot record
        var snapshot = new Snapshot
        {
            ProjectId = projectId,
            SnapshotId = snapshotId,
            CreatedByUserId = userId,
            Description = description,
            StoragePath = storagePath,
            FileCount = files.Count,
            KeyCount = dbState.Keys.Count,
            TranslationCount = dbState.Keys.Sum(k => k.Translations.Count),
            SnapshotType = snapshotType,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<Snapshot>().Add(snapshot);
        await _db.SaveChangesAsync();

        // Apply retention policy if configured
        var project = await _db.Projects.FindAsync(projectId);
        if (project != null)
        {
            await ApplyRetentionPolicyAsync(projectId, project.SnapshotRetentionDays, project.MaxSnapshots);
        }

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
        await _storageService.UploadFileAsync(projectId, $"snapshots/{snapshotId}/dbstate.json", stream, "application/json");
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
    /// Gets a snapshot with its file list.
    /// </summary>
    public async Task<SnapshotDetailDto?> GetSnapshotAsync(int projectId, string snapshotId)
    {
        var snapshot = await _db.Set<Snapshot>()
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SnapshotId == snapshotId);

        if (snapshot == null)
            return null;

        // Get files from storage
        var storedFiles = await _storageService.ListSnapshotFilesAsync(projectId, snapshotId);
        var files = storedFiles.Select(f =>
        {
            var fileName = f.Replace($"snapshots/{snapshotId}/", "");
            return new SnapshotFileDto
            {
                Path = fileName,
                LanguageCode = ExtractLanguageCode(fileName),
                Size = 0 // Size would require additional API call
            };
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
    /// Restores both files and database state.
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

        // Restore files from snapshot
        await _storageService.RestoreFromSnapshotAsync(projectId, snapshotId);

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
            _logger.LogWarning("No database state found in snapshot {SnapshotId} - only files were restored", snapshotId);
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
    /// Compares two snapshots.
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

        // Get files from both snapshots
        var fromFiles = await _storageService.ListSnapshotFilesAsync(projectId, fromSnapshotId);
        var toFiles = await _storageService.ListSnapshotFilesAsync(projectId, toSnapshotId);

        var fromFileNames = fromFiles.Select(f => f.Replace($"snapshots/{fromSnapshotId}/", "")).ToHashSet();
        var toFileNames = toFiles.Select(f => f.Replace($"snapshots/{toSnapshotId}/", "")).ToHashSet();

        var diffFiles = new List<SnapshotDiffFileDto>();

        // Added files (in toSnapshot but not in fromSnapshot)
        foreach (var file in toFileNames.Except(fromFileNames))
        {
            diffFiles.Add(new SnapshotDiffFileDto
            {
                Path = file,
                ChangeType = "added"
            });
        }

        // Removed files (in fromSnapshot but not in toSnapshot)
        foreach (var file in fromFileNames.Except(toFileNames))
        {
            diffFiles.Add(new SnapshotDiffFileDto
            {
                Path = file,
                ChangeType = "removed"
            });
        }

        // Modified files (in both, need to compare content)
        foreach (var file in fromFileNames.Intersect(toFileNames))
        {
            // For now, mark all common files as potentially modified
            // Full content comparison would be expensive
            diffFiles.Add(new SnapshotDiffFileDto
            {
                Path = file,
                ChangeType = "modified"
            });
        }

        return new SnapshotDiffDto
        {
            FromSnapshotId = fromSnapshotId,
            ToSnapshotId = toSnapshotId,
            Files = diffFiles,
            KeysAdded = toSnapshot.KeyCount - fromSnapshot.KeyCount > 0
                ? toSnapshot.KeyCount - fromSnapshot.KeyCount : 0,
            KeysRemoved = fromSnapshot.KeyCount - toSnapshot.KeyCount > 0
                ? fromSnapshot.KeyCount - toSnapshot.KeyCount : 0,
            KeysModified = 0 // Would require detailed comparison
        };
    }

    /// <summary>
    /// Applies retention policy by deleting old snapshots.
    /// </summary>
    private async Task ApplyRetentionPolicyAsync(int projectId, int? retentionDays, int? maxSnapshots)
    {
        // Delete snapshots older than retention period
        if (retentionDays.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays.Value);
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
                    oldSnapshots.Count, projectId, retentionDays.Value);
            }
        }

        // Keep only maxSnapshots most recent
        if (maxSnapshots.HasValue)
        {
            var excessSnapshots = await _db.Set<Snapshot>()
                .Where(s => s.ProjectId == projectId)
                .OrderByDescending(s => s.CreatedAt)
                .Skip(maxSnapshots.Value)
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
                    excessSnapshots.Count, projectId, maxSnapshots.Value);
            }
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

    /// <summary>
    /// Extracts language code from filename.
    /// </summary>
    private static string ExtractLanguageCode(string fileName)
    {
        // Handle patterns like: strings.json, strings.de.json, de.json
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var parts = nameWithoutExt.Split('.');

        if (parts.Length > 1)
            return parts[^1];

        return parts[0];
    }
}
