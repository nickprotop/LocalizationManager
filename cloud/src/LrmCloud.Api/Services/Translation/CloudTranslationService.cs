using System.Diagnostics;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Cloud translation service that wraps LocalizationManager.Core translation providers.
/// Handles API key resolution, usage tracking, and caching.
/// </summary>
public class CloudTranslationService : ICloudTranslationService
{
    private readonly AppDbContext _db;
    private readonly IApiKeyHierarchyService _keyHierarchy;
    private readonly ILogger<CloudTranslationService> _logger;

    public CloudTranslationService(
        AppDbContext db,
        IApiKeyHierarchyService keyHierarchy,
        ILogger<CloudTranslationService> logger)
    {
        _db = db;
        _keyHierarchy = keyHierarchy;
        _logger = logger;
    }

    public async Task<List<TranslationProviderDto>> GetAvailableProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        var providers = TranslationProviderFactory.GetProviderInfos();
        var configuredProviders = await _keyHierarchy.GetConfiguredProvidersAsync(projectId, userId, organizationId);

        var result = new List<TranslationProviderDto>();

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

            // Create provider instance
            var provider = await CreateProviderAsync(providerName, projectId, userId, project.OrganizationId);
            if (provider == null)
            {
                response.Errors.Add($"Failed to initialize provider: {providerName}");
                return response;
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
                        continue;

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
                        var translationRequest = new TranslationRequest
                        {
                            SourceText = sourceTranslation.Value,
                            SourceLanguage = sourceLanguage,
                            TargetLanguage = targetLang,
                            Context = request.Context ?? key.Comment
                        };

                        var translationResponse = await provider.TranslateAsync(translationRequest);

                        result.TranslatedText = translationResponse.TranslatedText;
                        result.Success = true;
                        result.FromCache = translationResponse.FromCache;

                        // Update or create translation in database
                        if (existingTranslation != null)
                        {
                            existingTranslation.Value = translationResponse.TranslatedText;
                            existingTranslation.Status = "translated";
                            existingTranslation.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            key.Translations.Add(new Shared.Entities.Translation
                            {
                                ResourceKeyId = key.Id,
                                LanguageCode = targetLang,
                                Value = translationResponse.TranslatedText,
                                Status = "translated"
                            });
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

            // Save changes
            await _db.SaveChangesAsync();

            // TODO: Track usage
            // await TrackUsageAsync(userId, providerName, response.CharactersTranslated);

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
            response.Provider = providerName;
            response.FromCache = result.FromCache;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Single translation failed");
            response.Error = ex.Message;
        }

        return response;
    }

    public Task<TranslationUsageDto> GetUsageAsync(int userId)
    {
        // TODO: Implement usage tracking
        return Task.FromResult(new TranslationUsageDto
        {
            CharactersUsed = 0,
            CharacterLimit = null, // Unlimited for now
            ApiCallsUsed = 0,
            ApiCallLimit = null,
            Plan = "free"
        });
    }

    public Task<List<ProviderUsageDto>> GetUsageByProviderAsync(int userId)
    {
        // TODO: Implement per-provider usage tracking
        return Task.FromResult(new List<ProviderUsageDto>());
    }

    private async Task<string?> GetBestAvailableProviderAsync(
        int? projectId, int userId, int? organizationId)
    {
        // Priority: free providers first, then by quality
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

            if (info == null) continue;

            // Provider doesn't require API key, or has one configured
            if (!info.RequiresApiKey || configuredProviders.ContainsKey(provider))
            {
                return provider;
            }
        }

        return null;
    }

    private async Task<ITranslationProvider?> CreateProviderAsync(
        string providerName,
        int? projectId,
        int? userId,
        int? organizationId)
    {
        try
        {
            // Resolve API key from hierarchy
            var (apiKey, _) = await _keyHierarchy.ResolveApiKeyAsync(
                providerName, projectId, userId, organizationId);

            // Create a minimal config model with just the API key
            var config = new ConfigurationModel
            {
                Translation = new TranslationConfiguration
                {
                    DefaultProvider = providerName
                }
            };

            // Set the appropriate API key in the config
            SetApiKeyInConfig(config, providerName, apiKey);

            return TranslationProviderFactory.Create(providerName, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create provider {Provider}", providerName);
            return null;
        }
    }

    private static void SetApiKeyInConfig(ConfigurationModel config, string provider, string? apiKey)
    {
        config.Translation ??= new TranslationConfiguration();
        config.Translation.ApiKeys ??= new TranslationApiKeys();
        config.Translation.AIProviders ??= new AIProviderConfiguration();

        switch (provider.ToLowerInvariant())
        {
            case "google":
                config.Translation.ApiKeys.Google = apiKey;
                break;
            case "deepl":
                config.Translation.ApiKeys.DeepL = apiKey;
                break;
            case "openai":
                config.Translation.AIProviders.OpenAI ??= new OpenAISettings();
                config.Translation.ApiKeys.OpenAI = apiKey;
                break;
            case "claude":
                config.Translation.AIProviders.Claude ??= new ClaudeSettings();
                config.Translation.ApiKeys.Claude = apiKey;
                break;
            case "azureopenai":
                config.Translation.AIProviders.AzureOpenAI ??= new AzureOpenAISettings();
                config.Translation.ApiKeys.AzureOpenAI = apiKey;
                break;
            case "azuretranslator":
                config.Translation.AIProviders.AzureTranslator ??= new AzureTranslatorSettings();
                config.Translation.ApiKeys.AzureTranslator = apiKey;
                break;
            case "libretranslate":
                config.Translation.ApiKeys.LibreTranslate = apiKey;
                break;
        }
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
