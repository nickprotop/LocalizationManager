using System.Text.Json.Serialization;

namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Base class for all provider configurations.
/// Contains common settings applicable to all providers.
/// </summary>
public class ProviderConfigBase
{
    /// <summary>
    /// Optional rate limit (requests per minute).
    /// When null, the platform default is used.
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration for Ollama provider.
/// Ollama is a local LLM server - no API key required.
/// </summary>
public class OllamaConfig : ProviderConfigBase
{
    /// <summary>
    /// Ollama API URL. Default: http://localhost:11434
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Model to use for translation (e.g., "llama3.2", "mistral", "qwen2.5").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation instructions.
    /// When null, the default prompt is used.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
}

/// <summary>
/// Configuration for OpenAI provider.
/// </summary>
public class OpenAIConfig : ProviderConfigBase
{
    /// <summary>
    /// Model to use (e.g., "gpt-4o-mini", "gpt-4o", "gpt-4-turbo").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation instructions.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
}

/// <summary>
/// Configuration for Anthropic Claude provider.
/// </summary>
public class ClaudeConfig : ProviderConfigBase
{
    /// <summary>
    /// Model to use (e.g., "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation instructions.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
}

/// <summary>
/// Configuration for Azure OpenAI provider.
/// </summary>
public class AzureOpenAIConfig : ProviderConfigBase
{
    /// <summary>
    /// Azure OpenAI endpoint URL (e.g., "https://your-resource.openai.azure.com").
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Deployment name for the model.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Custom system prompt for translation instructions.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
}

/// <summary>
/// Configuration for Azure Translator (Cognitive Services).
/// </summary>
public class AzureTranslatorConfig : ProviderConfigBase
{
    /// <summary>
    /// Azure region (e.g., "eastus", "westeurope").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Custom endpoint URL (optional - uses default if not specified).
    /// </summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// Configuration for Lingva Translate.
/// Lingva is a free, privacy-respecting Google Translate frontend.
/// </summary>
public class LingvaConfig : ProviderConfigBase
{
    /// <summary>
    /// Lingva instance URL (e.g., "https://lingva.ml", "https://lingva.lunar.icu").
    /// Default: https://lingva.ml
    /// </summary>
    public string? InstanceUrl { get; set; }
}

/// <summary>
/// Configuration for LibreTranslate.
/// LibreTranslate can be self-hosted or use public instances.
/// </summary>
public class LibreTranslateConfig : ProviderConfigBase
{
    /// <summary>
    /// LibreTranslate API URL (e.g., "https://libretranslate.com", "http://localhost:5000").
    /// </summary>
    public string? ApiUrl { get; set; }
}

/// <summary>
/// Configuration for Google Cloud Translation.
/// </summary>
public class GoogleTranslateConfig : ProviderConfigBase
{
    /// <summary>
    /// Google Cloud project ID.
    /// </summary>
    public string? ProjectId { get; set; }
}

/// <summary>
/// Configuration for DeepL.
/// DeepL doesn't have additional configuration options beyond the API key.
/// </summary>
public class DeepLConfig : ProviderConfigBase
{
    // DeepL uses API Free vs Pro based on key format, no additional config needed
}

/// <summary>
/// Configuration for MyMemory.
/// MyMemory is free and doesn't require configuration.
/// </summary>
public class MyMemoryConfig : ProviderConfigBase
{
    /// <summary>
    /// Optional email for higher rate limits.
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
/// Generic provider configuration for storage/transfer.
/// Contains a JSON string that can be deserialized to provider-specific config.
/// </summary>
public class ProviderConfigDto
{
    /// <summary>
    /// Provider name (e.g., "ollama", "openai").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Configuration level: "platform", "organization", "user", "project".
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Whether an API key is configured at this level.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Masked API key for display (if configured).
    /// </summary>
    public string? MaskedApiKey { get; set; }

