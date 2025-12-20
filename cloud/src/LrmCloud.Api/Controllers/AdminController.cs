using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Admin endpoints for superadmin users.
/// Provides system statistics, health monitoring, user management, and logs.
/// </summary>
[Route("api/admin")]
[ApiController]
[Authorize(Policy = "SuperAdmin")]
public class AdminController : ApiControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// Get database and system statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<AdminStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminStatsDto>>> GetStats()
    {
        var stats = await _adminService.GetStatsAsync();
        return Success(stats);
    }

    /// <summary>
    /// Get system health status for all services.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<SystemHealthDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SystemHealthDto>>> GetHealth()
    {
        var health = await _adminService.GetHealthAsync();
        return Success(health);
    }

    /// <summary>
    /// Get application logs with filtering.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(ApiResponse<List<LogEntryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<LogEntryDto>>>> GetLogs(
        [FromQuery] string? level,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 500) pageSize = 500;

        var filter = new LogFilterDto
        {
            Level = level,
            Search = search,
            From = from,
            To = to
        };

        var logs = await _adminService.GetLogsAsync(filter, page, pageSize);
        return Success(logs);
    }

    /// <summary>
    /// Get paginated list of all users.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AdminUserDto>>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? plan,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (users, totalCount) = await _adminService.GetUsersAsync(search, plan, page, pageSize);
        return Paginated(users, page, pageSize, totalCount);
    }

    /// <summary>
    /// Get detailed user information.
    /// </summary>
    [HttpGet("users/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminUserDetailDto>>> GetUser(int id)
    {
        var user = await _adminService.GetUserAsync(id);
        if (user == null)
            return NotFound(ErrorCodes.RES_NOT_FOUND, "User not found");

        return Success(user);
    }

    /// <summary>
    /// Update user's admin-controlled properties.
    /// </summary>
    [HttpPut("users/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminUserDetailDto>>> UpdateUser(int id, [FromBody] AdminUpdateUserDto dto)
    {
        var (success, errorMessage) = await _adminService.UpdateUserAsync(id, dto);
        if (!success)
        {
            if (errorMessage == "User not found")
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        // Return updated user
        var user = await _adminService.GetUserAsync(id);
        return Success(user!);
    }

    /// <summary>
    /// Soft delete a user.
    /// </summary>
    [HttpDelete("users/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var (success, errorMessage) = await _adminService.DeleteUserAsync(id);
        if (!success)
        {
            if (errorMessage == "User not found")
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return NoContent();
    }

    /// <summary>
    /// Reset user's usage counters.
    /// </summary>
    [HttpPost("users/{id:int}/reset-usage")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminUserDetailDto>>> ResetUserUsage(int id)
    {
        var (success, errorMessage) = await _adminService.ResetUserUsageAsync(id);
        if (!success)
            return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage!);

        // Return updated user
        var user = await _adminService.GetUserAsync(id);
        return Success(user!);
    }

    /// <summary>
    /// Get paginated list of all organizations.
    /// </summary>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminOrganizationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AdminOrganizationDto>>>> GetOrganizations(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (orgs, totalCount) = await _adminService.GetOrganizationsAsync(search, page, pageSize);
        return Paginated(orgs, page, pageSize, totalCount);
    }

    /// <summary>
    /// Get recent webhook events.
    /// </summary>
    [HttpGet("webhook-events")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminWebhookEventDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AdminWebhookEventDto>>>> GetWebhookEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (events, totalCount) = await _adminService.GetWebhookEventsAsync(page, pageSize);
        return Paginated(events, page, pageSize, totalCount);
    }

    // ===== Analytics Endpoints =====

    /// <summary>
    /// Get revenue analytics including MRR and history.
    /// </summary>
    [HttpGet("analytics/revenue")]
    [ProducesResponseType(typeof(ApiResponse<RevenueAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RevenueAnalyticsDto>>> GetRevenueAnalytics(
        [FromQuery] int months = 12)
    {
        if (months < 1) months = 1;
        if (months > 24) months = 24;

        var analytics = await _adminService.GetRevenueAnalyticsAsync(months);
        return Success(analytics);
    }

    /// <summary>
    /// Get user analytics including growth, churn, and conversions.
    /// </summary>
    [HttpGet("analytics/users")]
    [ProducesResponseType(typeof(ApiResponse<UserAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserAnalyticsDto>>> GetUserAnalytics(
        [FromQuery] int months = 12)
    {
        if (months < 1) months = 1;
        if (months > 24) months = 24;

        var analytics = await _adminService.GetUserAnalyticsAsync(months);
        return Success(analytics);
    }

    /// <summary>
    /// Get usage analytics including translation character trends.
    /// </summary>
    [HttpGet("analytics/usage")]
    [ProducesResponseType(typeof(ApiResponse<UsageAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UsageAnalyticsDto>>> GetUsageAnalytics(
        [FromQuery] int days = 30)
    {
        if (days < 1) days = 1;
        if (days > 90) days = 90;

        var analytics = await _adminService.GetUsageAnalyticsAsync(days);
        return Success(analytics);
    }

    // ===== Bulk Action Endpoints =====

    /// <summary>
    /// Change plan for multiple users.
    /// </summary>
    [HttpPost("users/bulk/change-plan")]
    [ProducesResponseType(typeof(ApiResponse<BulkActionResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BulkActionResult>>> BulkChangePlan([FromBody] BulkChangePlanRequest request)
    {
        if (request.UserIds.Count == 0)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "No user IDs provided");

        if (request.UserIds.Count > 100)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Maximum 100 users per bulk operation");

        var result = await _adminService.BulkChangePlanAsync(request);
        return Success(result);
    }

    /// <summary>
    /// Verify emails for multiple users.
    /// </summary>
    [HttpPost("users/bulk/verify-emails")]
    [ProducesResponseType(typeof(ApiResponse<BulkActionResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BulkActionResult>>> BulkVerifyEmails([FromBody] BulkActionRequest request)
    {
        if (request.UserIds.Count == 0)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "No user IDs provided");

        if (request.UserIds.Count > 100)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Maximum 100 users per bulk operation");

        var result = await _adminService.BulkVerifyEmailsAsync(request);
        return Success(result);
    }

    /// <summary>
    /// Reset usage counters for multiple users.
    /// </summary>
    [HttpPost("users/bulk/reset-usage")]
    [ProducesResponseType(typeof(ApiResponse<BulkActionResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BulkActionResult>>> BulkResetUsage([FromBody] BulkActionRequest request)
    {
        if (request.UserIds.Count == 0)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "No user IDs provided");

        if (request.UserIds.Count > 100)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Maximum 100 users per bulk operation");

        var result = await _adminService.BulkResetUsageAsync(request);
        return Success(result);
    }

    // ===== Organization Detail Endpoints =====

    /// <summary>
    /// Get detailed organization information.
    /// </summary>
    [HttpGet("organizations/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrganizationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminOrganizationDetailDto>>> GetOrganization(int id)
    {
        var org = await _adminService.GetOrganizationAsync(id);
        if (org == null)
            return NotFound(ErrorCodes.RES_NOT_FOUND, "Organization not found");

        return Success(org);
    }

    /// <summary>
    /// Update organization's admin-controlled properties.
    /// </summary>
    [HttpPut("organizations/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrganizationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminOrganizationDetailDto>>> UpdateOrganization(int id, [FromBody] AdminUpdateOrganizationDto dto)
    {
        var (success, errorMessage) = await _adminService.UpdateOrganizationAsync(id, dto);
        if (!success)
        {
            if (errorMessage == "Organization not found")
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        // Return updated organization
        var org = await _adminService.GetOrganizationAsync(id);
        return Success(org!);
    }

    /// <summary>
    /// Transfer organization ownership.
    /// </summary>
    [HttpPost("organizations/{id:int}/transfer-ownership")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrganizationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminOrganizationDetailDto>>> TransferOrganizationOwnership(int id, [FromBody] AdminTransferOwnershipRequest request)
    {
        var (success, errorMessage) = await _adminService.TransferOrganizationOwnershipAsync(id, request);
        if (!success)
        {
            if (errorMessage == "Organization not found" || errorMessage == "New owner not found")
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        // Return updated organization
        var org = await _adminService.GetOrganizationAsync(id);
        return Success(org!);
    }
}
