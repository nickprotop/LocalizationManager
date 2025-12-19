using System.Security.Claims;
using LrmCloud.Api.Authorization;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Glossary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Controller for glossary management.
/// Supports both project-level and organization-level glossaries.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class GlossaryController : ApiControllerBase
{
    private readonly GlossaryService _glossaryService;
    private readonly ILrmAuthorizationService _authService;
    private readonly ILogger<GlossaryController> _logger;

    public GlossaryController(
        GlossaryService glossaryService,
        ILrmAuthorizationService authService,
        ILogger<GlossaryController> logger)
    {
        _glossaryService = glossaryService;
        _authService = authService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    #region Project Glossary

    /// <summary>
    /// Get all glossary terms for a project (includes inherited organization terms).
    /// </summary>
    [HttpGet("projects/{projectId:int}/glossary")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryListResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryListResponse>>> GetProjectGlossary(
        int projectId,
        [FromQuery] bool includeInherited = true)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this project");

        try
        {
            var result = await _glossaryService.GetProjectGlossaryAsync(projectId, includeInherited);
            return Success(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Create a new project-level glossary term.
    /// </summary>
    [HttpPost("projects/{projectId:int}/glossary")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryTermDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryTermDto>>> CreateProjectTerm(
        int projectId,
        [FromBody] CreateGlossaryTermRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this project");

        try
        {
            var result = await _glossaryService.CreateProjectTermAsync(projectId, userId, request);
            return Created(nameof(GetTerm), new { termId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("GLOSSARY_TERM_INVALID", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Update a project-level glossary term.
    /// </summary>
    [HttpPut("projects/{projectId:int}/glossary/{termId:int}")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryTermDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryTermDto>>> UpdateProjectTerm(
        int projectId,
        int termId,
        [FromBody] UpdateGlossaryTermRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this project");

        // Verify term belongs to this project
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.ProjectId != projectId)
            return NotFound("GLOSSARY_TERM_NOT_FOUND", "Term not found in this project");

        try
        {
            var result = await _glossaryService.UpdateTermAsync(termId, request);
            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("GLOSSARY_TERM_INVALID", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_TERM_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Delete a project-level glossary term.
    /// </summary>
    [HttpDelete("projects/{projectId:int}/glossary/{termId:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DeleteProjectTerm(int projectId, int termId)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this project");

        // Verify term belongs to this project
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.ProjectId != projectId)
            return NotFound("GLOSSARY_TERM_NOT_FOUND", "Term not found in this project");

        try
        {
            await _glossaryService.DeleteTermAsync(termId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_TERM_NOT_FOUND", ex.Message);
        }
    }

    #endregion

    #region Organization Glossary

    /// <summary>
    /// Get all glossary terms for an organization.
    /// </summary>
    [HttpGet("organizations/{organizationId:int}/glossary")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryListResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryListResponse>>> GetOrganizationGlossary(int organizationId)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationMemberAsync(userId, organizationId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this organization");

        try
        {
            var result = await _glossaryService.GetOrganizationGlossaryAsync(organizationId);
            return Success(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Create a new organization-level glossary term.
    /// </summary>
    [HttpPost("organizations/{organizationId:int}/glossary")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryTermDto>), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryTermDto>>> CreateOrganizationTerm(
        int organizationId,
        [FromBody] CreateGlossaryTermRequest request)
    {
        var userId = GetUserId();

        // Only admins/owners can create org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "Only organization admins can create organization-level terms");

        try
        {
            var result = await _glossaryService.CreateOrganizationTermAsync(organizationId, userId, request);
            return Created(nameof(GetTerm), new { termId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("GLOSSARY_TERM_INVALID", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Update an organization-level glossary term.
    /// </summary>
    [HttpPut("organizations/{organizationId:int}/glossary/{termId:int}")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryTermDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryTermDto>>> UpdateOrganizationTerm(
        int organizationId,
        int termId,
        [FromBody] UpdateGlossaryTermRequest request)
    {
        var userId = GetUserId();

        // Only admins/owners can update org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "Only organization admins can update organization-level terms");

        // Verify term belongs to this organization
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.OrganizationId != organizationId)
            return NotFound("GLOSSARY_TERM_NOT_FOUND", "Term not found in this organization");

        try
        {
            var result = await _glossaryService.UpdateTermAsync(termId, request);
            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest("GLOSSARY_TERM_INVALID", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_TERM_NOT_FOUND", ex.Message);
        }
    }

    /// <summary>
    /// Delete an organization-level glossary term.
    /// </summary>
    [HttpDelete("organizations/{organizationId:int}/glossary/{termId:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DeleteOrganizationTerm(int organizationId, int termId)
    {
        var userId = GetUserId();

        // Only admins/owners can delete org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "Only organization admins can delete organization-level terms");

        // Verify term belongs to this organization
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.OrganizationId != organizationId)
            return NotFound("GLOSSARY_TERM_NOT_FOUND", "Term not found in this organization");

        try
        {
            await _glossaryService.DeleteTermAsync(termId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound("GLOSSARY_TERM_NOT_FOUND", ex.Message);
        }
    }

    #endregion

    #region Common

    /// <summary>
    /// Get a single glossary term by ID.
    /// </summary>
    [HttpGet("glossary/{termId:int}")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryTermDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<GlossaryTermDto>>> GetTerm(int termId)
    {
        var userId = GetUserId();
        var term = await _glossaryService.GetTermAsync(termId);

        if (term == null)
            return NotFound("GLOSSARY_TERM_NOT_FOUND", "Term not found");

        // Check access
        if (term.ProjectId.HasValue)
        {
            if (!await _authService.HasProjectAccessAsync(userId, term.ProjectId.Value))
                return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this term");
        }
        else if (term.OrganizationId.HasValue)
        {
            if (!await _authService.IsOrganizationMemberAsync(userId, term.OrganizationId.Value))
                return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this term");
        }

        return Success(term);
    }

    /// <summary>
    /// Find glossary terms that match the given source text.
    /// Used by UI to show what terms will be applied during translation.
    /// </summary>
    [HttpPost("projects/{projectId:int}/glossary/match")]
    [ProducesResponseType(typeof(ApiResponse<GlossaryUsageSummary>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<GlossaryUsageSummary>>> FindMatchingTerms(
        int projectId,
        [FromQuery] string sourceLanguage,
        [FromQuery] string targetLanguage,
        [FromBody] string sourceText)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden("GLOSSARY_ACCESS_DENIED", "You do not have access to this project");

        var result = await _glossaryService.FindMatchingTermsAsync(
            projectId, sourceLanguage, targetLanguage, sourceText);

        return Success(result);
    }

    #endregion
}
