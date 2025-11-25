using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for import operations.
/// </summary>
public class ImportApiClient
{
    private readonly HttpClient _httpClient;

    public ImportApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Import resources from CSV data.
    /// </summary>
    public async Task<ImportResult?> ImportFromCsvAsync(ImportCsvRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/import/csv", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportResult>();
    }
}
