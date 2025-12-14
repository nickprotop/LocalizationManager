using LrmCloud.Api.Authorization;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for machine translation operations.
/// </summary>
[Authorize]
[Route("api/[controller]")]
[EnableRateLimiting("translation")]
public class TranslationController : ApiControllerBase
{
    private readonly ICloudTranslationService _translationService;
    private readonly IApiKeyHierarchyService _keyHierarchy;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly ILrmAuthorizationService _authService;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(
        ICloudTranslationService translationService,
        IApiKeyHierarchyService keyHierarchy,
        IApiKeyEncryptionService encryption,
        ILrmAuthorizationService authService,
        ILogger<TranslationController> logger)
    {
        _translationService = translationService;
        _keyHierarchy = keyHierarchy;
        _encryption = encryption;
        _authService = authService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get available translation providers for a project.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(ApiResponse<List<TranslationProviderDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<TranslationProviderDto>>>> GetProviders(
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();
        var providers = await _translationService.GetAvailableProvidersAsync(projectId, userId, organizationId);
        return Success(providers);
    }

    /// <summary>
    /// Get translation usage statistics.
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(ApiResponse<TranslationUsageDto>), 200)]
    public async Task<ActionResult<ApiResponse<TranslationUsageDto>>> GetUsage()
    {
        var userId = GetUserId();
        var usage = await _translationService.GetUsageAsync(userId);
        return Success(usage);
    }

