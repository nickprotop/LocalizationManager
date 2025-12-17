using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for user usage statistics.
/// </summary>
[Route("api/usage")]
[Authorize]
public class UsageController : ApiControllerBase
{
    private readonly IUsageService _usageService;
    private readonly ILogger<UsageController> _logger;

    public UsageController(IUsageService usageService, ILogger<UsageController> logger)
    {
        _usageService = usageService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get usage statistics for the current user.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<UsageStatsDto>>> GetStats()
    {
        var userId = GetUserId();
        var stats = await _usageService.GetUserStatsAsync(userId);
        return Success(stats);
    }

    /// <summary>
    /// Get usage statistics for an organization.
    /// </summary>
    [HttpGet("organizations/{organizationId:int}")]
    public async Task<ActionResult<ApiResponse<OrganizationUsageDto>>> GetOrganizationStats(int organizationId)
    {
        var userId = GetUserId();
        var stats = await _usageService.GetOrganizationStatsAsync(organizationId, userId);

        if (stats == null)
        {
            return NotFound("Organization not found or you don't have access to it.");
        }

        return Success(stats);
    }

    /// <summary>
    /// Get user's usage breakdown by personal vs organization contributions.
    /// </summary>
    [HttpGet("breakdown")]
    public async Task<ActionResult<ApiResponse<UserUsageBreakdownDto>>> GetUserBreakdown()
    {
        var userId = GetUserId();
        var breakdown = await _usageService.GetUserUsageBreakdownAsync(userId);
        return Success(breakdown);
    }

    /// <summary>
    /// Get organization usage breakdown by member (admins/owners only).
    /// </summary>
    [HttpGet("organizations/{organizationId:int}/members")]
    public async Task<ActionResult<ApiResponse<List<OrgMemberUsageDto>>>> GetOrgMemberUsage(int organizationId)
    {
        var userId = GetUserId();
        var usage = await _usageService.GetOrgMemberUsageAsync(organizationId, userId);
        return Success(usage);
    }

    /// <summary>
    /// Get project usage breakdown by contributor.
    /// </summary>
    [HttpGet("projects/{projectId:int}")]
    public async Task<ActionResult<ApiResponse<ProjectUsageDto>>> GetProjectUsage(int projectId)
    {
        var userId = GetUserId();
        var usage = await _usageService.GetProjectUsageAsync(projectId, userId);

        if (usage == null)
        {
            return NotFound("Project not found or you don't have access to it.");
        }

        return Success(usage);
    }
}
