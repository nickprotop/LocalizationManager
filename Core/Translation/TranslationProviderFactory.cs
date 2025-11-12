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
    /// <param name="providerName">The provider name (e.g., "deepl", "libretranslate").</param>
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

        return providerName.ToLowerInvariant() switch
        {
            "google" => new GoogleTranslateProvider(apiKey),
            "deepl" => new DeepLProvider(apiKey),
            "libretranslate" => new LibreTranslateProvider(apiKey),
            _ => throw new ArgumentException($"Unknown translation provider: {providerName}", nameof(providerName))
        };
    }

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static string[] GetSupportedProviders()
    {
        return new[] { "google", "deepl", "libretranslate" };
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
            "google" or "deepl" or "libretranslate" => true,
            _ => false
        };
    }
}
