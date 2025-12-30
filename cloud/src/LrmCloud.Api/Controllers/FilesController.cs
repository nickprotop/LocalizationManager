using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for file-level import/export operations.
/// Allows importing localization files into projects and exporting to downloadable ZIP.
/// </summary>
[Route("api/projects/{projectId}/files")]
[Authorize]
public class FilesController : ApiControllerBase
{
    private readonly IFileOperationsService _fileOperationsService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileOperationsService fileOperationsService,
        ILogger<FilesController> logger)
    {
        _fileOperationsService = fileOperationsService;
        _logger = logger;
    }

    /// <summary>
    /// Import localization files into a project.
    /// Parses files and saves keys/translations to the database.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Import request with files and optional format</param>
    /// <returns>Import result with counts and any errors</returns>
    /// <response code="200">Import completed (check Success flag and Errors list)</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to import to this project</response>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<FileImportResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<FileImportResponse>>> Import(
        int projectId,
        [FromBody] FileImportRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var result = await _fileOperationsService.ImportFilesAsync(projectId, userId, request, ct);

            if (result.Success)
            {
                _logger.LogInformation(
                    "User {UserId} imported {Applied} entries to project {ProjectId}",
                    userId, result.Applied, projectId);
            }
            else
            {
                _logger.LogWarning(
                    "User {UserId} import to project {ProjectId} had errors: {Errors}",
                    userId, projectId, string.Join(", ", result.Errors));
            }

            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("FILES_FORBIDDEN", ex.Message);
        }
    }

    /// <summary>
    /// Export project translations as a ZIP file.
    /// Generates localization files in the specified format.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="format">Output format (resx, json, i18next, android, ios, po, xliff)</param>
    /// <param name="languages">Comma-separated language codes (optional, defaults to all)</param>
    /// <returns>ZIP file containing localization files</returns>
    /// <response code="200">ZIP file download</response>
    /// <response code="400">Invalid format or parameters</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">User doesn't have permission to export this project</response>
    /// <response code="404">Project not found</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Export(
        int projectId,
        [FromQuery] string format,
        [FromQuery] string? languages,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(format))
        {
            return BadRequest("FILES_INVALID_FORMAT", "Format is required");
        }

        // Validate format
        var validFormats = new[] { "resx", "json", "i18next", "android", "ios", "po", "xliff" };
        if (!validFormats.Contains(format.ToLowerInvariant()))
        {
            return BadRequest("FILES_INVALID_FORMAT", $"Invalid format. Valid formats: {string.Join(", ", validFormats)}");
        }

        try
        {
            var languageArray = string.IsNullOrWhiteSpace(languages)
                ? null
                : languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var zipBytes = await _fileOperationsService.ExportFilesAsync(
                projectId, userId, format.ToLowerInvariant(), languageArray, ct);

            _logger.LogInformation(
                "User {UserId} exported project {ProjectId} with format {Format}",
                userId, projectId, format);

            var fileName = $"localization-{projectId}-{format}.zip";
            return File(zipBytes, "application/zip", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("FILES_FORBIDDEN", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound("FILES_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Get a preview of what export would produce.
    /// Shows files that would be generated without actually creating them.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="format">Output format</param>
    /// <param name="languages">Comma-separated language codes (optional)</param>
    /// <returns>Preview with file list and key counts</returns>
    [HttpGet("export/preview")]
    [ProducesResponseType(typeof(ApiResponse<FileExportPreviewResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<FileExportPreviewResponse>>> ExportPreview(
        int projectId,
        [FromQuery] string format,
        [FromQuery] string? languages,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(format))
        {
            return BadRequest("FILES_INVALID_FORMAT", "Format is required");
        }

        try
        {
            var languageArray = string.IsNullOrWhiteSpace(languages)
                ? null
                : languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var preview = await _fileOperationsService.GetExportPreviewAsync(
                projectId, userId, format.ToLowerInvariant(), languageArray, ct);

            return Success(preview);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden("FILES_FORBIDDEN", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound("FILES_NOT_FOUND", ex.Message);
        }
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}
