// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for key-level synchronization.
/// Provides three-way merge with conflict detection for CLI push/pull operations.
/// </summary>
[Route("api/projects/{projectId}/sync")]
[Authorize]
public class SyncController : ApiControllerBase
{
    private readonly IKeySyncService _syncService;
    private readonly ISyncHistoryService _historyService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        IKeySyncService syncService,
        ISyncHistoryService historyService,
        ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Pushes local changes to the server with conflict detection.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Push request with entry changes, deletions, and config</param>
    /// <returns>Push result with applied count, conflicts, and new hashes</returns>
    /// <response code="200">Push successful (may include conflicts)</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to push to this project</response>
    [HttpPost("push")]
    [ProducesResponseType(typeof(ApiResponse<KeySyncPushResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<KeySyncPushResponse>>> Push(
        int projectId,
        [FromBody] KeySyncPushRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var result = await _syncService.PushAsync(projectId, userId, request, ct);

            if (result.Conflicts.Count > 0)
            {
                _logger.LogInformation(
                    "User {UserId} push to project {ProjectId} has {ConflictCount} conflicts",
                    userId, projectId, result.Conflicts.Count);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} pushed {Applied} entries, deleted {Deleted} to project {ProjectId}",
                    userId, result.Applied, result.Deleted, projectId);
            }

            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
    }

    /// <summary>
    /// Pulls all entries from the server for merge.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="since">Optional timestamp for delta sync (only return entries modified after this time)</param>
    /// <param name="limit">Optional limit for pagination</param>
    /// <param name="offset">Optional offset for pagination</param>
    /// <returns>All entries with their translations and hashes</returns>
    /// <response code="200">Pull successful</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to access this project</response>
    [HttpGet("pull")]
    [ProducesResponseType(typeof(ApiResponse<KeySyncPullResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<KeySyncPullResponse>>> Pull(
        int projectId,
        [FromQuery] DateTime? since = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        try
        {
            var result = await _syncService.PullAsync(projectId, userId, since, limit, offset, ct);

            _logger.LogInformation(
                "User {UserId} pulled {EntryCount} entries from project {ProjectId} (incremental: {IsIncremental})",
                userId, result.Entries.Count, projectId, result.IsIncremental);

            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound("PROJECT_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Resolves conflicts after a push operation.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Conflict resolution request with user's choices</param>
    /// <returns>Resolution result with applied count and new hashes</returns>
    /// <response code="200">Conflicts resolved successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to resolve conflicts in this project</response>
    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ApiResponse<ConflictResolutionResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<ConflictResolutionResponse>>> ResolveConflicts(
        int projectId,
        [FromBody] ConflictResolutionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var result = await _syncService.ResolveConflictsAsync(projectId, userId, request, ct);

            _logger.LogInformation(
                "User {UserId} resolved {Count} conflicts in project {ProjectId}",
                userId, result.Applied, projectId);

            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
    }

    /// <summary>
    /// Gets the sync history for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of history entries</returns>
    /// <response code="200">History retrieved successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to access this project</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<SyncHistoryListResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<SyncHistoryListResponse>>> GetHistory(
        int projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        try
        {
            var result = await _historyService.GetHistoryAsync(projectId, userId, page, pageSize, ct);
            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
    }

    /// <summary>
    /// Gets details of a specific history entry including the diff.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="historyId">History entry ID (8-character string)</param>
    /// <returns>Full history entry with changes</returns>
    /// <response code="200">History entry retrieved successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to access this project</response>
    /// <response code="404">History entry not found</response>
    [HttpGet("history/{historyId}")]
    [ProducesResponseType(typeof(ApiResponse<SyncHistoryDetailDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SyncHistoryDetailDto>>> GetHistoryDetail(
        int projectId,
        string historyId,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        try
        {
            var result = await _historyService.GetHistoryDetailAsync(projectId, historyId, userId, ct);

            if (result == null)
            {
                return NotFound("HISTORY_NOT_FOUND", $"History entry '{historyId}' not found");
            }

            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
    }

    /// <summary>
    /// Reverts a project to the state before a specific push.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="historyId">History entry ID to revert</param>
    /// <param name="request">Optional message for the revert</param>
    /// <returns>The new history entry created for the revert</returns>
    /// <response code="200">Revert successful</response>
    /// <response code="400">Cannot revert (already reverted or no changes)</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to revert in this project</response>
    /// <response code="404">History entry not found</response>
    [HttpPost("history/{historyId}/revert")]
    [ProducesResponseType(typeof(ApiResponse<RevertResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<RevertResponse>>> Revert(
        int projectId,
        string historyId,
        [FromBody] RevertRequest? request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        try
        {
            var revertHistory = await _historyService.RevertToAsync(
                projectId, historyId, userId, request?.Message, ct);

            var response = new RevertResponse
            {
                Success = true,
                History = new SyncHistoryDto
                {
                    HistoryId = revertHistory.HistoryId,
                    OperationType = revertHistory.OperationType,
                    Source = revertHistory.Source,
                    Message = revertHistory.Message,
                    EntriesAdded = revertHistory.EntriesAdded,
                    EntriesModified = revertHistory.EntriesModified,
                    EntriesDeleted = revertHistory.EntriesDeleted,
                    Status = revertHistory.Status,
                    CreatedAt = revertHistory.CreatedAt
                },
                EntriesRestored = revertHistory.EntriesAdded + revertHistory.EntriesModified + revertHistory.EntriesDeleted
            };

            _logger.LogInformation(
                "User {UserId} reverted history {HistoryId} in project {ProjectId}",
                userId, historyId, projectId);

            return Success(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("SYNC_FORBIDDEN", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("REVERT_FAILED", ex.Message);
        }
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}