    /// <summary>
    /// Get usage breakdown by provider.
    /// </summary>
    [HttpGet("usage/providers")]
    [ProducesResponseType(typeof(ApiResponse<List<ProviderUsageDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ProviderUsageDto>>>> GetUsageByProvider()
    {
        var userId = GetUserId();
        var usage = await _translationService.GetUsageByProviderAsync(userId);
        return Success(usage);
    }

    /// <summary>
    /// Translate resource keys for a project.
    /// </summary>
    [HttpPost("projects/{projectId}/translate")]
    [ProducesResponseType(typeof(ApiResponse<TranslateResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<TranslateResponseDto>>> TranslateKeys(
        int projectId,
        [FromBody] TranslateRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid translation request");

        var userId = GetUserId();
        var result = await _translationService.TranslateKeysAsync(projectId, userId, request);

        // Always return Success wrapper - the result itself contains Success/Errors
        return Success(result);
    }

    /// <summary>
    /// Translate a single text (for preview/testing).
    /// </summary>
    [HttpPost("translate-single")]
    [ProducesResponseType(typeof(ApiResponse<TranslateSingleResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<TranslateSingleResponseDto>>> TranslateSingle(
        [FromBody] TranslateSingleRequestDto request,
        [FromQuery] int? projectId = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid translation request");

        var userId = GetUserId();
        var result = await _translationService.TranslateSingleAsync(userId, request, projectId);

        // Always return Success wrapper - the result itself contains Success/Error
        return Success(result);
    }

    // =========================================================================
    // API Key Management
    // =========================================================================

    /// <summary>
    /// Set an API key for a translation provider at user level.
    /// </summary>
    [HttpPost("keys/user")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> SetUserApiKey([FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid request");

        var userId = GetUserId();

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "user", userId);
            return Success("API key saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set user API key for {Provider}", request.ProviderName);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save API key");
        }
    }

    /// <summary>
    /// Remove an API key at user level.
    /// </summary>
    [HttpDelete("keys/user/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveUserApiKey(string provider)
    {
        var userId = GetUserId();
        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "user", userId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_KEY_NOT_FOUND, "API key not found");

        return Success("API key removed");
    }

    /// <summary>
    /// Set an API key for a translation provider at project level.
    /// </summary>
    [HttpPost("keys/projects/{projectId}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse>> SetProjectApiKey(int projectId, [FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid request");

        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have permission to edit this project");

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "project", projectId);
            return Success("API key saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set project API key for {Provider}", request.ProviderName);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save API key");
        }
    }

    /// <summary>
    /// Remove an API key at project level.
    /// </summary>
    [HttpDelete("keys/projects/{projectId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveProjectApiKey(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have permission to edit this project");

        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "project", projectId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_KEY_NOT_FOUND, "API key not found");

        return Success("API key removed");
    }

    /// <summary>
    /// Set an API key for a translation provider at organization level.
    /// </summary>
    [HttpPost("keys/organizations/{organizationId}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse>> SetOrganizationApiKey(int organizationId, [FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid request");

        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You must be an organization admin to manage API keys");

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "organization", organizationId);
            return Success("API key saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization API key for {Provider}", request.ProviderName);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save API key");
        }
    }

    /// <summary>
    /// Remove an API key at organization level.
    /// </summary>
    [HttpDelete("keys/organizations/{organizationId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveOrganizationApiKey(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You must be an organization admin to manage API keys");

        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "organization", organizationId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_KEY_NOT_FOUND, "API key not found");

        return Success("API key removed");
    }

    // =========================================================================
    // Provider Configuration Management (API Keys + Config)
    // =========================================================================

    /// <summary>
    /// Get provider configuration at user level.
    /// </summary>
    [HttpGet("config/user/{provider}")]
    [ProducesResponseType(typeof(ApiResponse<ProviderConfigDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProviderConfigDto>>> GetUserProviderConfig(string provider)
    {
        var userId = GetUserId();
        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "user", userId);

        if (config == null)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success(config);
    }

    /// <summary>
    /// Set provider configuration at user level (API key and/or config).
    /// </summary>
    [HttpPut("config/user/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> SetUserProviderConfig(string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "user", userId, request.ApiKey, request.Config);
            return Success("Provider configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set user provider config for {Provider}", provider);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save provider configuration");
        }
    }

    /// <summary>
    /// Remove provider configuration at user level.
    /// </summary>
    [HttpDelete("config/user/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveUserProviderConfig(string provider)
    {
        var userId = GetUserId();
        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "user", userId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success("Provider configuration removed");
    }

    /// <summary>
    /// Get provider configuration at organization level.
    /// </summary>
    [HttpGet("config/organizations/{organizationId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse<ProviderConfigDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProviderConfigDto>>> GetOrganizationProviderConfig(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationMemberAsync(userId, organizationId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You must be a member of this organization");

        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "organization", organizationId);

        if (config == null)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success(config);
    }

    /// <summary>
    /// Set provider configuration at organization level.
    /// </summary>
    [HttpPut("config/organizations/{organizationId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse>> SetOrganizationProviderConfig(int organizationId, string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You must be an organization admin to manage provider configuration");

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "organization", organizationId, request.ApiKey, request.Config);
            return Success("Provider configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization provider config for {Provider}", provider);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save provider configuration");
        }
    }

    /// <summary>
    /// Remove provider configuration at organization level.
    /// </summary>
    [HttpDelete("config/organizations/{organizationId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveOrganizationProviderConfig(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You must be an organization admin to manage provider configuration");

        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "organization", organizationId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success("Provider configuration removed");
    }

    /// <summary>
    /// Get provider configuration at project level.
    /// </summary>
    [HttpGet("config/projects/{projectId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse<ProviderConfigDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse<ProviderConfigDto>>> GetProjectProviderConfig(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have access to this project");

        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "project", projectId);

        if (config == null)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success(config);
    }

    /// <summary>
    /// Set provider configuration at project level.
    /// </summary>
    [HttpPut("config/projects/{projectId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse>> SetProjectProviderConfig(int projectId, string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have permission to edit this project");

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "project", projectId, request.ApiKey, request.Config);
            return Success("Provider configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set project provider config for {Provider}", provider);
            return BadRequest(ErrorCodes.TRN_SAVE_FAILED, "Failed to save provider configuration");
        }
    }

    /// <summary>
    /// Remove provider configuration at project level.
    /// </summary>
    [HttpDelete("config/projects/{projectId}/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> RemoveProjectProviderConfig(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have permission to edit this project");

        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "project", projectId);

        if (!removed)
            return NotFound(ErrorCodes.TRN_CONFIG_NOT_FOUND, "Provider configuration not found");

        return Success("Provider configuration removed");
    }

    /// <summary>
    /// Get resolved (merged) configuration for a provider in a given context.
    /// </summary>
    [HttpGet("config/resolved/{provider}")]
    [ProducesResponseType(typeof(ApiResponse<ResolvedProviderConfigDto>), 200)]
    public async Task<ActionResult<ApiResponse<ResolvedProviderConfigDto>>> GetResolvedProviderConfig(
        string provider,
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();
        var resolved = await _keyHierarchy.ResolveProviderConfigAsync(provider, projectId, userId, organizationId);
        return Success(resolved);
    }

    /// <summary>
    /// Get summary of all providers at a specific level.
    /// </summary>
    [HttpGet("config/summary/{level}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<List<ProviderConfigSummaryDto>>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<ActionResult<ApiResponse<List<ProviderConfigSummaryDto>>>> GetProviderSummaries(
        string level,
        int entityId,
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();

        // For user level, entityId should match the authenticated user
        if (level.Equals("user", StringComparison.OrdinalIgnoreCase) && entityId != userId)
        {
            return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You can only view your own provider configuration");
        }

        var summaries = await _keyHierarchy.GetProviderSummariesAsync(level, entityId, projectId, userId, organizationId);
        return Success(summaries);
    }

    // =========================================================================
    // API Key Testing
    // =========================================================================

    /// <summary>
    /// Test an API key without saving it.
    /// </summary>
    [HttpPost("keys/test")]
    [ProducesResponseType(typeof(ApiResponse<TestApiKeyResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<TestApiKeyResponse>>> TestApiKey([FromBody] TestApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid request");

        try
        {
            // Create a temporary provider to test the key
            var config = new LocalizationManager.Core.Configuration.ConfigurationModel();

            // Set the API key based on provider
            config.Translation = new LocalizationManager.Core.Configuration.TranslationConfiguration
            {
                ApiKeys = new LocalizationManager.Core.Configuration.TranslationApiKeys()
            };

            switch (request.ProviderName.ToLowerInvariant())
            {
                case "google":
                    config.Translation.ApiKeys.Google = request.ApiKey;
                    break;
                case "deepl":
                    config.Translation.ApiKeys.DeepL = request.ApiKey;
                    break;
                case "openai":
                    config.Translation.ApiKeys.OpenAI = request.ApiKey;
                    break;
                case "claude":
                    config.Translation.ApiKeys.Claude = request.ApiKey;
                    break;
                case "azuretranslator":
                    config.Translation.ApiKeys.AzureTranslator = request.ApiKey;
                    break;
                case "azureopenai":
                    config.Translation.ApiKeys.AzureOpenAI = request.ApiKey;
                    break;
                case "libretranslate":
                    config.Translation.ApiKeys.LibreTranslate = request.ApiKey;
                    break;
                default:
                    // For providers that don't require API keys or need special handling
                    return Success(new TestApiKeyResponse
                    {
                        IsValid = true,
                        ProviderMessage = "Provider configuration accepted"
                    });
            }

            var provider = LocalizationManager.Core.Translation.TranslationProviderFactory.Create(
                request.ProviderName, config);

            // Test with a simple translation
            var testRequest = new LocalizationManager.Core.Translation.TranslationRequest
            {
                SourceText = "Hello",
                SourceLanguage = "en",
                TargetLanguage = "es"
            };

            var result = await provider.TranslateAsync(testRequest);

            return Success(new TestApiKeyResponse
            {
                IsValid = true,
                ProviderMessage = $"Test successful: 'Hello' → '{result.TranslatedText}'"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API key test failed for {Provider}", request.ProviderName);
            return Success(new TestApiKeyResponse
            {
                IsValid = false,
                Error = ex.Message
            });
        }
    }
}
