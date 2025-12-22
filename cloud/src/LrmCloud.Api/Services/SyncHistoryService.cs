// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Entities = LrmCloud.Shared.Entities;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing sync history - logging push operations and enabling revert.
/// </summary>
public class SyncHistoryService : ISyncHistoryService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<SyncHistoryService> _logger;

    public SyncHistoryService(
        AppDbContext db,
        IProjectService projectService,
        ILogger<SyncHistoryService> logger)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Records a push operation in history.
    /// </summary>
    public async Task<SyncHistory> RecordPushAsync(
        int projectId,
        int userId,
        string? message,
        List<SyncChangeEntry> changes,
        string operationType = "push",
        CancellationToken ct = default)
    {
        var history = new SyncHistory
        {
            HistoryId = GenerateHistoryId(),
            ProjectId = projectId,
            UserId = userId,
            OperationType = operationType,
            Message = message,
            EntriesAdded = changes.Count(c => c.ChangeType == "added"),
            EntriesModified = changes.Count(c => c.ChangeType == "modified"),
            EntriesDeleted = changes.Count(c => c.ChangeType == "deleted"),
            ChangesJson = JsonSerializer.Serialize(new SyncChangesData { Changes = changes }),
            Status = "completed",
            CreatedAt = DateTime.UtcNow
        };

        _db.SyncHistory.Add(history);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recorded push history {HistoryId} for project {ProjectId} by user {UserId}: +{Added} ~{Modified} -{Deleted}",
            history.HistoryId, projectId, userId,
            history.EntriesAdded, history.EntriesModified, history.EntriesDeleted);

        return history;
    }

    /// <summary>
    /// Gets paginated history for a project.
    /// </summary>
    public async Task<SyncHistoryListResponse> GetHistoryAsync(
        int projectId,
        int userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to view this project's history");
        }

        var query = _db.SyncHistory
            .Include(h => h.User)
            .Where(h => h.ProjectId == projectId)
            .OrderByDescending(h => h.CreatedAt);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new SyncHistoryDto
            {
                HistoryId = h.HistoryId,
                OperationType = h.OperationType,
                Message = h.Message,
                UserEmail = h.User != null ? h.User.Email : null,
                UserName = h.User != null ? h.User.DisplayName ?? h.User.Username : null,
                EntriesAdded = h.EntriesAdded,
                EntriesModified = h.EntriesModified,
                EntriesDeleted = h.EntriesDeleted,
                Status = h.Status,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync(ct);

        return new SyncHistoryListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            HasMore = total > page * pageSize
        };
    }

    /// <summary>
    /// Gets a specific history entry with full diff.
    /// </summary>
    public async Task<SyncHistoryDetailDto?> GetHistoryDetailAsync(
        int projectId,
        string historyId,
        int userId,
        CancellationToken ct = default)
    {
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to view this project's history");
        }

        var history = await _db.SyncHistory
            .Include(h => h.User)
            .Include(h => h.RevertedFrom)
            .FirstOrDefaultAsync(h => h.ProjectId == projectId && h.HistoryId == historyId, ct);

        if (history == null)
        {
            return null;
        }

        var changes = new List<SyncChangeEntry>();
        if (!string.IsNullOrEmpty(history.ChangesJson))
        {
            var changesData = JsonSerializer.Deserialize<SyncChangesData>(history.ChangesJson);
            changes = changesData?.Changes ?? new List<SyncChangeEntry>();
        }

        return new SyncHistoryDetailDto
        {
            HistoryId = history.HistoryId,
            OperationType = history.OperationType,
            Message = history.Message,
            UserEmail = history.User?.Email,
            UserName = history.User?.DisplayName ?? history.User?.Username,
            EntriesAdded = history.EntriesAdded,
            EntriesModified = history.EntriesModified,
            EntriesDeleted = history.EntriesDeleted,
            Status = history.Status,
            CreatedAt = history.CreatedAt,
            Changes = changes.Select(c => new SyncChangeDto
            {
                Key = c.Key,
                Lang = c.Lang,
                ChangeType = c.ChangeType,
                BeforeValue = c.BeforeValue,
                AfterValue = c.AfterValue,
                BeforeComment = c.BeforeComment,
                AfterComment = c.AfterComment
            }).ToList(),
            RevertedFromId = history.RevertedFrom?.HistoryId
        };
    }

    /// <summary>
    /// Reverts a project to the state before a specific push.
    /// </summary>
    public async Task<SyncHistory> RevertToAsync(
        int projectId,
        string historyId,
        int userId,
        string? message,
        CancellationToken ct = default)
    {
        if (!await _projectService.CanManageResourcesAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to revert changes in this project");
        }

        // Get the history entry to revert
        var historyToRevert = await _db.SyncHistory
            .FirstOrDefaultAsync(h => h.ProjectId == projectId && h.HistoryId == historyId, ct);

        if (historyToRevert == null)
        {
            throw new InvalidOperationException($"History entry '{historyId}' not found");
        }

        if (historyToRevert.Status == "reverted")
        {
            throw new InvalidOperationException($"History entry '{historyId}' has already been reverted");
        }

        // Parse the changes that were made
        if (string.IsNullOrEmpty(historyToRevert.ChangesJson))
        {
            throw new InvalidOperationException($"History entry '{historyId}' has no changes to revert");
        }

        var changesData = JsonSerializer.Deserialize<SyncChangesData>(historyToRevert.ChangesJson);
        if (changesData?.Changes == null || changesData.Changes.Count == 0)
        {
            throw new InvalidOperationException($"History entry '{historyId}' has no changes to revert");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var revertChanges = new List<SyncChangeEntry>();

            // Apply inverse operations
            foreach (var change in changesData.Changes)
            {
                var resourceKey = await _db.ResourceKeys
                    .Include(k => k.Translations)
                    .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == change.Key, ct);

                switch (change.ChangeType)
                {
                    case "added":
                        // Revert addition = delete
                        if (resourceKey != null)
                        {
                            var translation = resourceKey.Translations
                                .FirstOrDefault(t => t.LanguageCode == change.Lang && t.PluralForm == "");
                            if (translation != null)
                            {
                                _db.Translations.Remove(translation);
                                revertChanges.Add(new SyncChangeEntry
                                {
                                    Key = change.Key,
                                    Lang = change.Lang,
                                    ChangeType = "deleted",
                                    BeforeValue = change.AfterValue,
                                    BeforeHash = change.AfterHash,
                                    AfterValue = null,
                                    AfterHash = null
                                });
                            }
                        }
                        break;

                    case "modified":
                        // Revert modification = restore previous value
                        if (resourceKey != null && change.BeforeValue != null)
                        {
                            var translation = resourceKey.Translations
                                .FirstOrDefault(t => t.LanguageCode == change.Lang && t.PluralForm == "");
                            if (translation != null)
                            {
                                revertChanges.Add(new SyncChangeEntry
                                {
                                    Key = change.Key,
                                    Lang = change.Lang,
                                    ChangeType = "modified",
                                    BeforeValue = translation.Value,
                                    BeforeHash = translation.Hash,
                                    AfterValue = change.BeforeValue,
                                    AfterHash = change.BeforeHash
                                });

                                translation.Value = change.BeforeValue;
                                translation.Comment = change.BeforeComment;
                                translation.Hash = change.BeforeHash;
                                translation.Version++;
                                translation.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                        break;

                    case "deleted":
                        // Revert deletion = re-add
                        if (change.BeforeValue != null)
                        {
                            if (resourceKey == null)
                            {
                                resourceKey = new ResourceKey
                                {
                                    ProjectId = projectId,
                                    KeyName = change.Key,
                                    Version = 1,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                _db.ResourceKeys.Add(resourceKey);
                                await _db.SaveChangesAsync(ct);
                            }

                            var translation = new Entities.Translation
                            {
                                ResourceKeyId = resourceKey.Id,
                                LanguageCode = change.Lang,
                                Value = change.BeforeValue,
                                Comment = change.BeforeComment,
                                Hash = change.BeforeHash,
                                Status = "translated",
                                PluralForm = "",
                                Version = 1,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _db.Translations.Add(translation);

                            revertChanges.Add(new SyncChangeEntry
                            {
                                Key = change.Key,
                                Lang = change.Lang,
                                ChangeType = "added",
                                BeforeValue = null,
                                BeforeHash = null,
                                AfterValue = change.BeforeValue,
                                AfterHash = change.BeforeHash
                            });
                        }
                        break;
                }
            }

            await _db.SaveChangesAsync(ct);

            // Mark original history as reverted
            historyToRevert.Status = "reverted";

            // Record the revert operation
            var revertHistory = new SyncHistory
            {
                HistoryId = GenerateHistoryId(),
                ProjectId = projectId,
                UserId = userId,
                OperationType = "revert",
                Message = message ?? $"Reverted '{historyId}'",
                EntriesAdded = revertChanges.Count(c => c.ChangeType == "added"),
                EntriesModified = revertChanges.Count(c => c.ChangeType == "modified"),
                EntriesDeleted = revertChanges.Count(c => c.ChangeType == "deleted"),
                ChangesJson = JsonSerializer.Serialize(new SyncChangesData { Changes = revertChanges }),
                RevertedFromId = historyToRevert.Id,
                Status = "completed",
                CreatedAt = DateTime.UtcNow
            };

            _db.SyncHistory.Add(revertHistory);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Reverted history {HistoryId} for project {ProjectId} by user {UserId}, created revert entry {RevertHistoryId}",
                historyId, projectId, userId, revertHistory.HistoryId);

            return revertHistory;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to revert history {HistoryId} for project {ProjectId}", historyId, projectId);
            throw;
        }
    }

    private static string GenerateHistoryId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}

/// <summary>
/// Interface for sync history service.
/// </summary>
public interface ISyncHistoryService
{
    Task<SyncHistory> RecordPushAsync(int projectId, int userId, string? message, List<SyncChangeEntry> changes, string operationType = "push", CancellationToken ct = default);
    Task<SyncHistoryListResponse> GetHistoryAsync(int projectId, int userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<SyncHistoryDetailDto?> GetHistoryDetailAsync(int projectId, string historyId, int userId, CancellationToken ct = default);
    Task<SyncHistory> RevertToAsync(int projectId, string historyId, int userId, string? message, CancellationToken ct = default);
}
