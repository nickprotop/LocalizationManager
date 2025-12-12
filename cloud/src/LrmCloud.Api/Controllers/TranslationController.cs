using LrmCloud.Api.Authorization;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.DTOs.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for machine translation operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranslationController : ControllerBase
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
    public async Task<ActionResult<List<TranslationProviderDto>>> GetProviders(
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();
        var providers = await _translationService.GetAvailableProvidersAsync(projectId, userId, organizationId);
        return Ok(providers);
    }

    /// <summary>
    /// Get translation usage statistics.
    /// </summary>
    [HttpGet("usage")]
    public async Task<ActionResult<TranslationUsageDto>> GetUsage()
    {
        var userId = GetUserId();
        var usage = await _translationService.GetUsageAsync(userId);
        return Ok(usage);
    }

    /// <summary>
    /// Get usage breakdown by provider.
    /// </summary>
    [HttpGet("usage/providers")]
    public async Task<ActionResult<List<ProviderUsageDto>>> GetUsageByProvider()
    {
        var userId = GetUserId();
        var usage = await _translationService.GetUsageByProviderAsync(userId);
        return Ok(usage);
    }

    /// <summary>
    /// Translate resource keys for a project.
    /// </summary>
    [HttpPost("projects/{projectId}/translate")]
    public async Task<ActionResult<TranslateResponseDto>> TranslateKeys(
        int projectId,
        [FromBody] TranslateRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var result = await _translationService.TranslateKeysAsync(projectId, userId, request);

        if (result.Errors.Any())
        {
            return result.Success
                ? Ok(result)  // Partial success
                : BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Translate a single text (for preview/testing).
    /// </summary>
    [HttpPost("translate-single")]
    public async Task<ActionResult<TranslateSingleResponseDto>> TranslateSingle(
        [FromBody] TranslateSingleRequestDto request,
        [FromQuery] int? projectId = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var result = await _translationService.TranslateSingleAsync(userId, request, projectId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // =========================================================================
    // API Key Management
    // =========================================================================

    /// <summary>
    /// Set an API key for a translation provider at user level.
    /// </summary>
    [HttpPost("keys/user")]
    public async Task<IActionResult> SetUserApiKey([FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "user", userId);
            return Ok(new { message = "API key saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set user API key for {Provider}", request.ProviderName);
            return BadRequest(new { error = "Failed to save API key" });
        }
    }

    /// <summary>
    /// Remove an API key at user level.
    /// </summary>
    [HttpDelete("keys/user/{provider}")]
    public async Task<IActionResult> RemoveUserApiKey(string provider)
    {
        var userId = GetUserId();
        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "user", userId);

        if (!removed)
            return NotFound(new { error = "API key not found" });

        return Ok(new { message = "API key removed" });
    }

    /// <summary>
    /// Set an API key for a translation provider at project level.
    /// </summary>
    [HttpPost("keys/projects/{projectId}")]
    public async Task<IActionResult> SetProjectApiKey(int projectId, [FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbid();

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "project", projectId);
            return Ok(new { message = "API key saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set project API key for {Provider}", request.ProviderName);
            return BadRequest(new { error = "Failed to save API key" });
        }
    }

    /// <summary>
    /// Remove an API key at project level.
    /// </summary>
    [HttpDelete("keys/projects/{projectId}/{provider}")]
    public async Task<IActionResult> RemoveProjectApiKey(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbid();

        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "project", projectId);

        if (!removed)
            return NotFound(new { error = "API key not found" });

        return Ok(new { message = "API key removed" });
    }

    /// <summary>
    /// Set an API key for a translation provider at organization level.
    /// </summary>
    [HttpPost("keys/organizations/{organizationId}")]
    public async Task<IActionResult> SetOrganizationApiKey(int organizationId, [FromBody] SetApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        try
        {
            await _keyHierarchy.SetApiKeyAsync(request.ProviderName, request.ApiKey, "organization", organizationId);
            return Ok(new { message = "API key saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization API key for {Provider}", request.ProviderName);
            return BadRequest(new { error = "Failed to save API key" });
        }
    }

    /// <summary>
    /// Remove an API key at organization level.
    /// </summary>
    [HttpDelete("keys/organizations/{organizationId}/{provider}")]
    public async Task<IActionResult> RemoveOrganizationApiKey(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        var removed = await _keyHierarchy.RemoveApiKeyAsync(provider, "organization", organizationId);

        if (!removed)
            return NotFound(new { error = "API key not found" });

        return Ok(new { message = "API key removed" });
    }

// =========================================================================
    // Provider Configuration Management (API Keys + Config)
    // =========================================================================

    /// <summary>
    /// Get provider configuration at user level.
    /// </summary>
    [HttpGet("config/user/{provider}")]
    public async Task<ActionResult<ProviderConfigDto>> GetUserProviderConfig(string provider)
    {
        var userId = GetUserId();
        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "user", userId);

        if (config == null)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(config);
    }

    /// <summary>
    /// Set provider configuration at user level (API key and/or config).
    /// </summary>
    [HttpPut("config/user/{provider}")]
    public async Task<IActionResult> SetUserProviderConfig(string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "user", userId, request.ApiKey, request.Config);
            return Ok(new { message = "Provider configuration saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set user provider config for {Provider}", provider);
            return BadRequest(new { error = "Failed to save provider configuration" });
        }
    }

    /// <summary>
    /// Remove provider configuration at user level.
    /// </summary>
    [HttpDelete("config/user/{provider}")]
    public async Task<IActionResult> RemoveUserProviderConfig(string provider)
    {
        var userId = GetUserId();
        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "user", userId);

        if (!removed)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(new { message = "Provider configuration removed" });
    }

    /// <summary>
    /// Get provider configuration at organization level.
    /// </summary>
    [HttpGet("config/organizations/{organizationId}/{provider}")]
    public async Task<ActionResult<ProviderConfigDto>> GetOrganizationProviderConfig(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationMemberAsync(userId, organizationId))
            return Forbid();

        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "organization", organizationId);

        if (config == null)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(config);
    }

    /// <summary>
    /// Set provider configuration at organization level.
    /// </summary>
    [HttpPut("config/organizations/{organizationId}/{provider}")]
    public async Task<IActionResult> SetOrganizationProviderConfig(int organizationId, string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "organization", organizationId, request.ApiKey, request.Config);
            return Ok(new { message = "Provider configuration saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization provider config for {Provider}", provider);
            return BadRequest(new { error = "Failed to save provider configuration" });
        }
    }

    /// <summary>
    /// Remove provider configuration at organization level.
    /// </summary>
    [HttpDelete("config/organizations/{organizationId}/{provider}")]
    public async Task<IActionResult> RemoveOrganizationProviderConfig(int organizationId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.IsOrganizationAdminAsync(userId, organizationId))
            return Forbid();

        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "organization", organizationId);

        if (!removed)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(new { message = "Provider configuration removed" });
    }

    /// <summary>
    /// Get provider configuration at project level.
    /// </summary>
    [HttpGet("config/projects/{projectId}/{provider}")]
    public async Task<ActionResult<ProviderConfigDto>> GetProjectProviderConfig(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.HasProjectAccessAsync(userId, projectId))
            return Forbid();

        var config = await _keyHierarchy.GetProviderConfigAsync(provider, "project", projectId);

        if (config == null)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(config);
    }

    /// <summary>
    /// Set provider configuration at project level.
    /// </summary>
    [HttpPut("config/projects/{projectId}/{provider}")]
    public async Task<IActionResult> SetProjectProviderConfig(int projectId, string provider, [FromBody] SetProviderConfigRequest request)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbid();

        try
        {
            await _keyHierarchy.SetProviderConfigAsync(provider, "project", projectId, request.ApiKey, request.Config);
            return Ok(new { message = "Provider configuration saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set project provider config for {Provider}", provider);
            return BadRequest(new { error = "Failed to save provider configuration" });
        }
    }

    /// <summary>
    /// Remove provider configuration at project level.
    /// </summary>
    [HttpDelete("config/projects/{projectId}/{provider}")]
    public async Task<IActionResult> RemoveProjectProviderConfig(int projectId, string provider)
    {
        var userId = GetUserId();
        if (!await _authService.CanEditProjectAsync(userId, projectId))
            return Forbid();

        var removed = await _keyHierarchy.RemoveProviderConfigAsync(provider, "project", projectId);

        if (!removed)
            return NotFound(new { error = "Provider configuration not found" });

        return Ok(new { message = "Provider configuration removed" });
    }

    /// <summary>
    /// Get resolved (merged) configuration for a provider in a given context.
    /// </summary>
    [HttpGet("config/resolved/{provider}")]
    public async Task<ActionResult<ResolvedProviderConfigDto>> GetResolvedProviderConfig(
        string provider,
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();
        var resolved = await _keyHierarchy.ResolveProviderConfigAsync(provider, projectId, userId, organizationId);
        return Ok(resolved);
    }

    /// <summary>
    /// Get summary of all providers at a specific level.
    /// </summary>
    [HttpGet("config/summary/{level}/{entityId}")]
    public async Task<ActionResult<List<ProviderConfigSummaryDto>>> GetProviderSummaries(
        string level,
        int entityId,
        [FromQuery] int? projectId = null,
        [FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();

        // For user level, entityId should match the authenticated user
        if (level.Equals("user", StringComparison.OrdinalIgnoreCase) && entityId != userId)
        {
            return Forbid();
        }

        var summaries = await _keyHierarchy.GetProviderSummariesAsync(level, entityId, projectId, userId, organizationId);
        return Ok(summaries);
    }

    // =========================================================================
    // API Key Testing
    // =========================================================================

    /// <summary>
    /// Test an API key without saving it.
    /// </summary>
    [HttpPost("keys/test")]
    public async Task<ActionResult<TestApiKeyResponse>> TestApiKey([FromBody] TestApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
                    return Ok(new TestApiKeyResponse
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

            return Ok(new TestApiKeyResponse
            {
                IsValid = true,
                ProviderMessage = $"Test successful: 'Hello' → '{result.TranslatedText}'"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API key test failed for {Provider}", request.ProviderName);
            return Ok(new TestApiKeyResponse
            {
                IsValid = false,
                Error = ex.Message
            });
        }
    }
}
