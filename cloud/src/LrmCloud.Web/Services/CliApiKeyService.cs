using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Auth;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for managing CLI API keys.
/// </summary>
public class CliApiKeyService
{
    private readonly HttpClient _httpClient;

    public CliApiKeyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CliApiKeyDto>> GetKeysAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("settings/cli-keys");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CliApiKeyDto>>>();
                return result?.Data ?? new List<CliApiKeyDto>();
            }
        }
        catch
        {
            // Ignore errors
        }
        return new List<CliApiKeyDto>();
    }

    public async Task<CreateCliApiKeyResult> CreateKeyAsync(CreateCliApiKeyRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("settings/cli-keys", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<CreateCliApiKeyResponse>>();
                if (result?.Data != null)
                {
                    return CreateCliApiKeyResult.Success(result.Data.ApiKey, result.Data.KeyInfo);
                }
            }

            var error = await ReadErrorAsync(response);
            return CreateCliApiKeyResult.Failure(error);
        }
        catch (Exception ex)
        {
            return CreateCliApiKeyResult.Failure($"Failed to create key: {ex.Message}");
        }
    }

    public async Task<bool> DeleteKeyAsync(int keyId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"settings/cli-keys/{keyId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("\"error\""))
            {
                var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    return error.GetString() ?? "An error occurred";
                }
            }
            return content.Length < 200 ? content : "An error occurred";
        }
        catch
        {
            return $"Request failed with status {response.StatusCode}";
        }
    }
}

public class CreateCliApiKeyResult
{
    public bool IsSuccess { get; private set; }
    public string? ApiKey { get; private set; }
    public CliApiKeyDto? KeyInfo { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CreateCliApiKeyResult Success(string apiKey, CliApiKeyDto keyInfo) => new()
    {
        IsSuccess = true,
        ApiKey = apiKey,
        KeyInfo = keyInfo
    };

    public static CreateCliApiKeyResult Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };
}
