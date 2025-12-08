using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for managing localization projects.
/// </summary>
[Authorize]
public class ProjectsController : ApiControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all projects accessible by the current user (personal + organization).
    /// </summary>
    /// <returns>List of projects</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ProjectDto>>>> GetProjects()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var projects = await _projectService.GetUserProjectsAsync(userId);
        return Success(projects);
    }

    /// <summary>
    /// Gets a specific project by ID.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <returns>Project details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var project = await _projectService.GetProjectAsync(id, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(project);
    }

    /// <summary>
    /// Creates a new project (personal or organization).
    /// </summary>
    /// <param name="request">Project creation request</param>
    /// <returns>Created project</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject(
        [FromBody] CreateProjectRequest request)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var (success, project, errorMessage) = await _projectService.CreateProjectAsync(userId, request);

        if (!success)
            return BadRequest("PRJ_CREATE_FAILED", errorMessage!);

        _logger.LogInformation("User {UserId} created project {ProjectId}", userId, project!.Id);
        return Created(nameof(GetProject), new { id = project.Id }, project);
    }

    /// <summary>
    /// Updates an existing project.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <param name="request">Project update request</param>
    /// <returns>Updated project</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(
        int id, [FromBody] UpdateProjectRequest request)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var (success, project, errorMessage) = await _projectService.UpdateProjectAsync(id, userId, request);

        if (!success)
        {
            if (errorMessage == "Project not found")
                return NotFound("PRJ_NOT_FOUND", errorMessage);

            return BadRequest("PRJ_UPDATE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} updated project {ProjectId}", userId, id);
        return Success(project!);
    }

    /// <summary>
    /// Deletes a project.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> DeleteProject(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var (success, errorMessage) = await _projectService.DeleteProjectAsync(id, userId);

        if (!success)
        {
            if (errorMessage == "Project not found")
                return NotFound("PRJ_NOT_FOUND", errorMessage);

            return BadRequest("PRJ_DELETE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} deleted project {ProjectId}", userId, id);
        return Success("Project deleted successfully");
    }

    /// <summary>
    /// Triggers a sync from GitHub for a project.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <returns>Success response</returns>
    [HttpPost("{id}/sync")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> TriggerSync(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var (success, errorMessage) = await _projectService.TriggerSyncAsync(id, userId);

        if (!success)
        {
            if (errorMessage == "Project not found")
                return NotFound("PRJ_NOT_FOUND", errorMessage);

            return BadRequest("PRJ_SYNC_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} triggered sync for project {ProjectId}", userId, id);
        return Success(errorMessage ?? "Sync triggered successfully");
    }

    /// <summary>
    /// Gets the project configuration (lrm.json).
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <returns>Project configuration</returns>
    [HttpGet("{id}/configuration")]
    [ProducesResponseType(typeof(ApiResponse<ConfigurationDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ConfigurationDto>>> GetConfiguration(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var configuration = await _projectService.GetConfigurationAsync(id, userId);

        if (configuration == null)
            return NotFound("CFG_NOT_FOUND", "Configuration not found or access denied");

        return Success(configuration);
    }

    /// <summary>
    /// Updates the project configuration (lrm.json).
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <param name="request">Configuration update request</param>
    /// <returns>Updated configuration</returns>
    [HttpPut("{id}/configuration")]
    [ProducesResponseType(typeof(ApiResponse<ConfigurationDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<ApiResponse<ConfigurationDto>>> UpdateConfiguration(
        int id, [FromBody] UpdateConfigurationRequest request)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var (success, configuration, errorMessage) = await _projectService.UpdateConfigurationAsync(id, userId, request);

        if (!success)
        {
            if (errorMessage == "Configuration not found")
                return NotFound("CFG_NOT_FOUND", errorMessage);

            if (errorMessage == "Configuration conflict")
                return Conflict("CFG_CONFLICT", "Configuration has been modified by another user. Please pull and merge changes.");

            return BadRequest("CFG_UPDATE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} updated configuration for project {ProjectId}", userId, id);
        return Success(configuration!);
    }

    /// <summary>
    /// Gets the configuration history for a project.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <param name="limit">Maximum number of history entries to return</param>
    /// <returns>Configuration history</returns>
    [HttpGet("{id}/configuration/history")]
    [ProducesResponseType(typeof(ApiResponse<List<ConfigurationHistoryDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<List<ConfigurationHistoryDto>>>> GetConfigurationHistory(
        int id, [FromQuery] int limit = 50)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var history = await _projectService.GetConfigurationHistoryAsync(id, userId, limit);

        if (history == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(history);
    }

    /// <summary>
    /// Gets the sync status for a project.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <returns>Sync status</returns>
    [HttpGet("{id}/sync/status")]
    [ProducesResponseType(typeof(ApiResponse<SyncStatusDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SyncStatusDto>>> GetSyncStatus(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var status = await _projectService.GetSyncStatusAsync(id, userId);

        if (status == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(status);
    }
}
