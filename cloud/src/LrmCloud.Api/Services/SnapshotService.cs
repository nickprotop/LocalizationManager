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
    /// </summary>
    public async Task<Snapshot> CreateSnapshotAsync(
        int projectId,
        int? userId,
        string snapshotType,
        string? description = null)
    {
        var snapshotId = GenerateSnapshotId();
        var storagePath = $"snapshots/{snapshotId}/";

        // Create snapshot in MinIO storage
        await _storageService.CreateSnapshotAsync(projectId, snapshotId);

        // Get file count from storage
        var files = await _storageService.ListSnapshotFilesAsync(projectId, snapshotId);

        // Get counts from database
        var keyCount = await _db.ResourceKeys.CountAsync(k => k.ProjectId == projectId);
        var translationCount = await _db.Translations
            .CountAsync(t => t.ResourceKey != null && t.ResourceKey.ProjectId == projectId);

        // Create snapshot record
        var snapshot = new Snapshot
        {
            ProjectId = projectId,
            SnapshotId = snapshotId,
            CreatedByUserId = userId,
            Description = description,
            StoragePath = storagePath,
            FileCount = files.Count,
            KeyCount = keyCount,
            TranslationCount = translationCount,
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

        _logger.LogInformation("Created snapshot {SnapshotId} for project {ProjectId} by user {UserId}",
            snapshotId, projectId, userId);

        return snapshot;
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
            await CreateSnapshotAsync(projectId, userId, "restore",
                $"Backup before restore to {snapshotId}");
        }

        // Restore files from snapshot
        await _storageService.RestoreFromSnapshotAsync(projectId, snapshotId);

        // TODO: Also restore database state from snapshot
        // This would require storing database state in the snapshot as well
        // For now, we only restore files

        _logger.LogInformation("Restored snapshot {SnapshotId} for project {ProjectId} by user {UserId}",
            snapshotId, projectId, userId);

        // Create a new snapshot record for the restore event
        var restoreSnapshot = await CreateSnapshotAsync(projectId, userId, "restore",
            message ?? $"Restored from snapshot {snapshotId}");

        return restoreSnapshot;
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
