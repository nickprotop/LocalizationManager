using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for validation operations.
/// </summary>
public class ValidationApiClient
{
    private readonly HttpClient _httpClient;

    public ValidationApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Validate resource files (includes placeholder validation).
    /// </summary>
    public async Task<ValidationResponse?> ValidateAsync(ValidateRequest? request = null)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/validation/validate", request ?? new ValidateRequest());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ValidationResponse>();
    }

    /// <summary>
    /// Get validation issues summary.
    /// </summary>
    public async Task<Models.Api.ValidationSummary?> GetIssuesAsync()
    {
        return await _httpClient.GetFromJsonAsync<Models.Api.ValidationSummary>("/api/validation/issues");
    }
}
