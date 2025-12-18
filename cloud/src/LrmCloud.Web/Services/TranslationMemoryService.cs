using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.TranslationMemory;

namespace LrmCloud.Web.Services;

/// <summary>
/// Client service for Translation Memory API
/// </summary>
public class TranslationMemoryService
{
    private readonly HttpClient _httpClient;

    public TranslationMemoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Looks up TM matches for a source text
    /// </summary>
    public async Task<ServiceResult<TmLookupResponse>> LookupAsync(TmLookupRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("tm/lookup", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TmLookupResponse>>();
                if (result?.Data != null)
                {
                    return ServiceResult<TmLookupResponse>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<TmLookupResponse>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<TmLookupResponse>.Failure($"TM lookup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores a translation in TM
    /// </summary>
    public async Task<ServiceResult<TmMatchDto>> StoreAsync(TmStoreRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("tm/store", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TmMatchDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<TmMatchDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<TmMatchDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<TmMatchDto>.Failure($"TM store failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Batch stores multiple translations in TM
    /// </summary>
    public async Task<ServiceResult> StoreBatchAsync(List<TmStoreRequest> requests)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("tm/store-batch", requests);

            if (response.IsSuccessStatusCode)
            {
                return ServiceResult.Success();
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure($"TM batch store failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Increments use count when user accepts a TM suggestion
    /// </summary>
    public async Task IncrementUseCountAsync(int tmEntryId)
    {
        try
        {
            await _httpClient.PostAsync($"tm/{tmEntryId}/use", null);
        }
        catch
        {
            // Fire and forget - don't fail if this doesn't work
        }
    }

    /// <summary>
    /// Gets TM statistics
    /// </summary>
    public async Task<ServiceResult<TmStatsDto>> GetStatsAsync(int? organizationId = null)
    {
        try
        {
            var url = organizationId.HasValue
                ? $"tm/stats?organizationId={organizationId}"
                : "tm/stats";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TmStatsDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<TmStatsDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<TmStatsDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<TmStatsDto>.Failure($"Failed to get TM stats: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears TM entries
    /// </summary>
    public async Task<ServiceResult<int>> ClearAsync(string? sourceLanguage = null, string? targetLanguage = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(sourceLanguage))
                queryParams.Add($"sourceLanguage={sourceLanguage}");
            if (!string.IsNullOrEmpty(targetLanguage))
                queryParams.Add($"targetLanguage={targetLanguage}");

            var url = queryParams.Count > 0
                ? $"tm?{string.Join("&", queryParams)}"
                : "tm";

            var response = await _httpClient.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                // Parse count from message like "Cleared 5 TM entries"
                if (result?.Message != null && result.Message.StartsWith("Cleared "))
                {
                    var parts = result.Message.Split(' ');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var count))
                    {
                        return ServiceResult<int>.Success(count);
                    }
                }
                return ServiceResult<int>.Success(0);
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<int>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<int>.Failure($"Failed to clear TM: {ex.Message}");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(content)
                ? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                : content;
        }
        catch
        {
            return $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
    }
}
