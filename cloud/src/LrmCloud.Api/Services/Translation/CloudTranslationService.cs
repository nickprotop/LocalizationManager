using System.Diagnostics;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Cloud translation service that wraps LocalizationManager.Core translation providers.
/// Handles API key resolution, usage tracking, and caching.
/// </summary>
public class CloudTranslationService : ICloudTranslationService
{
    private readonly AppDbContext _db;
    private readonly IApiKeyHierarchyService _keyHierarchy;
    private readonly ILrmTranslationProvider _lrmProvider;
    private readonly CloudConfiguration _config;
    private readonly ILogger<CloudTranslationService> _logger;

    public CloudTranslationService(
        AppDbContext db,
        IApiKeyHierarchyService keyHierarchy,
        ILrmTranslationProvider lrmProvider,
        IOptions<CloudConfiguration> config,
        ILogger<CloudTranslationService> logger)
    {
        _db = db;
        _keyHierarchy = keyHierarchy;
        _lrmProvider = lrmProvider;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<List<TranslationProviderDto>> GetAvailableProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        var result = new List<TranslationProviderDto>();

        // Add LRM provider first (our managed translation service)
        if (_config.LrmProvider.Enabled && userId.HasValue)
        {
            var (available, reason) = await _lrmProvider.IsAvailableAsync(userId.Value);
            var remaining = userId.HasValue ? await _lrmProvider.GetRemainingCharsAsync(userId.Value) : 0;

            result.Add(new TranslationProviderDto
            {
                Name = "lrm",
                DisplayName = "LRM Translation",
                RequiresApiKey = false, // User doesn't need to provide API key
                IsConfigured = available,
                Type = "managed",
                IsManagedProvider = true, // This is our managed service, not free
                IsAiProvider = false,
                Description = available
                    ? $"LRM managed translation ({remaining:N0} chars remaining)"
                    : reason ?? "LRM translation unavailable",
                ApiKeySource = "platform"
            });
        }

        // Add BYOK providers (user's own API keys)
        var providers = TranslationProviderFactory.GetProviderInfos();
        var configuredProviders = await _keyHierarchy.GetConfiguredProvidersAsync(projectId, userId, organizationId);

        foreach (var provider in providers)
        {
            var isConfigured = configuredProviders.TryGetValue(provider.Name, out var source);

            result.Add(new TranslationProviderDto
            {
                Name = provider.Name,
                DisplayName = provider.DisplayName,
                RequiresApiKey = provider.RequiresApiKey,
                IsConfigured = isConfigured || !provider.RequiresApiKey,
                Type = IsLocalProvider(provider.Name) ? "local" : "api",
                IsAiProvider = IsAiProvider(provider.Name),
                Description = GetProviderDescription(provider.Name),
                ApiKeySource = isConfigured ? source : null
            });
        }

        return result;
    }

