using LrmCloud.Shared.DTOs.Admin;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for superadmin operations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Get database and system statistics.
    /// </summary>
    Task<AdminStatsDto> GetStatsAsync();

    /// <summary>
    /// Get system health status for all services.
    /// </summary>
    Task<SystemHealthDto> GetHealthAsync();

    /// <summary>
    /// Get application logs with filtering.
    /// </summary>
    Task<List<LogEntryDto>> GetLogsAsync(LogFilterDto? filter, int page = 1, int pageSize = 100);

    /// <summary>
    /// Get paginated list of all users.
    /// </summary>
    Task<(List<AdminUserDto> Users, int TotalCount)> GetUsersAsync(string? search, string? plan, int page = 1, int pageSize = 20);

    /// <summary>
    /// Get detailed user information.
    /// </summary>
    Task<AdminUserDetailDto?> GetUserAsync(int userId);

    /// <summary>
    /// Update user's admin-controlled properties.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(int userId, AdminUpdateUserDto dto);

    /// <summary>
    /// Soft delete a user.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId);

    /// <summary>
    /// Reset user's usage counters.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ResetUserUsageAsync(int userId);

    /// <summary>
    /// Get paginated list of all organizations.
    /// </summary>
    Task<(List<AdminOrganizationDto> Organizations, int TotalCount)> GetOrganizationsAsync(string? search, int page = 1, int pageSize = 20);

    /// <summary>
    /// Get recent webhook events.
    /// </summary>
    Task<(List<AdminWebhookEventDto> Events, int TotalCount)> GetWebhookEventsAsync(int page = 1, int pageSize = 50);

    // ===== Analytics Methods =====

    /// <summary>
    /// Get revenue analytics including MRR and history.
    /// </summary>
    Task<RevenueAnalyticsDto> GetRevenueAnalyticsAsync(int months = 12);

    /// <summary>
    /// Get user analytics including growth, churn, and conversions.
    /// </summary>
    Task<UserAnalyticsDto> GetUserAnalyticsAsync(int months = 12);

    /// <summary>
    /// Get usage analytics including translation character trends.
    /// </summary>
    Task<UsageAnalyticsDto> GetUsageAnalyticsAsync(int days = 30);

    // ===== Bulk Action Methods =====

    /// <summary>
    /// Change plan for multiple users.
    /// </summary>
    Task<BulkActionResult> BulkChangePlanAsync(BulkChangePlanRequest request);

    /// <summary>
    /// Verify emails for multiple users.
    /// </summary>
    Task<BulkActionResult> BulkVerifyEmailsAsync(BulkActionRequest request);

    /// <summary>
    /// Reset usage counters for multiple users.
    /// </summary>
    Task<BulkActionResult> BulkResetUsageAsync(BulkActionRequest request);

    // ===== Organization Detail Methods =====

    /// <summary>
    /// Get detailed organization information.
    /// </summary>
    Task<AdminOrganizationDetailDto?> GetOrganizationAsync(int orgId);

    /// <summary>
    /// Update organization's admin-controlled properties.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> UpdateOrganizationAsync(int orgId, AdminUpdateOrganizationDto dto);

    /// <summary>
    /// Transfer organization ownership.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TransferOrganizationOwnershipAsync(int orgId, AdminTransferOwnershipRequest request);
}