    /// <summary>
    /// Provider-specific configuration.
    /// </summary>
    public Dictionary<string, object?>? Config { get; set; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request to set provider configuration.
/// </summary>
public class SetProviderConfigRequest
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// API key (optional - can set config without changing key).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Provider-specific configuration as JSON object.
    /// </summary>
    public Dictionary<string, object?>? Config { get; set; }
}

/// <summary>
/// Response containing resolved (merged) configuration for a provider.
/// Shows the effective configuration after merging all hierarchy levels.
/// </summary>
public class ResolvedProviderConfigDto
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Whether the provider is fully configured and ready to use.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Where the API key comes from: "platform", "organization", "user", "project".
    /// Null if no API key is configured.
    /// </summary>
    public string? ApiKeySource { get; set; }

    /// <summary>
    /// Masked API key for display.
    /// </summary>
    public string? MaskedApiKey { get; set; }

    /// <summary>
    /// Merged configuration from all levels.
    /// </summary>
    public Dictionary<string, object?>? Config { get; set; }

    /// <summary>
    /// Source of each config field: which level it came from.
    /// Key is the field name, value is the source level.
    /// </summary>
    public Dictionary<string, string>? ConfigSources { get; set; }
}

/// <summary>
/// Summary of configured providers at a specific level.
/// </summary>
public class ProviderConfigSummaryDto
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether an API key is required for this provider.
    /// </summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>
    /// Whether configuration exists at this level.
    /// </summary>
    public bool HasConfigAtThisLevel { get; set; }

    /// <summary>
    /// Whether API key exists at this level.
    /// </summary>
    public bool HasApiKeyAtThisLevel { get; set; }

    /// <summary>
    /// Effective API key source (from hierarchy resolution).
    /// </summary>
    public string? EffectiveApiKeySource { get; set; }

    /// <summary>
    /// Brief summary of config at this level (e.g., "model: gpt-4").
    /// </summary>
    public string? ConfigSummary { get; set; }
}

/// <summary>
/// Static helper class for provider configuration.
/// </summary>
public static class ProviderConfigHelper
{
    /// <summary>
    /// Known translation provider names.
    /// </summary>
    public static class Providers
    {
        public const string Google = "google";
        public const string DeepL = "deepl";
        public const string Azure = "azure";
        public const string AzureOpenAI = "azureopenai";
        public const string OpenAI = "openai";
        public const string Claude = "claude";
        public const string Ollama = "ollama";
        public const string LibreTranslate = "libretranslate";
        public const string Lingva = "lingva";
        public const string MyMemory = "mymemory";
    }

    /// <summary>
    /// Providers that don't require an API key.
    /// </summary>
    public static readonly HashSet<string> FreeProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        Providers.MyMemory,
        Providers.Lingva,
        Providers.Ollama,      // Requires URL but not API key
        Providers.LibreTranslate  // API key optional for public instances
    };

    /// <summary>
    /// Providers that have configurable options beyond API key.
    /// </summary>
    public static readonly HashSet<string> ConfigurableProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        Providers.Ollama,
        Providers.OpenAI,
        Providers.Claude,
        Providers.AzureOpenAI,
        Providers.Azure,
        Providers.Lingva,
        Providers.LibreTranslate,
        Providers.Google
    };

    /// <summary>
    /// Get the configuration type for a provider.
    /// </summary>
    public static Type? GetConfigType(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            Providers.Ollama => typeof(OllamaConfig),
            Providers.OpenAI => typeof(OpenAIConfig),
            Providers.Claude => typeof(ClaudeConfig),
            Providers.AzureOpenAI => typeof(AzureOpenAIConfig),
            Providers.Azure => typeof(AzureTranslatorConfig),
            Providers.Lingva => typeof(LingvaConfig),
            Providers.LibreTranslate => typeof(LibreTranslateConfig),
            Providers.Google => typeof(GoogleTranslateConfig),
            Providers.DeepL => typeof(DeepLConfig),
            Providers.MyMemory => typeof(MyMemoryConfig),
            _ => null
        };
    }

    /// <summary>
    /// Check if a provider requires an API key.
    /// </summary>
    public static bool RequiresApiKey(string provider)
    {
        return !FreeProviders.Contains(provider);
    }

    /// <summary>
    /// Check if a provider has configurable options.
    /// </summary>
    public static bool HasConfigOptions(string provider)
    {
        return ConfigurableProviders.Contains(provider);
    }
}