    public async Task<TranslateResponseDto> TranslateKeysAsync(
        int projectId,
        int userId,
        TranslateRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new TranslateResponseDto();

        try
        {
            // Get project for organization context
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                response.Errors.Add("Project not found");
                return response;
            }

            // Determine provider
            var providerName = request.Provider ?? await GetBestAvailableProviderAsync(
                projectId, userId, project.OrganizationId);

            if (string.IsNullOrEmpty(providerName))
            {
                response.Errors.Add("No translation provider configured. Please configure an API key.");
                return response;
            }

            // Check if using LRM provider
            var isLrmProvider = providerName.Equals("lrm", StringComparison.OrdinalIgnoreCase);

            ITranslationProvider? provider = null;
            if (!isLrmProvider)
            {
                // Create BYOK provider instance
                provider = await CreateProviderAsync(providerName, projectId, userId, project.OrganizationId);
                if (provider == null)
                {
                    response.Errors.Add($"Failed to initialize provider: {providerName}");
                    return response;
                }
            }

            response.Provider = providerName;

            // Get source language
            var sourceLanguage = request.SourceLanguage ?? project.DefaultLanguage;

            // Get keys to translate
            var keysQuery = _db.ResourceKeys
                .Include(k => k.Translations)
                .Where(k => k.ProjectId == projectId);

            if (request.Keys.Any())
            {
                keysQuery = keysQuery.Where(k => request.Keys.Contains(k.KeyName));
            }

            var keys = await keysQuery.ToListAsync();

            // Process each key and target language
            foreach (var key in keys)
            {
                var sourceTranslation = key.Translations
                    .FirstOrDefault(t => t.LanguageCode == sourceLanguage);

                if (sourceTranslation == null || string.IsNullOrEmpty(sourceTranslation.Value))
                {
                    response.SkippedCount++;
                    continue;
                }

                foreach (var targetLang in request.TargetLanguages)
                {
                    if (targetLang == sourceLanguage)
                    {
                        continue;
                    }

                    var existingTranslation = key.Translations
                        .FirstOrDefault(t => t.LanguageCode == targetLang);

                    // Skip if translation exists and we're not overwriting
                    if (!request.Overwrite && existingTranslation != null &&
                        !string.IsNullOrEmpty(existingTranslation.Value))
                    {
                        if (request.OnlyMissing)
                        {
                            response.SkippedCount++;
                            continue;
                        }
                    }

                    var result = new TranslationResultDto
                    {
                        Key = key.KeyName,
                        TargetLanguage = targetLang,
                        SourceText = sourceTranslation.Value
                    };

                    try
                    {
                        string? translatedText;
                        bool fromCache = false;

                        if (isLrmProvider)
                        {
                            // Use LRM managed provider
                            var lrmResult = await _lrmProvider.TranslateAsync(
                                userId,
                                sourceTranslation.Value,
                                sourceLanguage,
                                targetLang,
                                request.Context ?? key.Comment);

                            if (!lrmResult.Success)
                            {
                                result.Success = false;
                                result.Error = lrmResult.Error;
                                response.FailedCount++;
                                response.Results.Add(result);
                                continue;
                            }

                            translatedText = lrmResult.TranslatedText;
                            fromCache = lrmResult.FromCache;
                        }
                        else
                        {
                            // Use BYOK provider
                            var translationRequest = new TranslationRequest
                            {
                                SourceText = sourceTranslation.Value,
                                SourceLanguage = sourceLanguage,
                                TargetLanguage = targetLang,
                                Context = request.Context ?? key.Comment
                            };

                            var translationResponse = await provider!.TranslateAsync(translationRequest);
                            translatedText = translationResponse.TranslatedText;
                            fromCache = translationResponse.FromCache;
                        }

                        result.TranslatedText = translatedText ?? string.Empty;
                        result.Success = true;
                        result.FromCache = fromCache;

                        // Only save to database if requested (default for CLI, skip for UI preview)
                        if (request.SaveToDatabase)
                        {
                            if (existingTranslation != null)
                            {
                                existingTranslation.Value = translatedText;
                                existingTranslation.Status = "translated";
                                existingTranslation.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                key.Translations.Add(new Shared.Entities.Translation
                                {
                                    ResourceKeyId = key.Id,
                                    LanguageCode = targetLang,
                                    Value = translatedText,
                                    Status = "translated"
                                });
                            }
                        }

                        response.TranslatedCount++;
                        response.CharactersTranslated += sourceTranslation.Value.Length;
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.Error = ex.Message;
                        response.FailedCount++;
                        _logger.LogWarning(ex, "Translation failed for key {Key} to {Language}",
                            key.KeyName, targetLang);
                    }

                    response.Results.Add(result);
                }
            }

            // Save changes only if requested
            if (request.SaveToDatabase)
            {
                await _db.SaveChangesAsync();
            }

            // Track other providers usage (LRM usage is tracked in LrmTranslationProvider)
            if (!isLrmProvider && response.CharactersTranslated > 0)
            {
                await TrackOtherUsageAsync(userId, response.CharactersTranslated);
            }

            // Track per-provider usage for analytics
            if (response.CharactersTranslated > 0)
            {
                await TrackProviderUsageAsync(userId, request.Provider ?? "auto", response.CharactersTranslated, response.TranslatedCount);
            }

            response.Success = response.FailedCount == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation batch failed for project {ProjectId}", projectId);
            response.Errors.Add($"Translation failed: {ex.Message}");
        }

