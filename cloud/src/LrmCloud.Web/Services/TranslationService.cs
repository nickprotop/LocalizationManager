using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Translation;

namespace LrmCloud.Web.Services;

/// <summary>
/// Frontend service for translation API operations.
/// </summary>
public class TranslationService
{
    private readonly HttpClient _httpClient;

    public TranslationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Get available translation providers with configuration status.
    /// </summary>
    public async Task<List<TranslationProviderDto>> GetProvidersAsync(int? projectId = null, int? organizationId = null)
    {
        var url = "translation/providers";
        var queryParams = new List<string>();

        if (projectId.HasValue)
            queryParams.Add($"projectId={projectId}");
        if (organizationId.HasValue)
            queryParams.Add($"organizationId={organizationId}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TranslationProviderDto>>>();
        return result?.Data ?? new List<TranslationProviderDto>();
    }

    /// <summary>
    /// Get translation usage statistics.
    /// </summary>
    public async Task<TranslationUsageDto?> GetUsageAsync()
    {
        var response = await _httpClient.GetAsync("translation/usage");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranslationUsageDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Get usage breakdown by provider.
    /// </summary>
    public async Task<List<ProviderUsageDto>> GetUsageByProviderAsync()
    {
        var response = await _httpClient.GetAsync("translation/usage/providers");
        if (!response.IsSuccessStatusCode)
            return new List<ProviderUsageDto>();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProviderUsageDto>>>();
        return result?.Data ?? new List<ProviderUsageDto>();
    }

    /// <summary>
    /// Translate resource keys for a project.
    /// </summary>
    public async Task<TranslateResponseDto> TranslateKeysAsync(int projectId, TranslateRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync($"translation/projects/{projectId}/translate", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranslateResponseDto>>();
            return result?.Data ?? new TranslateResponseDto { Success = false, Errors = ["Failed to parse response"] };
        }

        // Try to parse error response
        var error = await ReadErrorMessageAsync(response);
        return new TranslateResponseDto
        {
            Success = false,
            Errors = [error]
        };
    }

    /// <summary>
    /// Translate a single text (for preview/testing).
    /// </summary>
    public async Task<TranslateSingleResponseDto> TranslateSingleAsync(TranslateSingleRequestDto request, int? projectId = null)
    {
        var url = "translation/translate-single";
        if (projectId.HasValue)
            url += $"?projectId={projectId}";

        var response = await _httpClient.PostAsJsonAsync(url, request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TranslateSingleResponseDto>>();
            return result?.Data ?? new TranslateSingleResponseDto { Success = false, Error = "Failed to parse response" };
        }

        var error = await ReadErrorMessageAsync(response);
        return new TranslateSingleResponseDto
        {
            Success = false,
            Error = error
        };
    }

    // =========================================================================
    // API Key Management
    // =========================================================================

    /// <summary>
    /// Set an API key at user level.
    /// </summary>
    public async Task<ServiceResult> SetUserApiKeyAsync(SetApiKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("translation/keys/user", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove an API key at user level.
    /// </summary>
    public async Task<ServiceResult> RemoveUserApiKeyAsync(string provider)
    {
        var response = await _httpClient.DeleteAsync($"translation/keys/user/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Set an API key at project level.
    /// </summary>
    public async Task<ServiceResult> SetProjectApiKeyAsync(int projectId, SetApiKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"translation/keys/projects/{projectId}", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove an API key at project level.
    /// </summary>
    public async Task<ServiceResult> RemoveProjectApiKeyAsync(int projectId, string provider)
    {
        var response = await _httpClient.DeleteAsync($"translation/keys/projects/{projectId}/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Set an API key at organization level.
    /// </summary>
    public async Task<ServiceResult> SetOrganizationApiKeyAsync(int organizationId, SetApiKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"translation/keys/organizations/{organizationId}", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove an API key at organization level.
    /// </summary>
    public async Task<ServiceResult> RemoveOrganizationApiKeyAsync(int organizationId, string provider)
    {
        var response = await _httpClient.DeleteAsync($"translation/keys/organizations/{organizationId}/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Test an API key without saving it.
    /// </summary>
    public async Task<TestApiKeyResponse> TestApiKeyAsync(TestApiKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("translation/keys/test", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TestApiKeyResponse>>();
            return result?.Data ?? new TestApiKeyResponse { IsValid = false, Error = "Failed to parse response" };
        }

        return new TestApiKeyResponse
        {
            IsValid = false,
            Error = $"Test failed: {response.StatusCode}"
        };
    }

    // =========================================================================
    // Provider Configuration Management
    // =========================================================================

    /// <summary>
    /// Get provider configuration at user level.
    /// </summary>
    public async Task<ProviderConfigDto?> GetUserProviderConfigAsync(string provider)
    {
        var response = await _httpClient.GetAsync($"translation/config/user/{provider}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProviderConfigDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Set provider configuration at user level (API key and/or config).
    /// </summary>
    public async Task<ServiceResult> SetUserProviderConfigAsync(string provider, SetProviderConfigRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"translation/config/user/{provider}", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove provider configuration at user level.
    /// </summary>
    public async Task<ServiceResult> RemoveUserProviderConfigAsync(string provider)
    {
        var response = await _httpClient.DeleteAsync($"translation/config/user/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Get provider configuration at organization level.
    /// </summary>
    public async Task<ProviderConfigDto?> GetOrganizationProviderConfigAsync(int organizationId, string provider)
    {
        var response = await _httpClient.GetAsync($"translation/config/organizations/{organizationId}/{provider}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProviderConfigDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Set provider configuration at organization level.
    /// </summary>
    public async Task<ServiceResult> SetOrganizationProviderConfigAsync(
        int organizationId, string provider, SetProviderConfigRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"translation/config/organizations/{organizationId}/{provider}", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove provider configuration at organization level.
    /// </summary>
    public async Task<ServiceResult> RemoveOrganizationProviderConfigAsync(int organizationId, string provider)
    {
        var response = await _httpClient.DeleteAsync(
            $"translation/config/organizations/{organizationId}/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Get provider configuration at project level.
    /// </summary>
    public async Task<ProviderConfigDto?> GetProjectProviderConfigAsync(int projectId, string provider)
    {
        var response = await _httpClient.GetAsync($"translation/config/projects/{projectId}/{provider}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProviderConfigDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Set provider configuration at project level.
    /// </summary>
    public async Task<ServiceResult> SetProjectProviderConfigAsync(
        int projectId, string provider, SetProviderConfigRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"translation/config/projects/{projectId}/{provider}", request);
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Remove provider configuration at project level.
    /// </summary>
    public async Task<ServiceResult> RemoveProjectProviderConfigAsync(int projectId, string provider)
    {
        var response = await _httpClient.DeleteAsync(
            $"translation/config/projects/{projectId}/{provider}");
        return await ParseServiceResultAsync(response);
    }

    /// <summary>
    /// Get resolved (merged) configuration for a provider.
    /// </summary>
    public async Task<ResolvedProviderConfigDto?> GetResolvedProviderConfigAsync(
        string provider, int? projectId = null, int? organizationId = null)
    {
        var url = $"translation/config/resolved/{provider}";
        var queryParams = new List<string>();

        if (projectId.HasValue)
            queryParams.Add($"projectId={projectId}");
        if (organizationId.HasValue)
            queryParams.Add($"organizationId={organizationId}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResolvedProviderConfigDto>>();
        return result?.Data;
    }

    /// <summary>
    /// Parses API response into ServiceResult.
    /// </summary>
    private static async Task<ServiceResult> ParseServiceResultAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            return ServiceResult.Success(result?.Message);
        }

        var error = await ReadErrorMessageAsync(response);
        return ServiceResult.Failure(error);
    }

    /// <summary>
    /// Reads error message from ProblemDetails or other error response formats.
    /// </summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("\"detail\""))
            {
                var problem = System.Text.Json.JsonDocument.Parse(content);
                if (problem.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "An error occurred";
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
