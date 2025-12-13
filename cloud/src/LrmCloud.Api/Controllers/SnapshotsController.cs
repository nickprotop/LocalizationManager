using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Snapshots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for managing project snapshots (point-in-time backups).
/// </summary>
[Authorize]
[Route("api/projects/{projectId}/snapshots")]
public class SnapshotsController : ApiControllerBase
{
    private readonly SnapshotService _snapshotService;
    private readonly IProjectService _projectService;
    private readonly ILogger<SnapshotsController> _logger;

    public SnapshotsController(
        SnapshotService snapshotService,
        IProjectService projectService,
        ILogger<SnapshotsController> logger)
    {
        _snapshotService = snapshotService;
        _projectService = projectService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Lists all snapshots for a project.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SnapshotDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<List<SnapshotDto>>>> ListSnapshots(
        int projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();

        if (!await _projectService.CanViewProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have access to this project");

        var result = await _snapshotService.ListSnapshotsAsync(projectId, page, pageSize);
        return Paginated(result.Items, result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>
    /// Gets a specific snapshot with its file list.
    /// </summary>
    [HttpGet("{snapshotId}")]
    [ProducesResponseType(typeof(ApiResponse<SnapshotDetailDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SnapshotDetailDto>>> GetSnapshot(
        int projectId,
        string snapshotId)
    {
        var userId = GetUserId();

        if (!await _projectService.CanViewProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have access to this project");

        var snapshot = await _snapshotService.GetSnapshotAsync(projectId, snapshotId);
        if (snapshot == null)
            return NotFound("SNAPSHOT_NOT_FOUND", "Snapshot not found");

        return Success(snapshot);
    }

    /// <summary>
    /// Creates a manual snapshot of the current project state.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SnapshotDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<SnapshotDto>>> CreateSnapshot(
        int projectId,
        [FromBody] CreateSnapshotRequest request)
    {
        var userId = GetUserId();

        if (!await _projectService.CanEditProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have permission to create snapshots");

        var snapshot = await _snapshotService.CreateSnapshotAsync(
            projectId,
            userId,
            "manual",
            request.Description ?? "Manual snapshot");

        _logger.LogInformation("User {UserId} created manual snapshot {SnapshotId} for project {ProjectId}",
            userId, snapshot.SnapshotId, projectId);

        return Created(nameof(GetSnapshot), new { projectId, snapshotId = snapshot.SnapshotId }, new SnapshotDto
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
            CreatedAt = snapshot.CreatedAt
        });
    }

    /// <summary>
    /// Restores the project to a previous snapshot.
    /// </summary>
    [HttpPost("{snapshotId}/restore")]
    [ProducesResponseType(typeof(ApiResponse<SnapshotDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SnapshotDto>>> RestoreSnapshot(
        int projectId,
        string snapshotId,
        [FromBody] RestoreSnapshotRequest request)
    {
        var userId = GetUserId();

        if (!await _projectService.CanEditProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have permission to restore snapshots");

        var snapshot = await _snapshotService.RestoreSnapshotAsync(
            projectId,
            snapshotId,
            userId,
            request.CreateBackup,
            request.Message);

        if (snapshot == null)
            return NotFound("SNAPSHOT_NOT_FOUND", "Snapshot not found");

        _logger.LogInformation("User {UserId} restored snapshot {SnapshotId} for project {ProjectId}",
            userId, snapshotId, projectId);

        return Success(new SnapshotDto
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
            CreatedAt = snapshot.CreatedAt
        });
    }

    /// <summary>
    /// Compares two snapshots and returns the differences.
    /// </summary>
    [HttpGet("{fromSnapshotId}/diff/{toSnapshotId}")]
    [ProducesResponseType(typeof(ApiResponse<SnapshotDiffDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SnapshotDiffDto>>> DiffSnapshots(
        int projectId,
        string fromSnapshotId,
        string toSnapshotId)
    {
        var userId = GetUserId();

        if (!await _projectService.CanViewProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have access to this project");

        var diff = await _snapshotService.DiffSnapshotsAsync(projectId, fromSnapshotId, toSnapshotId);
        if (diff == null)
            return NotFound("SNAPSHOT_NOT_FOUND", "One or both snapshots not found");

        return Success(diff);
    }

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    [HttpDelete("{snapshotId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult> DeleteSnapshot(
        int projectId,
        string snapshotId)
    {
        var userId = GetUserId();

        if (!await _projectService.CanEditProjectAsync(projectId, userId))
            return Forbidden("PRJ_ACCESS_DENIED", "You don't have permission to delete snapshots");

        var deleted = await _snapshotService.DeleteSnapshotAsync(projectId, snapshotId);
        if (!deleted)
            return NotFound("SNAPSHOT_NOT_FOUND", "Snapshot not found");

        _logger.LogInformation("User {UserId} deleted snapshot {SnapshotId} for project {ProjectId}",
            userId, snapshotId, projectId);

        return NoContent();
    }
}
