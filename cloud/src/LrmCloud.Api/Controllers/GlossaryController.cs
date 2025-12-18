using System.Security.Claims;
using LrmCloud.Api.Authorization;
using LrmCloud.Api.Services;
using LrmCloud.Shared.DTOs.Glossary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Controller for glossary management.
/// Supports both project-level and organization-level glossaries.
/// </summary>
[ApiController]
[Authorize]
public class GlossaryController : ControllerBase
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
    [HttpGet("api/projects/{projectId:int}/glossary")]
    public async Task<ActionResult<GlossaryListResponse>> GetProjectGlossary(
        int projectId,
        [FromQuery] bool includeInherited = true)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        try
        {
            var result = await _glossaryService.GetProjectGlossaryAsync(projectId, includeInherited);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new project-level glossary term.
    /// </summary>
    [HttpPost("api/projects/{projectId:int}/glossary")]
    public async Task<ActionResult<GlossaryTermDto>> CreateProjectTerm(
        int projectId,
        [FromBody] CreateGlossaryTermRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        try
        {
            var result = await _glossaryService.CreateProjectTermAsync(projectId, userId, request);
            return CreatedAtAction(nameof(GetTerm), new { termId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a project-level glossary term.
    /// </summary>
    [HttpPut("api/projects/{projectId:int}/glossary/{termId:int}")]
    public async Task<ActionResult<GlossaryTermDto>> UpdateProjectTerm(
        int projectId,
        int termId,
        [FromBody] UpdateGlossaryTermRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        // Verify term belongs to this project
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.ProjectId != projectId)
            return NotFound(new { error = "Term not found in this project" });

        try
        {
            var result = await _glossaryService.UpdateTermAsync(termId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a project-level glossary term.
    /// </summary>
    [HttpDelete("api/projects/{projectId:int}/glossary/{termId:int}")]
    public async Task<IActionResult> DeleteProjectTerm(int projectId, int termId)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        // Verify term belongs to this project
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.ProjectId != projectId)
            return NotFound(new { error = "Term not found in this project" });

        try
        {
            await _glossaryService.DeleteTermAsync(termId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    #endregion

    #region Organization Glossary

    /// <summary>
    /// Get all glossary terms for an organization.
    /// </summary>
    [HttpGet("api/organizations/{organizationId:int}/glossary")]
    public async Task<ActionResult<GlossaryListResponse>> GetOrganizationGlossary(int organizationId)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationMemberAsync(userId, organizationId))
            return Forbid();

        try
        {
            var result = await _glossaryService.GetOrganizationGlossaryAsync(organizationId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new organization-level glossary term.
    /// </summary>
    [HttpPost("api/organizations/{organizationId:int}/glossary")]
    public async Task<ActionResult<GlossaryTermDto>> CreateOrganizationTerm(
        int organizationId,
        [FromBody] CreateGlossaryTermRequest request)
    {
        var userId = GetUserId();

        // Only admins/owners can create org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        try
        {
            var result = await _glossaryService.CreateOrganizationTermAsync(organizationId, userId, request);
            return CreatedAtAction(nameof(GetTerm), new { termId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an organization-level glossary term.
    /// </summary>
    [HttpPut("api/organizations/{organizationId:int}/glossary/{termId:int}")]
    public async Task<ActionResult<GlossaryTermDto>> UpdateOrganizationTerm(
        int organizationId,
        int termId,
        [FromBody] UpdateGlossaryTermRequest request)
    {
        var userId = GetUserId();

        // Only admins/owners can update org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        // Verify term belongs to this organization
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.OrganizationId != organizationId)
            return NotFound(new { error = "Term not found in this organization" });

        try
        {
            var result = await _glossaryService.UpdateTermAsync(termId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an organization-level glossary term.
    /// </summary>
    [HttpDelete("api/organizations/{organizationId:int}/glossary/{termId:int}")]
    public async Task<IActionResult> DeleteOrganizationTerm(int organizationId, int termId)
    {
        var userId = GetUserId();

        // Only admins/owners can delete org-level terms
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        // Verify term belongs to this organization
        var existing = await _glossaryService.GetTermAsync(termId);
        if (existing == null || existing.OrganizationId != organizationId)
            return NotFound(new { error = "Term not found in this organization" });

        try
        {
            await _glossaryService.DeleteTermAsync(termId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    #endregion

    #region Common

    /// <summary>
    /// Get a single glossary term by ID.
    /// </summary>
    [HttpGet("api/glossary/{termId:int}")]
    public async Task<ActionResult<GlossaryTermDto>> GetTerm(int termId)
    {
        var userId = GetUserId();
        var term = await _glossaryService.GetTermAsync(termId);

        if (term == null)
            return NotFound(new { error = "Term not found" });

        // Check access
        if (term.ProjectId.HasValue)
        {
            if (!await _authService.HasProjectAccessAsync(userId, term.ProjectId.Value))
                return Forbid();
        }
        else if (term.OrganizationId.HasValue)
        {
            if (!await _authService.IsOrganizationMemberAsync(userId, term.OrganizationId.Value))
                return Forbid();
        }

        return Ok(term);
    }

    /// <summary>
    /// Find glossary terms that match the given source text.
    /// Used by UI to show what terms will be applied during translation.
    /// </summary>
    [HttpPost("api/projects/{projectId:int}/glossary/match")]
    public async Task<ActionResult<GlossaryUsageSummary>> FindMatchingTerms(
        int projectId,
        [FromQuery] string sourceLanguage,
        [FromQuery] string targetLanguage,
        [FromBody] string sourceText)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        var result = await _glossaryService.FindMatchingTermsAsync(
            projectId, sourceLanguage, targetLanguage, sourceText);

        return Ok(result);
    }

    #endregion
}
