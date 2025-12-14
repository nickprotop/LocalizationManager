using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Snapshots;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for project snapshot operations
/// </summary>
public class SnapshotService
{
    private readonly HttpClient _httpClient;

    public SnapshotService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SnapshotDto>> GetSnapshotsAsync(int projectId, int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"projects/{projectId}/snapshots?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SnapshotDto>>>();
        return result?.Data ?? new List<SnapshotDto>();
    }

    public async Task<SnapshotDetailDto?> GetSnapshotAsync(int projectId, string snapshotId)
    {
        var response = await _httpClient.GetAsync($"projects/{projectId}/snapshots/{snapshotId}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SnapshotDetailDto>>();
        return result?.Data;
    }

    public async Task<ServiceResult<SnapshotDto>> CreateSnapshotAsync(int projectId, string? description = null)
    {
        try
        {
            var request = new CreateSnapshotRequest { Description = description };
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/snapshots", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SnapshotDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<SnapshotDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<SnapshotDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<SnapshotDto>.Failure($"Failed to create snapshot: {ex.Message}");
        }
    }

    public async Task<ServiceResult<SnapshotDto>> RestoreSnapshotAsync(int projectId, string snapshotId, bool createBackup = true, string? message = null)
    {
        try
        {
            var request = new RestoreSnapshotRequest
            {
                CreateBackup = createBackup,
                Message = message
            };
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/snapshots/{snapshotId}/restore", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SnapshotDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<SnapshotDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<SnapshotDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<SnapshotDto>.Failure($"Failed to restore snapshot: {ex.Message}");
        }
    }

    public async Task<SnapshotDiffDto?> DiffSnapshotsAsync(int projectId, string fromSnapshotId, string toSnapshotId)
    {
        var response = await _httpClient.GetAsync($"projects/{projectId}/snapshots/{fromSnapshotId}/diff/{toSnapshotId}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SnapshotDiffDto>>();
        return result?.Data;
    }

    public async Task<ServiceResult> DeleteSnapshotAsync(int projectId, string snapshotId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"projects/{projectId}/snapshots/{snapshotId}");

            if (response.IsSuccessStatusCode)
            {
                return ServiceResult.Success("Snapshot deleted successfully");
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure($"Failed to delete snapshot: {ex.Message}");
        }
    }

    public async Task<UnsnapshotedChangesDto?> CheckUnsnapshotedChangesAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/snapshots/unsnapshoted-changes");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UnsnapshotedChangesDto>>();
            return result?.Data;
        }
        catch
        {
            return null;
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
