// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Sync;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for sync history operations in the Web UI.
/// </summary>
public class SyncHistoryService
{
    private readonly HttpClient _httpClient;

    public SyncHistoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets paginated sync history for a project.
    /// </summary>
    public async Task<SyncHistoryListResponse?> GetHistoryAsync(int projectId, int page = 1, int pageSize = 20)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/sync/history?page={page}&pageSize={pageSize}");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SyncHistoryListResponse>>();
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific history entry.
    /// </summary>
    public async Task<SyncHistoryDetailDto?> GetHistoryDetailAsync(int projectId, string historyId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/sync/history/{historyId}");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SyncHistoryDetailDto>>();
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reverts a push operation.
    /// </summary>
    public async Task<ServiceResult<RevertResponse>> RevertAsync(int projectId, string historyId, string? message = null)
    {
        try
        {
            var request = new RevertRequest { Message = message };
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/sync/history/{historyId}/revert", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<RevertResponse>>();
                if (result?.Data != null)
                {
                    return ServiceResult<RevertResponse>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<RevertResponse>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<RevertResponse>.Failure($"Failed to revert: {ex.Message}");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("\"detail\""))
            {
                var problem = System.Text.Json.JsonDocument.Parse(content);
                if (problem.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "An error occurred";
                }
            }
            return content.Length < 200 ? content : "An error occurred";
        }
        catch
        {
            return $"Request failed with status {response.StatusCode}";
        }
    }
}
