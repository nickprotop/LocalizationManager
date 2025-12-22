using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.DTOs.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for user-based project access (username/projectName routes).
/// These endpoints provide a user-friendly URL structure for CLI access.
/// </summary>
[Authorize]
[Route("api/users/{username}/projects/{projectName}")]
public class UserProjectsController : ApiControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IResourceService _resourceService;
    private readonly ILogger<UserProjectsController> _logger;

    public UserProjectsController(
        IProjectService projectService,
        IResourceService resourceService,
        ILogger<UserProjectsController> logger)
    {
        _projectService = projectService;
        _resourceService = resourceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets project by username and project name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(string username, string projectName)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(project);
    }

    /// <summary>
    /// Gets the project configuration (lrm.json).
    /// </summary>
    [HttpGet("configuration")]
    [ProducesResponseType(typeof(ApiResponse<ConfigurationDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ConfigurationDto>>> GetConfiguration(string username, string projectName)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var configuration = await _projectService.GetConfigurationAsync(project.Id, userId);

        if (configuration == null)
            return NotFound("CFG_NOT_FOUND", "Configuration not found");

        return Success(configuration);
    }

    /// <summary>
    /// Updates the project configuration (lrm.json).
    /// </summary>
    [HttpPut("configuration")]
    [ProducesResponseType(typeof(ApiResponse<ConfigurationDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<ApiResponse<ConfigurationDto>>> UpdateConfiguration(
        string username,
        string projectName,
        [FromBody] UpdateConfigurationRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var (success, configuration, errorMessage) = await _projectService.UpdateConfigurationAsync(project.Id, userId, request);

        if (!success)
        {
            if (errorMessage == "Configuration not found")
                return NotFound("CFG_NOT_FOUND", errorMessage);

            if (errorMessage == "Configuration conflict")
                return Conflict("CFG_CONFLICT", "Configuration has been modified by another user. Please pull and merge changes.");

            return BadRequest("CFG_UPDATE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} updated configuration for project {ProjectId} via username/projectName route", userId, project.Id);
        return Success(configuration!);
    }

    /// <summary>
    /// Gets all resources for the project.
    /// </summary>
    [HttpGet("resources")]
    [ProducesResponseType(typeof(ApiResponse<List<ResourceDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<List<ResourceDto>>>> GetResources(
        string username,
        string projectName,
        [FromQuery] string? language = null)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var resources = await _resourceService.GetResourcesAsync(project.Id, language, userId);
        return Success(resources);
    }

    /// <summary>
    /// Gets sync status for the project.
    /// </summary>
    [HttpGet("sync/status")]
    [ProducesResponseType(typeof(ApiResponse<SyncStatusDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<SyncStatusDto>>> GetSyncStatus(string username, string projectName)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var status = await _projectService.GetSyncStatusAsync(project.Id, userId);

        if (status == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(status);
    }

    /// <summary>
    /// Gets configuration history.
    /// </summary>
    [HttpGet("configuration/history")]
    [ProducesResponseType(typeof(ApiResponse<List<ConfigurationHistoryDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<List<ConfigurationHistoryDto>>>> GetConfigurationHistory(
        string username,
        string projectName,
        [FromQuery] int limit = 50)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByNameAsync(username, projectName, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var history = await _projectService.GetConfigurationHistoryAsync(project.Id, userId, limit);

        if (history == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(history);
    }
}
