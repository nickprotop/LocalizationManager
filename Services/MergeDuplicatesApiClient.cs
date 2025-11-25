using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for merge duplicates operations.
/// </summary>
public class MergeDuplicatesApiClient
{
    private readonly HttpClient _httpClient;

    public MergeDuplicatesApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Get list of all duplicate keys.
    /// </summary>
    public async Task<DuplicateKeysResponse?> ListDuplicatesAsync()
    {
        return await _httpClient.GetFromJsonAsync<DuplicateKeysResponse>("/api/mergeduplicates/list");
    }

    /// <summary>
    /// Merge duplicates for a specific key (auto-first strategy).
    /// </summary>
    public async Task<MergeDuplicatesResponse?> MergeKeyAsync(string key, bool autoFirst = true)
    {
        var request = new MergeDuplicatesRequest
        {
            Key = key,
            MergeAll = false,
            AutoFirst = autoFirst
        };

        var response = await _httpClient.PostAsJsonAsync("/api/mergeduplicates/merge", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MergeDuplicatesResponse>();
    }

    /// <summary>
    /// Merge all duplicate keys (auto-first strategy).
    /// </summary>
    public async Task<MergeDuplicatesResponse?> MergeAllAsync(bool autoFirst = true)
    {
        var request = new MergeDuplicatesRequest
        {
            MergeAll = true,
            AutoFirst = autoFirst
        };

        var response = await _httpClient.PostAsJsonAsync("/api/mergeduplicates/merge", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MergeDuplicatesResponse>();
    }
}
