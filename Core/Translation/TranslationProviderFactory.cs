// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation.Providers;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Factory for creating translation provider instances.
/// </summary>
public static class TranslationProviderFactory
{
    /// <summary>
    /// Creates a translation provider by name.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "deepl", "libretranslate", "openai", "claude", "ollama", "azureopenai").</param>
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

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static string[] GetSupportedProviders()
    {
        return new[] { "google", "deepl", "libretranslate", "ollama", "openai", "claude", "azureopenai" };
    }

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

        return providerName.ToLowerInvariant() switch
        {
            "google" or "deepl" or "libretranslate" or "ollama" or "openai" or "claude" or "azureopenai" => true,
            _ => false
        };
    }
}
