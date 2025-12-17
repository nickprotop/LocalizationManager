using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Usage;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for retrieving user usage statistics.
/// </summary>
public class UsageService
{
    private readonly HttpClient _httpClient;

    public UsageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UsageStatsDto?> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("usage/stats");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UsageStatsDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<OrganizationUsageDto?> GetOrganizationStatsAsync(int organizationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"usage/organizations/{organizationId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationUsageDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<UserUsageBreakdownDto?> GetUserBreakdownAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("usage/breakdown");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserUsageBreakdownDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<List<OrgMemberUsageDto>?> GetOrgMemberUsageAsync(int organizationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"usage/organizations/{organizationId}/members");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrgMemberUsageDto>>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<ProjectUsageDto?> GetProjectUsageAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"usage/projects/{projectId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectUsageDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }
}
