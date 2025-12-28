// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Ota;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// OTA (Over-The-Air) localization endpoints for .NET client library.
/// Provides lightweight bundle delivery for runtime translation updates.
/// </summary>
[Authorize]
[Route("api/ota")]
public class OtaController : ApiControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IOtaService _otaService;
    private readonly ILogger<OtaController> _logger;

    public OtaController(
        IProjectService projectService,
        IOtaService otaService,
        ILogger<OtaController> logger)
    {
        _projectService = projectService;
        _otaService = otaService;
        _logger = logger;
    }

    // ============================================================
    // User Project Endpoints
    // ============================================================

    /// <summary>
    /// Gets the OTA bundle for a user project.
    /// </summary>
    /// <param name="username">Username of the project owner</param>
    /// <param name="project">Project slug</param>
    /// <param name="languages">Optional comma-separated language filter</param>
    /// <param name="since">Optional timestamp for delta updates (ISO 8601)</param>
    /// <returns>OTA bundle with translations</returns>
    [HttpGet("users/{username}/{project}/bundle")]
    [ProducesResponseType(typeof(OtaBundleDto), 200)]
    [ProducesResponseType(304)] // Not Modified
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetUserProjectBundle(
        string username,
        string project,
        [FromQuery] string? languages = null,
        [FromQuery] DateTime? since = null)
    {
        // Validate read scope
        var scopeError = ValidateReadScope();
        if (scopeError != null) return scopeError;

        // Get user ID from claims
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("OTA_UNAUTHORIZED", "Authentication required");

        // Resolve project
        var projectDto = await _projectService.GetProjectByNameAsync(username, project, userId.Value);
        if (projectDto == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found or access denied");

        // Validate project-scoped API key if applicable
        var projectScopeError = ValidateProjectScope(projectDto.Id);
        if (projectScopeError != null) return projectScopeError;

        // Parse languages
        var languageList = ParseLanguages(languages);

        // Get bundle
        var projectPath = $"@{username}/{project}";
        var bundle = await _otaService.GetBundleAsync(projectDto.Id, projectPath, languageList, since);

        if (bundle == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found");

        // Handle ETag for caching
        var etag = $"\"{_otaService.ComputeETag(bundle.Version)}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch))
        {
            if (ifNoneMatch.ToString() == etag)
            {
                return StatusCode(304);
            }
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=60";

        _logger.LogInformation("OTA bundle served for @{Username}/{Project}, version {Version}",
            username, project, bundle.Version);

        return Ok(bundle);
    }

    /// <summary>
    /// Gets the version timestamp for a user project (for efficient polling).
    /// </summary>
    /// <param name="username">Username of the project owner</param>
    /// <param name="project">Project slug</param>
    /// <returns>Version timestamp</returns>
    [HttpGet("users/{username}/{project}/version")]
    [ProducesResponseType(typeof(OtaVersionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetUserProjectVersion(
        string username,
        string project)
    {
        // Validate read scope
        var scopeError = ValidateReadScope();
        if (scopeError != null) return scopeError;

        // Get user ID from claims
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("OTA_UNAUTHORIZED", "Authentication required");

        // Resolve project
        var projectDto = await _projectService.GetProjectByNameAsync(username, project, userId.Value);
        if (projectDto == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found or access denied");

        // Validate project-scoped API key if applicable
        var projectScopeError = ValidateProjectScope(projectDto.Id);
        if (projectScopeError != null) return projectScopeError;

        // Get version
        var version = await _otaService.GetVersionAsync(projectDto.Id);
        if (version == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found");

        return Ok(version);
    }

    // ============================================================
    // Organization Project Endpoints
    // ============================================================

    /// <summary>
    /// Gets the OTA bundle for an organization project.
    /// </summary>
    /// <param name="orgSlug">Organization slug</param>
    /// <param name="project">Project slug</param>
    /// <param name="languages">Optional comma-separated language filter</param>
    /// <param name="since">Optional timestamp for delta updates (ISO 8601)</param>
    /// <returns>OTA bundle with translations</returns>
    [HttpGet("orgs/{orgSlug}/{project}/bundle")]
    [ProducesResponseType(typeof(OtaBundleDto), 200)]
    [ProducesResponseType(304)] // Not Modified
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetOrgProjectBundle(
        string orgSlug,
        string project,
        [FromQuery] string? languages = null,
        [FromQuery] DateTime? since = null)
    {
        // Validate read scope
        var scopeError = ValidateReadScope();
        if (scopeError != null) return scopeError;

        // Get user ID from claims
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("OTA_UNAUTHORIZED", "Authentication required");

        // Resolve project
        var projectDto = await _projectService.GetProjectByOrgSlugAsync(orgSlug, project, userId.Value);
        if (projectDto == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found or access denied");

        // Validate project-scoped API key if applicable
        var projectScopeError = ValidateProjectScope(projectDto.Id);
        if (projectScopeError != null) return projectScopeError;

        // Parse languages
        var languageList = ParseLanguages(languages);

        // Get bundle
        var projectPath = $"{orgSlug}/{project}";
        var bundle = await _otaService.GetBundleAsync(projectDto.Id, projectPath, languageList, since);

        if (bundle == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found");

        // Handle ETag for caching
        var etag = $"\"{_otaService.ComputeETag(bundle.Version)}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch))
        {
            if (ifNoneMatch.ToString() == etag)
            {
                return StatusCode(304);
            }
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=60";

        _logger.LogInformation("OTA bundle served for {OrgSlug}/{Project}, version {Version}",
            orgSlug, project, bundle.Version);

        return Ok(bundle);
    }

    /// <summary>
    /// Gets the version timestamp for an organization project (for efficient polling).
    /// </summary>
    /// <param name="orgSlug">Organization slug</param>
    /// <param name="project">Project slug</param>
    /// <returns>Version timestamp</returns>
    [HttpGet("orgs/{orgSlug}/{project}/version")]
    [ProducesResponseType(typeof(OtaVersionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetOrgProjectVersion(
        string orgSlug,
        string project)
    {
        // Validate read scope
        var scopeError = ValidateReadScope();
        if (scopeError != null) return scopeError;

        // Get user ID from claims
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("OTA_UNAUTHORIZED", "Authentication required");

        // Resolve project
        var projectDto = await _projectService.GetProjectByOrgSlugAsync(orgSlug, project, userId.Value);
        if (projectDto == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found or access denied");

        // Validate project-scoped API key if applicable
        var projectScopeError = ValidateProjectScope(projectDto.Id);
        if (projectScopeError != null) return projectScopeError;

        // Get version
        var version = await _otaService.GetVersionAsync(projectDto.Id);
        if (version == null)
            return NotFound("OTA_PROJECT_NOT_FOUND", "Project not found");

        return Ok(version);
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    /// <summary>
    /// Gets the user ID from claims.
    /// </summary>
    private int? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Validates that the API key has read scope.
    /// </summary>
    private IActionResult? ValidateReadScope()
    {
        // Check if using API key authentication
        var authType = User.FindFirst("auth_type")?.Value;
        if (authType != "api_key")
        {
            // JWT or other auth - all scopes implied
            return null;
        }

        // Get scopes claim
        var scopes = User.FindFirst("scopes")?.Value ?? "";
        var scopeList = scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Check for read, write, or admin scope (write/admin imply read)
        if (!scopeList.Any(s => s.Equals("read", StringComparison.OrdinalIgnoreCase) ||
                                s.Equals("write", StringComparison.OrdinalIgnoreCase) ||
                                s.Equals("admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Forbid("OTA_INSUFFICIENT_SCOPE", "API key requires read scope for OTA access");
        }

        return null;
    }

    /// <summary>
    /// Validates that a project-scoped API key matches the requested project.
    /// </summary>
    private IActionResult? ValidateProjectScope(int requestedProjectId)
    {
        // Check if API key is project-scoped
        var projectIdClaim = User.FindFirst("project_id")?.Value;
        if (string.IsNullOrEmpty(projectIdClaim))
        {
            // Not project-scoped, access to all user's projects
            return null;
        }

        if (!int.TryParse(projectIdClaim, out var scopedProjectId))
        {
            return Forbid("OTA_INVALID_PROJECT_SCOPE", "Invalid project scope in API key");
        }

        if (scopedProjectId != requestedProjectId)
        {
            return Forbid("OTA_PROJECT_SCOPE_MISMATCH", "API key does not have access to this project");
        }

        return null;
    }

    /// <summary>
    /// Parses comma-separated languages string.
    /// </summary>
    private static List<string>? ParseLanguages(string? languages)
    {
        if (string.IsNullOrWhiteSpace(languages))
        {
            return null;
        }

        return languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
    }
}
