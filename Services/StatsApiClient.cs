using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for statistics operations.
/// </summary>
public class StatsApiClient
{
    private readonly HttpClient _httpClient;

    public StatsApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Get translation coverage statistics.
    /// </summary>
    public async Task<StatsResponse?> GetStatsAsync()
    {
        return await _httpClient.GetFromJsonAsync<StatsResponse>("/api/stats");
    }
}
