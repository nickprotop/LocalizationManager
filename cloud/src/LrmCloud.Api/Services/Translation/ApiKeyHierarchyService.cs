using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Resolves API keys and configurations using the hierarchy: Project → User → Organization → Platform.
/// </summary>
public class ApiKeyHierarchyService : IApiKeyHierarchyService
{
    private readonly AppDbContext _db;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly IConfiguration _configuration;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiKeyHierarchyService(
        AppDbContext db,
        IApiKeyEncryptionService encryption,
        IConfiguration configuration)
    {
        _db = db;
        _encryption = encryption;
        _configuration = configuration;
    }

    public async Task<(string? ApiKey, string? Source)> ResolveApiKeyAsync(
        string provider,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        provider = provider.ToLowerInvariant();

        // 1. Check project-level key (highest priority)
        if (projectId.HasValue)
        {
            var projectKey = await _db.ProjectApiKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.Provider == provider);

            if (projectKey?.EncryptedKey != null)
            {
                return (_encryption.Decrypt(projectKey.EncryptedKey), "project");
            }
        }

        // 2. Check user-level key
        if (userId.HasValue)
        {
            var userKey = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider);

            if (userKey?.EncryptedKey != null)
            {
                return (_encryption.Decrypt(userKey.EncryptedKey), "user");
            }
        }

        // 3. Check organization-level key
        if (organizationId.HasValue)
        {
            var orgKey = await _db.OrganizationApiKeys
                .FirstOrDefaultAsync(k => k.OrganizationId == organizationId && k.Provider == provider);

            if (orgKey?.EncryptedKey != null)
            {
                return (_encryption.Decrypt(orgKey.EncryptedKey), "organization");
            }
        }

        // 4. Check platform-level key (from configuration)
        var platformKey = GetPlatformApiKey(provider);
        if (!string.IsNullOrEmpty(platformKey))
        {
            return (platformKey, "platform");
        }

        return (null, null);
    }

    public async Task<ResolvedProviderConfigDto> ResolveProviderConfigAsync(
        string provider,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        provider = provider.ToLowerInvariant();
        var result = new ResolvedProviderConfigDto
        {
            Provider = provider
        };

        // Start with platform config
        var mergedConfig = new Dictionary<string, object?>();
        var configSources = new Dictionary<string, string>();
        var platformConfig = GetPlatformConfig(provider);
        if (platformConfig != null)
        {
            foreach (var kvp in platformConfig)
            {
                mergedConfig[kvp.Key] = kvp.Value;
                configSources[kvp.Key] = "platform";
            }
        }

        // Merge organization config
        if (organizationId.HasValue)
        {
            var orgKey = await _db.OrganizationApiKeys
                .FirstOrDefaultAsync(k => k.OrganizationId == organizationId && k.Provider == provider);

            if (orgKey != null)
            {
                if (orgKey.EncryptedKey != null && result.ApiKeySource == null)
                {
                    result.ApiKeySource = "organization";
                    result.MaskedApiKey = _encryption.Mask(_encryption.Decrypt(orgKey.EncryptedKey));
                }

                if (!string.IsNullOrEmpty(orgKey.ConfigJson))
                {
                    var orgConfig = JsonSerializer.Deserialize<Dictionary<string, object?>>(orgKey.ConfigJson, JsonOptions);
                    if (orgConfig != null)
                    {
                        foreach (var kvp in orgConfig)
                        {
                            if (kvp.Value != null)
                            {
                                mergedConfig[kvp.Key] = kvp.Value;
                                configSources[kvp.Key] = "organization";
                            }
                        }
                    }
                }
            }
        }

        // Merge user config (higher priority than org)
        if (userId.HasValue)
        {
            var userKey = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider);

            if (userKey != null)
            {
                if (userKey.EncryptedKey != null)
                {
                    result.ApiKeySource = "user";
                    result.MaskedApiKey = _encryption.Mask(_encryption.Decrypt(userKey.EncryptedKey));
                }

                if (!string.IsNullOrEmpty(userKey.ConfigJson))
                {
                    var userConfig = JsonSerializer.Deserialize<Dictionary<string, object?>>(userKey.ConfigJson, JsonOptions);
                    if (userConfig != null)
                    {
                        foreach (var kvp in userConfig)
                        {
                            if (kvp.Value != null)
                            {
                                mergedConfig[kvp.Key] = kvp.Value;
                                configSources[kvp.Key] = "user";
                            }
                        }
                    }
                }
            }
        }

        // Merge project config (highest priority)
        if (projectId.HasValue)
        {
            var projectKey = await _db.ProjectApiKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.Provider == provider);

            if (projectKey != null)
            {
                if (projectKey.EncryptedKey != null)
                {
                    result.ApiKeySource = "project";
                    result.MaskedApiKey = _encryption.Mask(_encryption.Decrypt(projectKey.EncryptedKey));
                }

                if (!string.IsNullOrEmpty(projectKey.ConfigJson))
                {
                    var projectConfig = JsonSerializer.Deserialize<Dictionary<string, object?>>(projectKey.ConfigJson, JsonOptions);
                    if (projectConfig != null)
                    {
                        foreach (var kvp in projectConfig)
                        {
                            if (kvp.Value != null)
                            {
                                mergedConfig[kvp.Key] = kvp.Value;
                                configSources[kvp.Key] = "project";
                            }
                        }
                    }
                }
            }
        }

        // Check platform API key if no other source found
        if (result.ApiKeySource == null)
        {
            var platformKey = GetPlatformApiKey(provider);
            if (!string.IsNullOrEmpty(platformKey))
            {
                result.ApiKeySource = "platform";
                result.MaskedApiKey = _encryption.Mask(platformKey);
            }
        }

        // Determine if provider is configured
        var requiresApiKey = ProviderConfigHelper.RequiresApiKey(provider);
        result.IsConfigured = !requiresApiKey || result.ApiKeySource != null;

        result.Config = mergedConfig.Count > 0 ? mergedConfig : null;
        result.ConfigSources = configSources.Count > 0 ? configSources : null;

        return result;
    }

    public async Task<Dictionary<string, string>> GetConfiguredProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        var result = new Dictionary<string, string>();

        // Get all known providers from Core
        var providers = LocalizationManager.Core.Translation.TranslationProviderFactory.GetSupportedProviders();

        foreach (var provider in providers)
        {
            var resolved = await ResolveProviderConfigAsync(provider, projectId, userId, organizationId);
            if (resolved.IsConfigured && resolved.ApiKeySource != null)
            {
                result[provider] = resolved.ApiKeySource;
            }
            else if (resolved.IsConfigured && !ProviderConfigHelper.RequiresApiKey(provider))
            {
                // Free providers are always "configured"
                result[provider] = "free";
            }
        }

        return result;
    }

    public async Task<ProviderConfigDto?> GetProviderConfigAsync(
        string provider,
        string level,
        int entityId)
    {
        provider = provider.ToLowerInvariant();

        switch (level.ToLowerInvariant())
        {
            case "project":
                var projectKey = await _db.ProjectApiKeys
                    .FirstOrDefaultAsync(k => k.ProjectId == entityId && k.Provider == provider);
                if (projectKey == null)
                {
                    return null;
                }
                return MapToDto(projectKey.Provider, "project", projectKey.EncryptedKey, projectKey.ConfigJson, projectKey.UpdatedAt);

            case "user":
                var userKey = await _db.UserApiKeys
                    .FirstOrDefaultAsync(k => k.UserId == entityId && k.Provider == provider);
                if (userKey == null)
                {
                    return null;
                }
                return MapToDto(userKey.Provider, "user", userKey.EncryptedKey, userKey.ConfigJson, userKey.UpdatedAt);

            case "organization":
                var orgKey = await _db.OrganizationApiKeys
                    .FirstOrDefaultAsync(k => k.OrganizationId == entityId && k.Provider == provider);
                if (orgKey == null)
                {
                    return null;
                }
                return MapToDto(orgKey.Provider, "organization", orgKey.EncryptedKey, orgKey.ConfigJson, orgKey.UpdatedAt);

            default:
                return null;
        }
    }

    public async Task SetApiKeyAsync(string provider, string plainApiKey, string level, int entityId)
    {
        await SetProviderConfigAsync(provider, level, entityId, apiKey: plainApiKey);
    }

    public async Task SetProviderConfigAsync(
        string provider,
        string level,
        int entityId,
        string? apiKey = null,
        Dictionary<string, object?>? config = null)
    {
        provider = provider.ToLowerInvariant();
        var encryptedKey = apiKey != null ? _encryption.Encrypt(apiKey) : null;
        var configJson = config != null ? JsonSerializer.Serialize(config, JsonOptions) : null;
        var now = DateTime.UtcNow;

        switch (level.ToLowerInvariant())
        {
            case "project":
                var existingProject = await _db.ProjectApiKeys
                    .FirstOrDefaultAsync(k => k.ProjectId == entityId && k.Provider == provider);

                if (existingProject != null)
                {
                    if (encryptedKey != null)
                    {
                        existingProject.EncryptedKey = encryptedKey;
                    }
                    if (configJson != null)
                    {
                        existingProject.ConfigJson = configJson;
                    }
                    existingProject.UpdatedAt = now;
                }
                else
                {
                    _db.ProjectApiKeys.Add(new ProjectApiKey
                    {
                        ProjectId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey,
                        ConfigJson = configJson,
                        UpdatedAt = now
                    });
                }
                break;

            case "user":
                var existingUser = await _db.UserApiKeys
                    .FirstOrDefaultAsync(k => k.UserId == entityId && k.Provider == provider);

                if (existingUser != null)
                {
                    if (encryptedKey != null)
                    {
                        existingUser.EncryptedKey = encryptedKey;
                    }
                    if (configJson != null)
                    {
                        existingUser.ConfigJson = configJson;
                    }
                    existingUser.UpdatedAt = now;
                }
                else
                {
                    _db.UserApiKeys.Add(new UserApiKey
                    {
                        UserId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey,
                        ConfigJson = configJson,
                        UpdatedAt = now
                    });
                }
                break;

            case "organization":
                var existingOrg = await _db.OrganizationApiKeys
                    .FirstOrDefaultAsync(k => k.OrganizationId == entityId && k.Provider == provider);

                if (existingOrg != null)
                {
                    if (encryptedKey != null)
                    {
                        existingOrg.EncryptedKey = encryptedKey;
                    }
                    if (configJson != null)
                    {
                        existingOrg.ConfigJson = configJson;
                    }
                    existingOrg.UpdatedAt = now;
                }
                else
                {
                    _db.OrganizationApiKeys.Add(new OrganizationApiKey
                    {
                        OrganizationId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey,
                        ConfigJson = configJson,
                        UpdatedAt = now
                    });
                }
                break;

            default:
                throw new ArgumentException($"Invalid level: {level}", nameof(level));
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> RemoveApiKeyAsync(string provider, string level, int entityId)
    {
        return await RemoveProviderConfigAsync(provider, level, entityId);
    }

    public async Task<bool> RemoveProviderConfigAsync(string provider, string level, int entityId)
    {
        provider = provider.ToLowerInvariant();

        switch (level.ToLowerInvariant())
        {
            case "project":
                var projectKey = await _db.ProjectApiKeys
                    .FirstOrDefaultAsync(k => k.ProjectId == entityId && k.Provider == provider);
                if (projectKey != null)
                {
                    _db.ProjectApiKeys.Remove(projectKey);
                    await _db.SaveChangesAsync();
                    return true;
                }
                break;

            case "user":
                var userKey = await _db.UserApiKeys
                    .FirstOrDefaultAsync(k => k.UserId == entityId && k.Provider == provider);
                if (userKey != null)
                {
                    _db.UserApiKeys.Remove(userKey);
                    await _db.SaveChangesAsync();
                    return true;
                }
                break;

            case "organization":
                var orgKey = await _db.OrganizationApiKeys
                    .FirstOrDefaultAsync(k => k.OrganizationId == entityId && k.Provider == provider);
                if (orgKey != null)
                {
                    _db.OrganizationApiKeys.Remove(orgKey);
                    await _db.SaveChangesAsync();
                    return true;
                }
                break;
        }

        return false;
    }

    public async Task<List<ProviderConfigSummaryDto>> GetProviderSummariesAsync(
        string level,
        int entityId,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        var providers = LocalizationManager.Core.Translation.TranslationProviderFactory.GetSupportedProviders();
        var result = new List<ProviderConfigSummaryDto>();

        foreach (var providerName in providers)
        {
            var displayName = GetProviderDisplayName(providerName);
            var requiresApiKey = ProviderConfigHelper.RequiresApiKey(providerName);

            // Get config at this specific level
            var configAtLevel = await GetProviderConfigAsync(providerName, level, entityId);

            // Get effective config from hierarchy
            var resolved = await ResolveProviderConfigAsync(providerName, projectId, userId, organizationId);

            result.Add(new ProviderConfigSummaryDto
            {
                Provider = providerName,
                DisplayName = displayName,
                RequiresApiKey = requiresApiKey,
                HasConfigAtThisLevel = configAtLevel != null,
                HasApiKeyAtThisLevel = configAtLevel?.HasApiKey ?? false,
                EffectiveApiKeySource = resolved.ApiKeySource,
                ConfigSummary = GetConfigSummary(configAtLevel?.Config)
            });
        }

        return result;
    }

    private ProviderConfigDto MapToDto(string provider, string level, string? encryptedKey, string? configJson, DateTime updatedAt)
    {
        Dictionary<string, object?>? config = null;
        if (!string.IsNullOrEmpty(configJson))
        {
            config = JsonSerializer.Deserialize<Dictionary<string, object?>>(configJson, JsonOptions);
        }

        return new ProviderConfigDto
        {
            Provider = provider,
            Level = level,
            HasApiKey = encryptedKey != null,
            MaskedApiKey = encryptedKey != null ? _encryption.Mask(_encryption.Decrypt(encryptedKey)) : null,
            Config = config,
            UpdatedAt = updatedAt
        };
    }

    private string? GetPlatformApiKey(string provider)
    {
        // Check configuration for platform-level keys
        var configKey = $"TranslationProviders:{provider}:ApiKey";
        var apiKey = _configuration[configKey];

        if (string.IsNullOrEmpty(apiKey))
        {
            // Also check environment variables with standard naming
            var envVarName = $"LRM_{provider.ToUpperInvariant()}_API_KEY";
            apiKey = Environment.GetEnvironmentVariable(envVarName);
        }

        return apiKey;
    }

    private Dictionary<string, object?>? GetPlatformConfig(string provider)
    {
        // Check configuration for platform-level provider config
        var section = _configuration.GetSection($"TranslationProviders:{provider}");
        if (!section.Exists()) return null;

        var config = new Dictionary<string, object?>();
        foreach (var child in section.GetChildren())
        {
            if (child.Key != "ApiKey") // Don't include API key in config
            {
                config[child.Key] = child.Value;
            }
        }

        return config.Count > 0 ? config : null;
    }

    private static string GetProviderDisplayName(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => "Google Translate",
            "deepl" => "DeepL",
            "azure" => "Azure Translator",
            "azureopenai" => "Azure OpenAI",
            "openai" => "OpenAI",
            "claude" => "Claude",
            "ollama" => "Ollama",
            "libretranslate" => "LibreTranslate",
            "lingva" => "Lingva",
            "mymemory" => "MyMemory",
            _ => provider
        };
    }

    private static string? GetConfigSummary(Dictionary<string, object?>? config)
    {
        if (config == null || config.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();

        // Prioritize important fields
        if (config.TryGetValue("model", out var model) && model != null)
        {
            parts.Add($"model: {model}");
        }
        if (config.TryGetValue("apiUrl", out var apiUrl) && apiUrl != null)
        {
            parts.Add($"url: {apiUrl}");
        }
        if (config.TryGetValue("endpoint", out var endpoint) && endpoint != null)
        {
            parts.Add($"endpoint: {endpoint}");
        }
        if (config.TryGetValue("instanceUrl", out var instanceUrl) && instanceUrl != null)
        {
            parts.Add($"instance: {instanceUrl}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
