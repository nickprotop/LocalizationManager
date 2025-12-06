// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Resolves API keys from multiple sources with priority order.
/// Priority: 1) Environment variables, 2) Secure credential store, 3) Configuration file.
/// </summary>
public static class ApiKeyResolver
{
    /// <summary>
    /// Gets the API key for the specified translation provider.
    /// </summary>
    /// <param name="provider">The provider name (e.g., "google", "deepl", "libretranslate").</param>
    /// <param name="config">The configuration model, or null.</param>
    /// <returns>The API key if found, otherwise null.</returns>
    /// <remarks>
    /// Priority order:
    /// 1. Environment variable (LRM_GOOGLE_API_KEY, LRM_DEEPL_API_KEY, etc.)
    /// 2. Secure credential store (if UseSecureCredentialStore is enabled)
    /// 3. Plain configuration file (lrm.json)
    /// </remarks>
    public static string? GetApiKey(string provider, ConfigurationModel? config)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(provider));
        }

        // 1. Try environment variable (highest priority)
        var envVar = $"LRM_{provider.ToUpperInvariant()}_API_KEY";
        var envKey = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        // 2. Try secure credential store (if enabled)
        if (config?.Translation?.UseSecureCredentialStore == true)
        {
            try
            {
                var secureKey = SecureCredentialManager.GetApiKey(provider);
                if (!string.IsNullOrWhiteSpace(secureKey))
                {
                    return secureKey;
                }
            }
            catch
            {
                // If secure store fails, fall through to config file
                // This can happen if the credentials file is corrupted or has wrong format
            }
        }

        // 3. Try plain configuration file (lowest priority)
        return config?.Translation?.ApiKeys?.GetKeyForProvider(provider);
    }

    /// <summary>
    /// Gets the source of the API key for display purposes.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="config">The configuration model, or null.</param>
    /// <returns>A description of where the API key was found, or null if not found.</returns>
    public static string? GetApiKeySource(string provider, ConfigurationModel? config)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        // Check environment variable
        var envVar = $"LRM_{provider.ToUpperInvariant()}_API_KEY";
        var envKey = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return $"Environment variable ({envVar})";
        }

        // Check secure store
        if (config?.Translation?.UseSecureCredentialStore == true)
        {
            try
            {
                var secureKey = SecureCredentialManager.GetApiKey(provider);
                if (!string.IsNullOrWhiteSpace(secureKey))
                {
                    return "Secure credential store";
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        // Check configuration file
        var configKey = config?.Translation?.ApiKeys?.GetKeyForProvider(provider);
        if (!string.IsNullOrWhiteSpace(configKey))
        {
            return "Configuration file (lrm.json)";
        }

        return null;
    }

    /// <summary>
    /// Checks if an API key is configured for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="config">The configuration model, or null.</param>
    /// <returns>True if an API key is found, otherwise false.</returns>
    public static bool HasApiKey(string provider, ConfigurationModel? config)
    {
        return !string.IsNullOrWhiteSpace(GetApiKey(provider, config));
    }
}
