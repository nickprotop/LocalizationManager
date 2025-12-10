using LrmCloud.Api.Data;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Resolves API keys using the hierarchy: Project → User → Organization → Platform.
/// </summary>
public class ApiKeyHierarchyService : IApiKeyHierarchyService
{
    private readonly AppDbContext _db;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly IConfiguration _configuration;

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

            if (projectKey != null)
            {
                return (_encryption.Decrypt(projectKey.EncryptedKey), "project");
            }
        }

        // 2. Check user-level key
        if (userId.HasValue)
        {
            var userKey = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider);

            if (userKey != null)
            {
                return (_encryption.Decrypt(userKey.EncryptedKey), "user");
            }
        }

        // 3. Check organization-level key
        if (organizationId.HasValue)
        {
            var orgKey = await _db.OrganizationApiKeys
                .FirstOrDefaultAsync(k => k.OrganizationId == organizationId && k.Provider == provider);

            if (orgKey != null)
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
            var (_, source) = await ResolveApiKeyAsync(provider, projectId, userId, organizationId);
            if (source != null)
            {
                result[provider] = source;
            }
        }

        return result;
    }

    public async Task SetApiKeyAsync(string provider, string plainApiKey, string level, int entityId)
    {
        provider = provider.ToLowerInvariant();
        var encryptedKey = _encryption.Encrypt(plainApiKey);

        switch (level.ToLowerInvariant())
        {
            case "project":
                var existingProject = await _db.ProjectApiKeys
                    .FirstOrDefaultAsync(k => k.ProjectId == entityId && k.Provider == provider);

                if (existingProject != null)
                {
                    existingProject.EncryptedKey = encryptedKey;
                }
                else
                {
                    _db.ProjectApiKeys.Add(new ProjectApiKey
                    {
                        ProjectId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey
                    });
                }
                break;

            case "user":
                var existingUser = await _db.UserApiKeys
                    .FirstOrDefaultAsync(k => k.UserId == entityId && k.Provider == provider);

                if (existingUser != null)
                {
                    existingUser.EncryptedKey = encryptedKey;
                }
                else
                {
                    _db.UserApiKeys.Add(new UserApiKey
                    {
                        UserId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey
                    });
                }
                break;

            case "organization":
                var existingOrg = await _db.OrganizationApiKeys
                    .FirstOrDefaultAsync(k => k.OrganizationId == entityId && k.Provider == provider);

                if (existingOrg != null)
                {
                    existingOrg.EncryptedKey = encryptedKey;
                }
                else
                {
                    _db.OrganizationApiKeys.Add(new OrganizationApiKey
                    {
                        OrganizationId = entityId,
                        Provider = provider,
                        EncryptedKey = encryptedKey
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

    private string? GetPlatformApiKey(string provider)
    {
        // Check configuration for platform-level keys
        // These can be set in appsettings.json or environment variables
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
}
