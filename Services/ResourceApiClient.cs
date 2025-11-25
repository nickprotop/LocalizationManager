using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for resource operations (CRUD for localization keys).
/// </summary>
public class ResourceApiClient
{
    private readonly HttpClient _httpClient;

    public ResourceApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// List all resource files.
    /// </summary>
    public async Task<List<ResourceFileInfo>?> GetResourceFilesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ResourceFileInfo>>("/api/resources");
    }

    /// <summary>
    /// List all keys with their values across all languages.
    /// </summary>
    public async Task<List<ResourceKeyInfo>?> GetAllKeysAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ResourceKeyInfo>>("/api/resources/keys");
    }

    /// <summary>
    /// Get details for a specific key.
    /// </summary>
    public async Task<ResourceKeyDetails?> GetKeyAsync(string keyName)
    {
        return await _httpClient.GetFromJsonAsync<ResourceKeyDetails>($"/api/resources/keys/{Uri.EscapeDataString(keyName)}");
    }

    /// <summary>
    /// Add a new key to all resource files.
    /// </summary>
    public async Task<OperationResponse?> AddKeyAsync(AddKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/resources/keys", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }

    /// <summary>
    /// Update an existing key.
    /// </summary>
    public async Task<OperationResponse?> UpdateKeyAsync(string keyName, UpdateKeyRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/resources/keys/{Uri.EscapeDataString(keyName)}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }

    /// <summary>
    /// Delete a key (with optional occurrence number for duplicates).
    /// </summary>
    public async Task<OperationResponse?> DeleteKeyAsync(string keyName, int? occurrence = null)
    {
        var url = $"/api/resources/keys/{Uri.EscapeDataString(keyName)}";
        if (occurrence.HasValue)
        {
            url += $"?occurrence={occurrence.Value}";
        }
        var response = await _httpClient.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }
}
