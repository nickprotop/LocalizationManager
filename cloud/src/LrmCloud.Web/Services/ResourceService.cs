using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Resources;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for resource keys and translations
/// </summary>
public class ResourceService
{
    private readonly HttpClient _httpClient;

    public ResourceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all resource keys for a project
    /// </summary>
    public async Task<List<ResourceKeyDto>> GetResourceKeysAsync(int projectId)
    {
        var response = await _httpClient.GetAsync($"projects/{projectId}/keys");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ResourceKeyDto>>>();
        return result?.Data ?? new List<ResourceKeyDto>();
    }

    /// <summary>
    /// Gets a specific resource key with all translations
    /// </summary>
    public async Task<ResourceKeyDetailDto?> GetResourceKeyAsync(int projectId, string keyName)
    {
        var encodedKeyName = Uri.EscapeDataString(keyName);
        var response = await _httpClient.GetAsync($"projects/{projectId}/keys/{encodedKeyName}");

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResourceKeyDetailDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Gets resource keys with translations, paginated with search and sort support
    /// </summary>
    public async Task<PagedResult<ResourceKeyDetailDto>> GetResourceKeysPagedAsync(
        int projectId,
        int page = 1,
        int pageSize = 50,
        string? search = null,
        string? sortBy = null,
        bool sortDesc = false)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        if (!string.IsNullOrWhiteSpace(sortBy))
            queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");

        if (sortDesc)
            queryParams.Add("sortDesc=true");

        var queryString = string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"projects/{projectId}/resources?{queryString}");

        if (!response.IsSuccessStatusCode)
            return new PagedResult<ResourceKeyDetailDto> { Page = page, PageSize = pageSize };

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ResourceKeyDetailDto>>>();
        return result?.Data ?? new PagedResult<ResourceKeyDetailDto> { Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// Creates a new resource key
    /// </summary>
    public async Task<ServiceResult<ResourceKeyDto>> CreateResourceKeyAsync(int projectId, CreateResourceKeyRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/keys", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResourceKeyDto>>();
                if (result?.Data != null)
                    return ServiceResult<ResourceKeyDto>.Success(result.Data);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ResourceKeyDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ResourceKeyDto>.Failure($"Failed to create key: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a resource key
    /// </summary>
    public async Task<ServiceResult<ResourceKeyDto>> UpdateResourceKeyAsync(int projectId, string keyName, UpdateResourceKeyRequest request)
    {
        try
        {
            var encodedKeyName = Uri.EscapeDataString(keyName);
            var response = await _httpClient.PutAsJsonAsync($"projects/{projectId}/keys/{encodedKeyName}", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResourceKeyDto>>();
                if (result?.Data != null)
                    return ServiceResult<ResourceKeyDto>.Success(result.Data);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ResourceKeyDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ResourceKeyDto>.Failure($"Failed to update key: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a resource key
    /// </summary>
    public async Task<ServiceResult> DeleteResourceKeyAsync(int projectId, string keyName)
    {
        try
        {
            var encodedKeyName = Uri.EscapeDataString(keyName);
            var response = await _httpClient.DeleteAsync($"projects/{projectId}/keys/{encodedKeyName}");

            if (response.IsSuccessStatusCode)
                return ServiceResult.Success("Key deleted successfully");

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure($"Failed to delete key: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a translation
    /// </summary>
    public async Task<ServiceResult<TranslationDto>> UpdateTranslationAsync(
        int projectId, string keyName, string languageCode, UpdateTranslationRequest request)
    {
        try
        {
            var encodedKeyName = Uri.EscapeDataString(keyName);
            var response = await _httpClient.PutAsJsonAsync(
                $"projects/{projectId}/keys/{encodedKeyName}/translations/{languageCode}", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranslationDto>>();
                if (result?.Data != null)
                    return ServiceResult<TranslationDto>.Success(result.Data);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<TranslationDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<TranslationDto>.Failure($"Failed to update translation: {ex.Message}");
        }
    }

    /// <summary>
    /// Bulk updates translations for a language
    /// </summary>
    public async Task<ServiceResult<int>> BulkUpdateTranslationsAsync(
        int projectId, string languageCode, List<BulkTranslationUpdate> updates)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"projects/{projectId}/translations/{languageCode}/bulk", updates);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
                return ServiceResult<int>.Success(result?.Data ?? 0);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<int>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<int>.Failure($"Failed to bulk update translations: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets project stats including language list
    /// </summary>
    public async Task<ProjectStatsDto?> GetProjectStatsAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/stats");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectStatsDto>>();
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets validation results for a project
    /// </summary>
    public async Task<ValidationResultDto?> ValidateProjectAsync(int projectId, bool forceRefresh = false)
    {
        try
        {
            var url = $"projects/{projectId}/validate";
            if (forceRefresh)
                url += "?refresh=true";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ValidationResultDto>>();
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all languages in a project
    /// </summary>
    public async Task<List<ProjectLanguageDto>> GetProjectLanguagesAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/languages");
            if (!response.IsSuccessStatusCode)
                return new List<ProjectLanguageDto>();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProjectLanguageDto>>>();
            return result?.Data ?? new List<ProjectLanguageDto>();
        }
        catch
        {
            return new List<ProjectLanguageDto>();
        }
    }

    /// <summary>
    /// Adds a new language to a project
    /// </summary>
    public async Task<ServiceResult<ProjectLanguageDto>> AddLanguageAsync(int projectId, AddLanguageRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/languages", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectLanguageDto>>();
                if (result?.Data != null)
                    return ServiceResult<ProjectLanguageDto>.Success(result.Data);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ProjectLanguageDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectLanguageDto>.Failure($"Failed to add language: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes a language from a project
    /// </summary>
    public async Task<ServiceResult> RemoveLanguageAsync(int projectId, string languageCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"projects/{projectId}/languages/{languageCode}?confirm=true");

            if (response.IsSuccessStatusCode)
                return ServiceResult.Success("Language removed successfully");

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure($"Failed to remove language: {ex.Message}");
        }
    }

    /// <summary>
    /// Batch saves multiple changes with sync history recording
    /// </summary>
    public async Task<ServiceResult<BatchSaveResponse>> BatchSaveAsync(int projectId, BatchSaveRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/batch-save", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchSaveResponse>>();
                if (result?.Data != null)
                    return ServiceResult<BatchSaveResponse>.Success(result.Data);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<BatchSaveResponse>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<BatchSaveResponse>.Failure($"Failed to save: {ex.Message}");
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