        stopwatch.Stop();
        response.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return response;
    }

    public async Task<TranslateSingleResponseDto> TranslateSingleAsync(
        int userId,
        TranslateSingleRequestDto request,
        int? projectId = null)
    {
        var response = new TranslateSingleResponseDto();

        try
        {
            int? organizationId = null;
            if (projectId.HasValue)
            {
                var project = await _db.Projects.FindAsync(projectId);
                organizationId = project?.OrganizationId;
            }

            var providerName = request.Provider ?? await GetBestAvailableProviderAsync(
                projectId, userId, organizationId);

            if (string.IsNullOrEmpty(providerName))
            {
                response.Error = "No translation provider configured";
                return response;
            }

            var isLrmProvider = providerName.Equals("lrm", StringComparison.OrdinalIgnoreCase);
            response.Provider = providerName;

            if (isLrmProvider)
            {
                // Use LRM managed provider
                var lrmResult = await _lrmProvider.TranslateAsync(
                    userId,
                    request.Text,
                    request.SourceLanguage,
                    request.TargetLanguage,
                    request.Context);

                if (!lrmResult.Success)
                {
                    response.Error = lrmResult.Error;
                    return response;
                }

                response.Success = true;
                response.TranslatedText = lrmResult.TranslatedText ?? string.Empty;
                response.FromCache = lrmResult.FromCache;
            }
            else
            {
                // Use BYOK provider
                var provider = await CreateProviderAsync(providerName, projectId, userId, organizationId);
                if (provider == null)
                {
                    response.Error = $"Failed to initialize provider: {providerName}";
                    return response;
                }

                var translationRequest = new TranslationRequest
                {
                    SourceText = request.Text,
                    SourceLanguage = request.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    Context = request.Context
                };

                var result = await provider.TranslateAsync(translationRequest);

                response.Success = true;
                response.TranslatedText = result.TranslatedText;
                response.FromCache = result.FromCache;

                // Track other providers usage
                await TrackOtherUsageAsync(userId, request.Text.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Single translation failed");
            response.Error = ex.Message;
        }

        return response;
    }

    public async Task<TranslationUsageDto> GetUsageAsync(int userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.TranslationCharsUsed,
                u.TranslationCharsLimit,
                u.TranslationCharsResetAt,
                u.OtherCharsUsed,
                u.OtherCharsLimit,
                u.OtherCharsResetAt,
                u.Plan
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new TranslationUsageDto
            {
                Plan = "free",
                CharactersUsed = 0,
                CharacterLimit = _config.Limits.FreeTranslationChars,
                OtherCharactersUsed = 0,
                OtherCharacterLimit = _config.Limits.FreeOtherChars
            };
        }

        return new TranslationUsageDto
        {
            // LRM usage (counts against plan)
            CharactersUsed = user.TranslationCharsUsed,
            CharacterLimit = user.TranslationCharsLimit,
            ResetsAt = user.TranslationCharsResetAt,
            Plan = user.Plan,
            // Other providers usage (BYOK + free community)
            OtherCharactersUsed = user.OtherCharsUsed,
            OtherCharacterLimit = user.OtherCharsLimit,
            OtherResetsAt = user.OtherCharsResetAt
        };
    }

    public async Task<List<ProviderUsageDto>> GetUsageByProviderAsync(int userId)
    {
        // Get current month period
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var usageRecords = await _db.TranslationUsageHistory
            .Where(h => h.UserId == userId && h.PeriodStart == periodStart)
            .OrderByDescending(h => h.CharsUsed)
            .ToListAsync();

        return usageRecords.Select(r => new ProviderUsageDto
        {
            ProviderName = r.ProviderName,
            CharactersUsed = r.CharsUsed,
            ApiCalls = r.ApiCalls,
            LastUsedAt = r.LastUsedAt
        }).ToList();
    }

    private async Task<string?> GetBestAvailableProviderAsync(
        int? projectId, int userId, int? organizationId)
    {
        // Check LRM provider first (preferred - our managed service)
        if (_config.LrmProvider.Enabled)
        {
            var (available, _) = await _lrmProvider.IsAvailableAsync(userId);
            if (available)
            {
                return "lrm";
            }
        }

        // Fallback to BYOK providers
        var preferredOrder = new[]
        {
            "mymemory",  // Free, no API key needed
            "lingva",    // Free, no API key needed
            "google",    // Best quality
            "deepl",     // High quality
            "claude",    // AI, good quality
            "openai",    // AI
            "azuretranslator",
            "azureopenai",
            "libretranslate",
            "ollama"     // Local, needs setup
        };

        var configuredProviders = await _keyHierarchy.GetConfiguredProvidersAsync(
            projectId, userId, organizationId);

        // Return first available provider from priority list
        foreach (var provider in preferredOrder)
        {
            var info = TranslationProviderFactory.GetProviderInfos()
                .FirstOrDefault(p => p.Name == provider);

            if (info == null)
            {
                continue;
            }

            // Provider doesn't require API key, or has one configured
            if (!info.RequiresApiKey || configuredProviders.ContainsKey(provider))
            {
                return provider;
            }
        }

        return null;
    }

    private async Task TrackOtherUsageAsync(int userId, long charsUsed)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return;
        }

        user.OtherCharsUsed += charsUsed;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogDebug("Other providers usage tracked: {Chars} chars for user {UserId}", charsUsed, userId);
    }

    private async Task TrackProviderUsageAsync(int userId, string providerName, long charsUsed, int apiCalls)
    {
        try
        {
            // Get current month period
            var now = DateTime.UtcNow;
            var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = periodStart.AddMonths(1).AddTicks(-1);

            // Find or create usage record for this user+provider+period
            var usage = await _db.TranslationUsageHistory
                .FirstOrDefaultAsync(h =>
                    h.UserId == userId &&
                    h.ProviderName == providerName &&
                    h.PeriodStart == periodStart);

            if (usage == null)
            {
                usage = new Shared.Entities.TranslationUsageHistory
                {
                    UserId = userId,
                    ProviderName = providerName,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    CharsUsed = 0,
                    ApiCalls = 0
                };
                _db.TranslationUsageHistory.Add(usage);
            }

            usage.CharsUsed += charsUsed;
            usage.ApiCalls += apiCalls;
            usage.LastUsedAt = now;

            await _db.SaveChangesAsync();

            _logger.LogDebug(
                "Provider usage tracked: {Provider} - {Chars} chars, {Calls} calls for user {UserId}",
                providerName, charsUsed, apiCalls, userId);
        }
        catch (Exception ex)
        {
            // Don't fail the translation if usage tracking fails
            _logger.LogWarning(ex, "Failed to track provider usage for {Provider}, user {UserId}", providerName, userId);
        }
    }

    private async Task<(bool allowed, string? reason)> CheckOtherLimitAsync(int userId, long charsToUse)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.OtherCharsUsed, u.OtherCharsLimit, u.Plan })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return (false, "User not found");
        }

        // Enterprise has unlimited
        if (user.Plan.Equals("enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        var remaining = user.OtherCharsLimit - user.OtherCharsUsed;
        if (remaining <= 0)
        {
            return (false, $"Other providers limit reached ({user.OtherCharsLimit:N0} chars/month). Upgrade your plan for more.");
        }

        if (charsToUse > remaining)
        {
            return (false, $"Request exceeds remaining limit ({remaining:N0} chars remaining)");
        }

        return (true, null);
    }

    private async Task<ITranslationProvider?> CreateProviderAsync(
        string providerName,
        int? projectId,
        int? userId,
        int? organizationId)
    {
        try
        {
            // Resolve API key and config from hierarchy
            var resolved = await _keyHierarchy.ResolveProviderConfigAsync(
                providerName, projectId, userId, organizationId);

            // Get the actual API key (not masked) for the provider
            var (apiKey, _) = await _keyHierarchy.ResolveApiKeyAsync(
                providerName, projectId, userId, organizationId);

            // Create config model with merged provider settings
            var config = new ConfigurationModel
            {
                Translation = new TranslationConfiguration
                {
                    DefaultProvider = providerName
                }
            };

            // Set the API key and apply provider-specific configuration
            ApplyProviderConfig(config, providerName, apiKey, resolved.Config);

            return TranslationProviderFactory.Create(providerName, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create provider {Provider}", providerName);
            return null;
        }
    }

    /// <summary>
    /// Applies the resolved API key and provider-specific configuration to the config model.
    /// </summary>
    private static void ApplyProviderConfig(
        ConfigurationModel config,
        string provider,
        string? apiKey,
        Dictionary<string, object?>? providerConfig)
    {
        config.Translation ??= new TranslationConfiguration();
        config.Translation.ApiKeys ??= new TranslationApiKeys();
        config.Translation.AIProviders ??= new AIProviderConfiguration();

        switch (provider.ToLowerInvariant())
        {
            case "google":
                config.Translation.ApiKeys.Google = apiKey;
                // Google doesn't have additional config currently
                break;

            case "deepl":
                config.Translation.ApiKeys.DeepL = apiKey;
                // DeepL doesn't have additional config currently
                break;

            case "openai":
                config.Translation.ApiKeys.OpenAI = apiKey;
                config.Translation.AIProviders.OpenAI = new OpenAISettings
                {
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "claude":
                config.Translation.ApiKeys.Claude = apiKey;
                config.Translation.AIProviders.Claude = new ClaudeSettings
                {
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "azureopenai":
                config.Translation.ApiKeys.AzureOpenAI = apiKey;
                config.Translation.AIProviders.AzureOpenAI = new AzureOpenAISettings
                {
                    Endpoint = GetConfigString(providerConfig, "endpoint"),
                    DeploymentName = GetConfigString(providerConfig, "deploymentName"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "azuretranslator":
                config.Translation.ApiKeys.AzureTranslator = apiKey;
                config.Translation.AIProviders.AzureTranslator = new AzureTranslatorSettings
                {
                    Region = GetConfigString(providerConfig, "region"),
                    Endpoint = GetConfigString(providerConfig, "endpoint"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "ollama":
                // Ollama doesn't require API key but has config
                config.Translation.AIProviders.Ollama = new OllamaSettings
                {
                    ApiUrl = GetConfigString(providerConfig, "apiUrl"),
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "lingva":
                // Lingva doesn't require API key but has config
                config.Translation.AIProviders.Lingva = new LingvaSettings
                {
                    InstanceUrl = GetConfigString(providerConfig, "instanceUrl"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "libretranslate":
                config.Translation.ApiKeys.LibreTranslate = apiKey;
                // LibreTranslate config would be apiUrl but that's not in current settings
                // TODO: Add LibreTranslateSettings to Core if needed
                break;

            case "mymemory":
                // MyMemory is free and has optional email config
                config.Translation.AIProviders.MyMemory = new MyMemorySettings
                {
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;
        }
    }

    /// <summary>
    /// Gets a string value from the provider config dictionary.
    /// </summary>
    private static string? GetConfigString(Dictionary<string, object?>? config, string key)
    {
        if (config == null)
        {
            return null;
        }

        // Try exact key first
        if (config.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }

        // Try case-insensitive
        var matchingKey = config.Keys.FirstOrDefault(k =>
            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

        if (matchingKey != null && config[matchingKey] != null)
        {
            return config[matchingKey]?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets an integer value from the provider config dictionary.
    /// </summary>
    private static int? GetConfigInt(Dictionary<string, object?>? config, string key)
    {
        var strValue = GetConfigString(config, key);
        if (strValue != null && int.TryParse(strValue, out var intValue))
        {
            return intValue;
        }
        return null;
    }

    private static bool IsLocalProvider(string provider) =>
        provider is "ollama" or "libretranslate";

    private static bool IsAiProvider(string provider) =>
        provider is "openai" or "claude" or "azureopenai" or "ollama";

    private static string? GetProviderDescription(string provider) => provider switch
    {
        "google" => "Google Cloud Translation API with excellent language coverage",
        "deepl" => "High-quality neural machine translation, great for European languages",
        "openai" => "GPT models for context-aware translation",
        "claude" => "Anthropic's Claude for nuanced, context-aware translation",
        "azuretranslator" => "Microsoft Azure Translator with enterprise features",
        "azureopenai" => "Azure-hosted OpenAI models",
        "lingva" => "Free Google Translate proxy (no API key needed)",
        "mymemory" => "Free translation memory service (no API key needed)",
        "libretranslate" => "Self-hosted open source translation",
        "ollama" => "Local LLM for private, offline translation",
        _ => null
    };
}
