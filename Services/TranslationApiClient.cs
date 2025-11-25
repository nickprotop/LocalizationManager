using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for translation operations.
/// </summary>
public class TranslationApiClient
{
    private readonly HttpClient _httpClient;

    public TranslationApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Get list of available translation providers.
    /// </summary>
    public async Task<TranslationProvidersResponse?> GetProvidersAsync()
    {
        return await _httpClient.GetFromJsonAsync<TranslationProvidersResponse>("/api/translation/providers");
    }

    /// <summary>
    /// Translate keys using specified provider.
    /// </summary>
    public async Task<TranslationResponse?> TranslateAsync(TranslateRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/translation/translate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TranslationResponse>();
    }
}
