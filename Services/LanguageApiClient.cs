using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for language file management operations.
/// </summary>
public class LanguageApiClient
{
    private readonly HttpClient _httpClient;

    public LanguageApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// List all languages in the project with coverage statistics.
    /// </summary>
    public async Task<LanguagesResponse?> GetLanguagesAsync()
    {
        return await _httpClient.GetFromJsonAsync<LanguagesResponse>("/api/language");
    }

    /// <summary>
    /// Add a new language file.
    /// </summary>
    public async Task<AddLanguageResponse?> AddLanguageAsync(AddLanguageRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/language", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AddLanguageResponse>();
    }

    /// <summary>
    /// Remove a language file.
    /// </summary>
    public async Task<RemoveLanguageResponse?> RemoveLanguageAsync(string cultureCode)
    {
        var response = await _httpClient.DeleteAsync($"/api/language/{Uri.EscapeDataString(cultureCode)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoveLanguageResponse>();
    }
}
