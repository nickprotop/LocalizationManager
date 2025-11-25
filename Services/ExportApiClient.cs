namespace LocalizationManager.Services;

/// <summary>
/// API client for export operations.
/// </summary>
public class ExportApiClient
{
    private readonly HttpClient _httpClient;

    public ExportApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Export resources to JSON format.
    /// </summary>
    public async Task<string?> ExportToJsonAsync(bool includeComments = false)
    {
        var url = $"/api/export/json?includeComments={includeComments}";
        return await _httpClient.GetStringAsync(url);
    }

    /// <summary>
    /// Export resources to CSV format.
    /// </summary>
    public async Task<string?> ExportToCsvAsync(bool includeComments = false)
    {
        var url = $"/api/export/csv?includeComments={includeComments}";
        return await _httpClient.GetStringAsync(url);
    }
}
