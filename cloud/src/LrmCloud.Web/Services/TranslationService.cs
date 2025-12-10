using System.Net.Http.Json;
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

        return await _httpClient.GetFromJsonAsync<List<TranslationProviderDto>>(url)
            ?? new List<TranslationProviderDto>();
    }

    /// <summary>
    /// Get translation usage statistics.
    /// </summary>
    public async Task<TranslationUsageDto?> GetUsageAsync()
    {
        return await _httpClient.GetFromJsonAsync<TranslationUsageDto>("translation/usage");
    }

    /// <summary>
    /// Get usage breakdown by provider.
    /// </summary>
    public async Task<List<ProviderUsageDto>> GetUsageByProviderAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ProviderUsageDto>>("translation/usage/providers")
            ?? new List<ProviderUsageDto>();
    }

    /// <summary>
    /// Translate resource keys for a project.
    /// </summary>
    public async Task<TranslateResponseDto> TranslateKeysAsync(int projectId, TranslateRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync($"translation/projects/{projectId}/translate", request);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TranslateResponseDto>()
                ?? new TranslateResponseDto { Success = false, Errors = ["Failed to parse response"] };
        }

        var errorResult = await response.Content.ReadFromJsonAsync<TranslateResponseDto>();
        return errorResult ?? new TranslateResponseDto
        {
            Success = false,
            Errors = [$"Translation failed: {response.StatusCode}"]
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
            return await response.Content.ReadFromJsonAsync<TranslateSingleResponseDto>()
                ?? new TranslateSingleResponseDto { Success = false, Error = "Failed to parse response" };
        }

        var errorResult = await response.Content.ReadFromJsonAsync<TranslateSingleResponseDto>();
        return errorResult ?? new TranslateSingleResponseDto
        {
            Success = false,
            Error = $"Translation failed: {response.StatusCode}"
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
            return await response.Content.ReadFromJsonAsync<TestApiKeyResponse>()
                ?? new TestApiKeyResponse { IsValid = false, Error = "Failed to parse response" };
        }

        return new TestApiKeyResponse
        {
            IsValid = false,
            Error = $"Test failed: {response.StatusCode}"
        };
    }

    private static async Task<ServiceResult> ParseServiceResultAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return ServiceResult.Success();
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return ServiceResult.Failure(error?.Error ?? $"Request failed: {response.StatusCode}");
        }
        catch
        {
            return ServiceResult.Failure($"Request failed: {response.StatusCode}");
        }
    }

    private class ErrorResponse
    {
        public string? Error { get; set; }
    }
}
