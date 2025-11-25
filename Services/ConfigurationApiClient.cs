using System.Net.Http.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for configuration management operations.
/// </summary>
public class ConfigurationApiClient
{
    private readonly HttpClient _httpClient;

    public ConfigurationApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Get current configuration (with auto-reload support).
    /// </summary>
    public async Task<ConfigurationResponse?> GetConfigurationAsync()
    {
        return await _httpClient.GetFromJsonAsync<ConfigurationResponse>("/api/configuration");
    }

    /// <summary>
    /// Update existing lrm.json configuration.
    /// </summary>
    public async Task<OperationResponse?> UpdateConfigurationAsync(ConfigurationModel configuration)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/configuration", configuration);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }

    /// <summary>
    /// Create new lrm.json configuration file.
    /// </summary>
    public async Task<OperationResponse?> CreateConfigurationAsync()
    {
        var response = await _httpClient.PostAsync("/api/configuration", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }

    /// <summary>
    /// Validate configuration without saving.
    /// </summary>
    public async Task<ConfigValidationResponse?> ValidateConfigurationAsync()
    {
        var response = await _httpClient.PostAsync("/api/configuration/validate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConfigValidationResponse>();
    }

    /// <summary>
    /// Get configuration schema.
    /// </summary>
    public async Task<object?> GetSchemaAsync()
    {
        return await _httpClient.GetFromJsonAsync<object>("/api/configuration/schema");
    }
}
