using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for code scanning operations.
/// </summary>
public class ScanApiClient
{
    private readonly HttpClient _httpClient;

    public ScanApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Scan source code for localization key usage.
    /// </summary>
    public async Task<ScanResponse?> ScanAsync(ScanRequest? request = null)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/scan/scan", request ?? new ScanRequest());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScanResponse>();
    }

    /// <summary>
    /// Get list of unused keys (keys in .resx but not found in code).
    /// </summary>
    public async Task<List<string>?> GetUnusedKeysAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<UnusedKeysResponse>("/api/scan/unused");
        return response?.UnusedKeys;
    }

    /// <summary>
    /// Get list of missing keys (keys found in code but not in .resx).
    /// </summary>
    public async Task<List<string>?> GetMissingKeysAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<MissingKeysResponse>("/api/scan/missing");
        return response?.MissingKeys;
    }

    /// <summary>
    /// Get code references for a specific key.
    /// </summary>
    public async Task<List<CodeReference>?> GetReferencesAsync(string keyName)
    {
        var response = await _httpClient.GetAsync($"/api/scan/references/{Uri.EscapeDataString(keyName)}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // No references found - return empty list instead of throwing
            return new List<CodeReference>();
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeyReferencesResponse>();
        return result?.References ?? new List<CodeReference>();
    }
}
