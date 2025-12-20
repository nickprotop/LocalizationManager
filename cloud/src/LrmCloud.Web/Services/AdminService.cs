using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Admin;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for admin operations.
/// </summary>
public class AdminService
{
    private readonly HttpClient _httpClient;

    public AdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AdminStatsDto?> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("admin/stats");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminStatsDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<SystemHealthDto?> GetHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("admin/health");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SystemHealthDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<List<LogEntryDto>?> GetLogsAsync(
        string? level = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 100)
    {
        try
        {
            var query = $"admin/logs?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(level)) query += $"&level={Uri.EscapeDataString(level)}";
            if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
            if (from.HasValue) query += $"&from={from.Value:o}";
            if (to.HasValue) query += $"&to={to.Value:o}";

            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<LogEntryDto>>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<(List<AdminUserDto>? Users, int TotalCount)> GetUsersAsync(
        string? search = null,
        string? plan = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var query = $"admin/users?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrWhiteSpace(plan)) query += $"&plan={Uri.EscapeDataString(plan)}";

            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminUserDto>>>();
                var totalCount = result?.Meta?.TotalCount ?? 0;
                return (result?.Data, totalCount);
            }
        }
        catch
        {
            // Ignore errors
        }
        return (null, 0);
    }

    public async Task<AdminUserDetailDto?> GetUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"admin/users/{id}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<(bool Success, string? Error, AdminUserDetailDto? User)> UpdateUserAsync(int id, AdminUpdateUserDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"admin/users/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"admin/users/{id}");
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error, AdminUserDetailDto? User)> ResetUserUsageAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"admin/users/{id}/reset-usage", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(List<AdminOrganizationDto>? Orgs, int TotalCount)> GetOrganizationsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var query = $"admin/organizations?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";

            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrganizationDto>>>();
                var totalCount = result?.Meta?.TotalCount ?? 0;
                return (result?.Data, totalCount);
            }
        }
        catch
        {
            // Ignore errors
        }
        return (null, 0);
    }

    public async Task<(List<AdminWebhookEventDto>? Events, int TotalCount)> GetWebhookEventsAsync(
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            var query = $"admin/webhook-events?page={page}&pageSize={pageSize}";

            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminWebhookEventDto>>>();
                var totalCount = result?.Meta?.TotalCount ?? 0;
                return (result?.Data, totalCount);
            }
        }
        catch
        {
            // Ignore errors
        }
        return (null, 0);
    }

    // ===== Analytics Methods =====

    public async Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(int months = 12)
    {
        try
        {
            var response = await _httpClient.GetAsync($"admin/analytics/revenue?months={months}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<RevenueAnalyticsDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<UserAnalyticsDto?> GetUserAnalyticsAsync(int months = 12)
    {
        try
        {
            var response = await _httpClient.GetAsync($"admin/analytics/users?months={months}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserAnalyticsDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<UsageAnalyticsDto?> GetUsageAnalyticsAsync(int days = 30)
    {
        try
        {
            var response = await _httpClient.GetAsync($"admin/analytics/usage?days={days}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UsageAnalyticsDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    // ===== Bulk Action Methods =====

    public async Task<(bool Success, string? Error, BulkActionResult? Result)> BulkChangePlanAsync(List<int> userIds, string newPlan)
    {
        try
        {
            var request = new BulkChangePlanRequest { UserIds = userIds, NewPlan = newPlan };
            var response = await _httpClient.PostAsJsonAsync("admin/users/bulk/change-plan", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkActionResult>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, BulkActionResult? Result)> BulkVerifyEmailsAsync(List<int> userIds)
    {
        try
        {
            var request = new BulkActionRequest { UserIds = userIds };
            var response = await _httpClient.PostAsJsonAsync("admin/users/bulk/verify-emails", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkActionResult>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, BulkActionResult? Result)> BulkResetUsageAsync(List<int> userIds)
    {
        try
        {
            var request = new BulkActionRequest { UserIds = userIds };
            var response = await _httpClient.PostAsJsonAsync("admin/users/bulk/reset-usage", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkActionResult>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    // ===== Organization Detail Methods =====

    public async Task<AdminOrganizationDetailDto?> GetOrganizationAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"admin/organizations/{id}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrganizationDetailDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<(bool Success, string? Error, AdminOrganizationDetailDto? Org)> UpdateOrganizationAsync(int id, AdminUpdateOrganizationDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"admin/organizations/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrganizationDetailDto>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error, AdminOrganizationDetailDto? Org)> TransferOrganizationOwnershipAsync(int id, int newOwnerId)
    {
        try
        {
            var request = new AdminTransferOwnershipRequest { NewOwnerId = newOwnerId };
            var response = await _httpClient.PostAsJsonAsync($"admin/organizations/{id}/transfer-ownership", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrganizationDetailDto>>();
                return (true, null, result?.Data);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }
}
