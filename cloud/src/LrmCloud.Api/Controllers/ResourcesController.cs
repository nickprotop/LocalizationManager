using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for managing resource keys and translations.
/// </summary>
[Route("api/projects/{projectId}")]
[Authorize]
public class ResourcesController : ApiControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(
        IResourceService resourceService,
        ILogger<ResourcesController> logger)
    {
        _resourceService = resourceService;
        _logger = logger;
    }

    // ============================================================
    // Resource Keys
    // ============================================================

    /// <summary>
    /// Gets all resource keys for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>List of resource keys</returns>
    [HttpGet("keys")]
    [ProducesResponseType(typeof(ApiResponse<List<ResourceKeyDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ResourceKeyDto>>>> GetResourceKeys(int projectId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var keys = await _resourceService.GetResourceKeysAsync(projectId, userId);
        return Success(keys);
    }

    /// <summary>
    /// Gets a specific resource key with all translations.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="keyName">Resource key name</param>
    /// <returns>Resource key details</returns>
    [HttpGet("keys/{keyName}")]
    [ProducesResponseType(typeof(ApiResponse<ResourceKeyDetailDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ResourceKeyDetailDto>>> GetResourceKey(
        int projectId, string keyName)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var key = await _resourceService.GetResourceKeyAsync(projectId, keyName, userId);

        if (key == null)
            return NotFound("RES_KEY_NOT_FOUND", "Resource key not found or access denied");

        return Success(key);
    }

    /// <summary>
    /// Creates a new resource key.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Resource key creation request</param>
    /// <returns>Created resource key</returns>
    [HttpPost("keys")]
    [ProducesResponseType(typeof(ApiResponse<ResourceKeyDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<ResourceKeyDto>>> CreateResourceKey(
        int projectId, [FromBody] CreateResourceKeyRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (success, key, errorMessage) = await _resourceService.CreateResourceKeyAsync(projectId, userId, request);

        if (!success)
            return BadRequest("RES_KEY_CREATE_FAILED", errorMessage!);

        _logger.LogInformation("User {UserId} created resource key {KeyName} in project {ProjectId}",
            userId, key!.KeyName, projectId);

        return Created(nameof(GetResourceKey), new { projectId, keyName = key.KeyName }, key);
    }

    /// <summary>
    /// Updates a resource key.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="keyName">Resource key name</param>
    /// <param name="request">Resource key update request</param>
    /// <returns>Updated resource key</returns>
    [HttpPut("keys/{keyName}")]
    [ProducesResponseType(typeof(ApiResponse<ResourceKeyDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ResourceKeyDto>>> UpdateResourceKey(
        int projectId, string keyName, [FromBody] UpdateResourceKeyRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (success, key, errorMessage) = await _resourceService.UpdateResourceKeyAsync(
            projectId, keyName, userId, request);

        if (!success)
        {
            if (errorMessage == "Resource key not found")
                return NotFound("RES_KEY_NOT_FOUND", errorMessage);

            return BadRequest("RES_KEY_UPDATE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} updated resource key {KeyName} in project {ProjectId}",
            userId, keyName, projectId);

        return Success(key!);
    }

    /// <summary>
    /// Deletes a resource key and all its translations.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="keyName">Resource key name</param>
    /// <returns>Success response</returns>
    [HttpDelete("keys/{keyName}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> DeleteResourceKey(int projectId, string keyName)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (success, errorMessage) = await _resourceService.DeleteResourceKeyAsync(projectId, keyName, userId);

        if (!success)
        {
            if (errorMessage == "Resource key not found")
                return NotFound("RES_KEY_NOT_FOUND", errorMessage);

            return BadRequest("RES_KEY_DELETE_FAILED", errorMessage!);
        }

        _logger.LogInformation("User {UserId} deleted resource key {KeyName} from project {ProjectId}",
            userId, keyName, projectId);

        return Success("Resource key deleted successfully");
    }

    // ============================================================
    // Translations
    // ============================================================

    /// <summary>
    /// Updates a specific translation.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="keyName">Resource key name</param>
    /// <param name="languageCode">Language code (e.g., 'en', 'fr')</param>
    /// <param name="request">Translation update request</param>
    /// <returns>Updated translation</returns>
    [HttpPut("keys/{keyName}/translations/{languageCode}")]
    [ProducesResponseType(typeof(ApiResponse<TranslationDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<TranslationDto>>> UpdateTranslation(
        int projectId, string keyName, string languageCode, [FromBody] UpdateTranslationRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            projectId, keyName, languageCode, userId, request);

        if (!success)
        {
            if (errorMessage == "Resource key not found")
                return NotFound("RES_KEY_NOT_FOUND", errorMessage);

            return BadRequest("TRANS_UPDATE_FAILED", errorMessage!);
        }

        _logger.LogInformation(
            "User {UserId} updated translation for key {KeyName}, language {LanguageCode} in project {ProjectId}",
            userId, keyName, languageCode, projectId);

        return Success(translation!);
    }

    /// <summary>
    /// Bulk updates translations for a specific language.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="languageCode">Language code (e.g., 'en', 'fr')</param>
    /// <param name="updates">List of translation updates</param>
    /// <returns>Number of updated translations</returns>
    [HttpPost("translations/{languageCode}/bulk")]
    [ProducesResponseType(typeof(ApiResponse<int>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<int>>> BulkUpdateTranslations(
        int projectId, string languageCode, [FromBody] List<BulkTranslationUpdate> updates)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (success, updatedCount, errorMessage) = await _resourceService.BulkUpdateTranslationsAsync(
            projectId, languageCode, userId, updates);

        if (!success)
            return BadRequest("TRANS_BULK_UPDATE_FAILED", errorMessage!);

        _logger.LogInformation(
            "User {UserId} bulk updated {Count} translations for language {LanguageCode} in project {ProjectId}",
            userId, updatedCount, languageCode, projectId);

        return Success(updatedCount);
    }

    // ============================================================
    // Stats & Validation
    // ============================================================

    /// <summary>
    /// Gets translation statistics for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Project statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ProjectStatsDto>), 200)]
    public async Task<ActionResult<ApiResponse<ProjectStatsDto>>> GetProjectStats(int projectId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var stats = await _resourceService.GetProjectStatsAsync(projectId, userId);
        return Success(stats);
    }

    /// <summary>
    /// Validates all resources in a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Validation results</returns>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(ApiResponse<ValidationResultDto>), 200)]
    public async Task<ActionResult<ApiResponse<ValidationResultDto>>> ValidateProject(int projectId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _resourceService.ValidateProjectAsync(projectId, userId);
        return Success(result);
    }
}
