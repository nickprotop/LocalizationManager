// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CredentialsController : ControllerBase
{
    private readonly ConfigurationService _configService;
    private readonly string _resourcePath;

    public CredentialsController(IConfiguration configuration, ConfigurationService configService)
    {
        _configService = configService;
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Get list of all providers with their credential source status
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<CredentialProvidersResponse> GetProviders()
    {
        try
        {
            var config = _configService.GetConfiguration();
            var providerInfos = TranslationProviderFactory.GetProviderInfos();
            var secureProviders = SecureCredentialManager.GetConfiguredProviders();

            var providers = providerInfos.Select(p =>
            {
                string? source = null;

                // Check environment variable first (highest priority)
                var envVar = $"LRM_{p.Name.ToUpperInvariant()}_API_KEY";
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
                {
                    source = "environment";
                }
                // Check secure store
                else if (secureProviders.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    source = "secure_store";
                }
                // Check config file
                else if (!string.IsNullOrWhiteSpace(config?.Translation?.ApiKeys?.GetKeyForProvider(p.Name)))
                {
                    source = "config_file";
                }

                return new CredentialProviderInfo
                {
                    Provider = p.Name,
                    DisplayName = p.DisplayName,
                    RequiresApiKey = p.RequiresApiKey,
                    Source = source,
                    IsConfigured = source != null || !p.RequiresApiKey
                };
            }).ToList();

            return Ok(new CredentialProvidersResponse
            {
                Providers = providers,
                UseSecureCredentialStore = config?.Translation?.UseSecureCredentialStore ?? false
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Set API key in secure credential store
    /// </summary>
    [HttpPut("{provider}")]
    public ActionResult<OperationResponse> SetApiKey(string provider, [FromBody] SetApiKeyRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(new ErrorResponse { Error = "Provider name is required" });
            }

            if (string.IsNullOrWhiteSpace(request?.ApiKey))
            {
                return BadRequest(new ErrorResponse { Error = "API key is required" });
            }

            // Validate provider name
            var validProviders = TranslationProviderFactory.GetProviderInfos()
                .Where(p => p.RequiresApiKey)
                .Select(p => p.Name.ToLowerInvariant())
                .ToHashSet();

            if (!validProviders.Contains(provider.ToLowerInvariant()))
            {
                return BadRequest(new ErrorResponse { Error = $"Unknown or keyless provider: {provider}" });
            }

            // Store in secure credential store
            SecureCredentialManager.SetApiKey(provider.ToLowerInvariant(), request.ApiKey);

            // Enable secure credential store in config if not already
            var config = _configService.GetConfiguration() ?? new ConfigurationModel();
            if (config.Translation == null)
            {
                config.Translation = new TranslationConfiguration();
            }
            if (!config.Translation.UseSecureCredentialStore)
            {
                config.Translation.UseSecureCredentialStore = true;
                _configService.SaveConfiguration(config);
            }

            return Ok(new OperationResponse
            {
                Success = true,
                Message = $"API key for {provider} stored securely"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete API key from secure credential store
    /// </summary>
    [HttpDelete("{provider}")]
    public ActionResult<OperationResponse> DeleteApiKey(string provider)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(new ErrorResponse { Error = "Provider name is required" });
            }

            var deleted = SecureCredentialManager.DeleteApiKey(provider.ToLowerInvariant());

            if (deleted)
            {
                return Ok(new OperationResponse
                {
                    Success = true,
                    Message = $"API key for {provider} removed from secure store"
                });
            }
            else
            {
                return Ok(new OperationResponse
                {
                    Success = false,
                    Message = $"No API key found for {provider} in secure store"
                });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get the source of an API key (for display purposes, never returns the actual key)
    /// </summary>
    [HttpGet("{provider}/source")]
    public ActionResult<CredentialSourceResponse> GetApiKeySource(string provider)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(new ErrorResponse { Error = "Provider name is required" });
            }

            var config = _configService.GetConfiguration();
            var source = ApiKeyResolver.GetApiKeySource(provider.ToLowerInvariant(), config);

            return Ok(new CredentialSourceResponse
            {
                Provider = provider,
                Source = source,
                IsConfigured = source != null
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Test provider connection with configured credentials
    /// </summary>
    [HttpPost("{provider}/test")]
    public async Task<ActionResult<ProviderTestResponse>> TestProvider(string provider)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest(new ErrorResponse { Error = "Provider name is required" });
            }

            var config = _configService.GetConfiguration();

            // Check if provider has credentials configured
            var apiKey = ApiKeyResolver.GetApiKey(provider.ToLowerInvariant(), config);
            var providerInfo = TranslationProviderFactory.GetProviderInfos()
                .FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (providerInfo == null)
            {
                return BadRequest(new ErrorResponse { Error = $"Unknown provider: {provider}" });
            }

            if (providerInfo.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
            {
                return Ok(new ProviderTestResponse
                {
                    Success = false,
                    Provider = provider,
                    Message = "No API key configured for this provider"
                });
            }

            // Try to create the provider and make a test translation
            try
            {
                var translationProvider = TranslationProviderFactory.Create(provider.ToLowerInvariant(), config);

                var testRequest = new TranslationRequest
                {
                    SourceText = "Hello",
                    SourceLanguage = "en",
                    TargetLanguage = "es",
                    Context = "test"
                };

                var response = await translationProvider.TranslateAsync(testRequest);

                return Ok(new ProviderTestResponse
                {
                    Success = true,
                    Provider = provider,
                    Message = $"Connection successful! Test translation: 'Hello' -> '{response.TranslatedText}'"
                });
            }
            catch (Exception ex)
            {
                // Sanitize error message
                var message = ex.Message.Contains("API key") || ex.Message.Contains("authentication")
                    ? "Authentication failed - check your API key"
                    : ex.Message.Contains("rate limit")
                        ? "Rate limit exceeded - try again later"
                        : ex.Message.Contains("network") || ex is HttpRequestException
                            ? "Network error - check your connection"
                            : "Provider test failed";

                return Ok(new ProviderTestResponse
                {
                    Success = false,
                    Provider = provider,
                    Message = message
                });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Enable or disable the secure credential store
    /// </summary>
    [HttpPut("secure-store")]
    public ActionResult<OperationResponse> SetSecureStoreEnabled([FromBody] SetSecureStoreRequest request)
    {
        try
        {
            var config = _configService.GetConfiguration() ?? new ConfigurationModel();
            if (config.Translation == null)
            {
                config.Translation = new TranslationConfiguration();
            }

            config.Translation.UseSecureCredentialStore = request.Enabled;
            _configService.SaveConfiguration(config);

            return Ok(new OperationResponse
            {
                Success = true,
                Message = request.Enabled
                    ? "Secure credential store enabled"
                    : "Secure credential store disabled"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }
}

// Request/Response models
public class SetApiKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
}

public class SetSecureStoreRequest
{
    public bool Enabled { get; set; }
}

public class CredentialProviderInfo
{
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool RequiresApiKey { get; set; }
    public string? Source { get; set; }
    public bool IsConfigured { get; set; }
}

public class CredentialProvidersResponse
{
    public List<CredentialProviderInfo> Providers { get; set; } = new();
    public bool UseSecureCredentialStore { get; set; }
}

public class CredentialSourceResponse
{
    public string Provider { get; set; } = string.Empty;
    public string? Source { get; set; }
    public bool IsConfigured { get; set; }
}

public class ProviderTestResponse
{
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
