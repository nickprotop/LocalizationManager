// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation.Providers;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Metadata about a translation provider.
/// </summary>
/// <param name="Name">The provider identifier (e.g., "google", "deepl").</param>
/// <param name="DisplayName">Human-readable name for UI display.</param>
/// <param name="RequiresApiKey">Whether the provider requires an API key to function.</param>
public record ProviderInfo(string Name, string DisplayName, bool RequiresApiKey);

/// <summary>
/// Factory for creating translation provider instances.
/// </summary>
public static class TranslationProviderFactory
{
    /// <summary>
    /// Centralized metadata for all supported translation providers.
    /// This is the single source of truth for provider information.
    /// </summary>
    private static readonly ProviderInfo[] _providers = new[]
    {
        new ProviderInfo("google", "Google Cloud Translation", RequiresApiKey: true),
        new ProviderInfo("deepl", "DeepL", RequiresApiKey: true),
        new ProviderInfo("libretranslate", "LibreTranslate", RequiresApiKey: false),
        new ProviderInfo("ollama", "Ollama (Local LLM)", RequiresApiKey: false),
        new ProviderInfo("openai", "OpenAI", RequiresApiKey: true),
        new ProviderInfo("claude", "Anthropic Claude", RequiresApiKey: true),
        new ProviderInfo("azureopenai", "Azure OpenAI", RequiresApiKey: true),
        new ProviderInfo("azuretranslator", "Azure Translator", RequiresApiKey: true),
        new ProviderInfo("lingva", "Lingva (Google via proxy)", RequiresApiKey: false),
        new ProviderInfo("mymemory", "MyMemory", RequiresApiKey: false),
    };
    /// <summary>
    /// Creates a translation provider by name.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "deepl", "libretranslate", "openai", "claude", "ollama", "azureopenai", "azuretranslator").</param>
    /// <param name="config">The configuration model containing API keys and settings.</param>
    /// <returns>An instance of the requested provider.</returns>
    /// <exception cref="ArgumentException">If the provider name is unknown.</exception>
    public static ITranslationProvider Create(string providerName, ConfigurationModel? config)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
        }

        var apiKey = ApiKeyResolver.GetApiKey(providerName, config);
        var aiConfig = config?.Translation?.AIProviders;

        return providerName.ToLowerInvariant() switch
        {
            "google" => new GoogleTranslateProvider(apiKey),
            "deepl" => new DeepLProvider(apiKey),
            "libretranslate" => new LibreTranslateProvider(apiKey),
            "ollama" => CreateOllamaProvider(apiKey, aiConfig?.Ollama),
            "openai" => CreateOpenAIProvider(apiKey, aiConfig?.OpenAI),
            "claude" => CreateClaudeProvider(apiKey, aiConfig?.Claude),
            "azureopenai" => CreateAzureOpenAIProvider(apiKey, aiConfig?.AzureOpenAI),
            "azuretranslator" => CreateAzureTranslatorProvider(apiKey, aiConfig?.AzureTranslator),
            "lingva" => CreateLingvaProvider(aiConfig?.Lingva),
            "mymemory" => CreateMyMemoryProvider(aiConfig?.MyMemory),
            _ => throw new ArgumentException($"Unknown translation provider: {providerName}", nameof(providerName))
        };
    }

    private static OllamaProvider CreateOllamaProvider(string? apiKey, OllamaSettings? settings)
    {
        return new OllamaProvider(
            apiUrl: settings?.ApiUrl,
            model: settings?.Model,
            customSystemPrompt: settings?.CustomSystemPrompt,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 10
        );
    }

    private static OpenAIProvider CreateOpenAIProvider(string? apiKey, OpenAISettings? settings)
    {
        return new OpenAIProvider(
            apiKey: apiKey,
            model: settings?.Model,
            customSystemPrompt: settings?.CustomSystemPrompt,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 60
        );
    }

    private static ClaudeProvider CreateClaudeProvider(string? apiKey, ClaudeSettings? settings)
    {
        return new ClaudeProvider(
            apiKey: apiKey,
            model: settings?.Model,
            customSystemPrompt: settings?.CustomSystemPrompt,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 50
        );
    }

    private static AzureOpenAIProvider CreateAzureOpenAIProvider(string? apiKey, AzureOpenAISettings? settings)
    {
        return new AzureOpenAIProvider(
            apiKey: apiKey,
            endpoint: settings?.Endpoint,
            deploymentName: settings?.DeploymentName,
            customSystemPrompt: settings?.CustomSystemPrompt,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 60
        );
    }

    private static AzureTranslatorProvider CreateAzureTranslatorProvider(string? apiKey, AzureTranslatorSettings? settings)
    {
        return new AzureTranslatorProvider(
            apiKey: apiKey,
            region: settings?.Region,
            endpoint: settings?.Endpoint,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 100
        );
    }

    private static LingvaTranslateProvider CreateLingvaProvider(LingvaSettings? settings)
    {
        return new LingvaTranslateProvider(
            instanceUrl: settings?.InstanceUrl,
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 30
        );
    }

    private static MyMemoryProvider CreateMyMemoryProvider(MyMemorySettings? settings)
    {
        return new MyMemoryProvider(
            rateLimitRequestsPerMinute: settings?.RateLimitPerMinute ?? 20
        );
    }

    /// <summary>
    /// Gets metadata for all supported translation providers.
    /// </summary>
    /// <returns>Array of provider information including name, display name, and API key requirements.</returns>
    public static ProviderInfo[] GetProviderInfos() => _providers;

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static string[] GetSupportedProviders() => _providers.Select(p => p.Name).ToArray();

    /// <summary>
    /// Checks if a provider name is supported.
    /// </summary>
    /// <param name="providerName">The provider name to check.</param>
    /// <returns>True if supported, otherwise false.</returns>
    public static bool IsProviderSupported(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        return _providers.Any(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }
}
