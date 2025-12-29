using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.DTOs.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for organization-based project access (orgSlug/projectSlug routes).
/// These endpoints provide a user-friendly URL structure for CLI access to organization projects.
/// </summary>
[Authorize]
[Route("api/organizations/{orgSlug}/projects/{projectSlug}")]
public class OrgProjectsController : ApiControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IResourceService _resourceService;
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrgProjectsController> _logger;

    public OrgProjectsController(
        IProjectService projectService,
        IResourceService resourceService,
        IOrganizationService organizationService,
        ILogger<OrgProjectsController> logger)
    {
        _projectService = projectService;
        _resourceService = resourceService;
        _organizationService = organizationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets project by organization slug and project slug.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(string orgSlug, string projectSlug)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByOrgSlugAsync(orgSlug, projectSlug, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(project);
    }

    /// <summary>
    /// Gets all resources for the project.
    /// </summary>
    [HttpGet("resources")]
    [ProducesResponseType(typeof(ApiResponse<List<ResourceDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<List<ResourceDto>>>> GetResources(
        string orgSlug,
        string projectSlug,
        [FromQuery] string? language = null)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByOrgSlugAsync(orgSlug, projectSlug, userId);

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
    public async Task<ActionResult<ApiResponse<SyncStatusDto>>> GetSyncStatus(string orgSlug, string projectSlug)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var project = await _projectService.GetProjectByOrgSlugAsync(orgSlug, projectSlug, userId);

        if (project == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        var status = await _projectService.GetSyncStatusAsync(project.Id, userId);

        if (status == null)
            return NotFound("PRJ_NOT_FOUND", "Project not found or access denied");

        return Success(status);
    }

}
