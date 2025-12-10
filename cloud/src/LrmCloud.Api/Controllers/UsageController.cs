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
}
