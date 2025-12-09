namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Represents the configuration model for LocalizationManager.
/// This class will be expanded in the future to include various configuration options.
/// </summary>
public class ConfigurationModel
{
    /// <summary>
    /// The language code for the default resource file (e.g., "en", "fr").
    /// Used for display output and as the source language for translations.
    /// If not set, displays "default" and translations use auto-detect.
    /// </summary>
    public string? DefaultLanguageCode { get; set; }

    /// <summary>
    /// Translation configuration settings.
    /// </summary>
    public TranslationConfiguration? Translation { get; set; }

    /// <summary>
    /// Code scanning configuration settings.
    /// </summary>
    public ScanningConfiguration? Scanning { get; set; }

    /// <summary>
    /// Validation configuration settings.
    /// </summary>
    public ValidationConfiguration? Validation { get; set; }

    /// <summary>
    /// Web server configuration settings.
    /// </summary>
    public WebConfiguration? Web { get; set; }

    /// <summary>
    /// Resource format: "resx" (default) or "json".
    /// Used to specify which backend to use for resource file operations.
    /// If not set, auto-detects based on existing files in the resource path.
    /// </summary>
    public string? ResourceFormat { get; set; }

    /// <summary>
    /// JSON-specific configuration settings.
    /// Only applicable when ResourceFormat is "json".
    /// </summary>
    public JsonFormatConfiguration? Json { get; set; }
}

/// <summary>
/// Configuration settings for the machine translation feature.
/// </summary>
public class TranslationConfiguration
{
    /// <summary>
    /// The default translation provider to use when not explicitly specified.
    /// Supported values: "google", "deepl", "libretranslate", "ollama", "openai", "claude", "azureopenai".
    /// Default: "google"
    /// </summary>
    public string DefaultProvider { get; set; } = "google";


    /// <summary>
    /// API keys for translation providers.
    /// Note: Environment variables and secure credential store take priority over these values.
    /// WARNING: Do not commit API keys to version control. Add lrm.json to .gitignore if it contains keys.
    /// </summary>
    public TranslationApiKeys? ApiKeys { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for failed translation requests.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout in seconds for translation requests.
    /// Default: 30
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of keys to translate in a single batch request.
    /// Default: 10
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Whether to use the secure credential store for API keys.
    /// When enabled, API keys can be stored encrypted in the user's application data directory.
    /// Default: false
    /// </summary>
    public bool UseSecureCredentialStore { get; set; } = false;

    /// <summary>
    /// Configuration settings for AI-powered translation providers.
    /// </summary>
    public AIProviderConfiguration? AIProviders { get; set; }
}

/// <summary>
/// API keys for translation providers.
/// Priority order: 1) Environment variables, 2) Secure store (if enabled), 3) These values.
/// </summary>
public class TranslationApiKeys
{
    /// <summary>
    /// Google Cloud Translation API key.
    /// Environment variable: LRM_GOOGLE_API_KEY
    /// Get your key at: https://cloud.google.com/translate/docs/setup
    /// </summary>
    public string? Google { get; set; }

    /// <summary>
    /// DeepL API key.
    /// Environment variable: LRM_DEEPL_API_KEY
    /// Get your key at: https://www.deepl.com/pro-api
    /// </summary>
    public string? DeepL { get; set; }

    /// <summary>
    /// LibreTranslate API key (optional for public instances).
    /// Environment variable: LRM_LIBRETRANSLATE_API_KEY
    /// Learn more at: https://libretranslate.com/
    /// </summary>
    public string? LibreTranslate { get; set; }

    /// <summary>
    /// OpenAI API key for GPT models.
    /// Environment variable: LRM_OPENAI_API_KEY
    /// Get your key at: https://platform.openai.com/api-keys
    /// </summary>
    public string? OpenAI { get; set; }

    /// <summary>
    /// Anthropic Claude API key.
    /// Environment variable: LRM_CLAUDE_API_KEY
    /// Get your key at: https://console.anthropic.com/
    /// </summary>
    public string? Claude { get; set; }

    /// <summary>
    /// Azure OpenAI API key.
    /// Environment variable: LRM_AZUREOPENAI_API_KEY
    /// Get your key from Azure Portal.
    /// </summary>
    public string? AzureOpenAI { get; set; }

    /// <summary>
    /// Azure AI Translator subscription key.
    /// Environment variable: LRM_AZURETRANSLATOR_API_KEY
    /// Get your key from Azure Portal (Cognitive Services - Translator).
    /// </summary>
    public string? AzureTranslator { get; set; }

    /// <summary>
    /// Gets the API key for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name (case-insensitive).</param>
    /// <returns>The API key, or null if not set.</returns>
    public string? GetKeyForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => Google,
            "deepl" => DeepL,
            "libretranslate" => LibreTranslate,
            "openai" => OpenAI,
            "claude" => Claude,
            "azureopenai" => AzureOpenAI,
            "azuretranslator" => AzureTranslator,
            _ => null
        };
    }
}

/// <summary>
/// Configuration settings for code scanning feature.
/// </summary>
public class ScanningConfiguration
{
    /// <summary>
    /// Resource class names to detect in code (e.g., "Resources", "Strings", "AppResources").
    /// These are the class names whose property accesses will be detected as localization key references.
    /// Example: If set to ["Resources", "Strings"], will detect Resources.KeyName and Strings.KeyName.
    /// </summary>
    public List<string>? ResourceClassNames { get; set; }

    /// <summary>
    /// Localization method names to detect (e.g., "GetString", "Translate", "L").
    /// These are method names that accept localization keys as string parameters.
    /// Example: If set to ["GetString", "T"], will detect GetString("KeyName") and T("KeyName").
    /// </summary>
    public List<string>? LocalizationMethods { get; set; }
}

/// <summary>
/// Configuration settings for AI-powered translation providers.
/// Allows customization of models, prompts, endpoints, and other AI-specific settings.
/// </summary>
public class AIProviderConfiguration
{
    /// <summary>
    /// Ollama provider settings (local LLM).
    /// </summary>
    public OllamaSettings? Ollama { get; set; }

    /// <summary>
    /// OpenAI provider settings.
    /// </summary>
    public OpenAISettings? OpenAI { get; set; }

    /// <summary>
    /// Claude provider settings.
    /// </summary>
    public ClaudeSettings? Claude { get; set; }

    /// <summary>
    /// Azure OpenAI provider settings.
    /// </summary>
    public AzureOpenAISettings? AzureOpenAI { get; set; }

    /// <summary>
    /// Azure AI Translator provider settings.
    /// </summary>
    public AzureTranslatorSettings? AzureTranslator { get; set; }

    /// <summary>
    /// Lingva provider settings (free Google Translate proxy).
    /// </summary>
    public LingvaSettings? Lingva { get; set; }

    /// <summary>
    /// MyMemory provider settings (free translation API).
    /// </summary>
    public MyMemorySettings? MyMemory { get; set; }
}

/// <summary>
/// Configuration settings for Ollama provider.
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Ollama API endpoint URL.
    /// Default: http://localhost:11434
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Model to use for translation (e.g., "llama3.2", "mistral", "phi").
    /// Default: llama3.2
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation.
    /// If not set, uses the default prompt.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 10
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for OpenAI provider.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Model to use for translation (e.g., "gpt-4", "gpt-4o-mini", "gpt-3.5-turbo").
    /// Default: gpt-4o-mini
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation.
    /// If not set, uses the default prompt.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 60
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for Claude provider.
/// </summary>
public class ClaudeSettings
{
    /// <summary>
    /// Model to use for translation (e.g., "claude-3-5-sonnet-20241022", "claude-3-opus-20240229").
    /// Default: claude-3-5-sonnet-20241022
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Custom system prompt for translation.
    /// If not set, uses the default prompt.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 50
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for Azure OpenAI provider.
/// </summary>
public class AzureOpenAISettings
{
    /// <summary>
    /// Azure OpenAI endpoint URL (e.g., "https://your-resource.openai.azure.com").
    /// Required for Azure OpenAI.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Deployment name in Azure OpenAI.
    /// Required for Azure OpenAI.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Custom system prompt for translation.
    /// If not set, uses the default prompt.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 60
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for Azure AI Translator provider.
/// </summary>
public class AzureTranslatorSettings
{
    /// <summary>
    /// Azure resource region (e.g., "westus", "eastus").
    /// Optional for global single-service resources.
    /// Required for multi-service or regional resources.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Custom endpoint URL for Azure Translator.
    /// Default: https://api.cognitive.microsofttranslator.com
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 100
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for Lingva provider.
/// Lingva is a free, open-source alternative frontend for Google Translate.
/// </summary>
public class LingvaSettings
{
    /// <summary>
    /// Lingva instance URL (e.g., "https://lingva.ml", "https://translate.plausibility.cloud").
    /// Default: https://lingva.ml
    /// </summary>
    public string? InstanceUrl { get; set; }

    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 30
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for MyMemory provider.
/// MyMemory offers free anonymous translation (5,000 characters/day).
/// </summary>
public class MyMemorySettings
{
    /// <summary>
    /// Rate limit in requests per minute.
    /// Default: 20 (conservative for free tier)
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
}

/// <summary>
/// Configuration settings for resource validation.
/// </summary>
public class ValidationConfiguration
{
    /// <summary>
    /// Placeholder types to validate.
    /// Supported values: "dotnet", "printf", "icu", "template", "all".
    /// Multiple types can be combined (e.g., ["dotnet", "printf"]).
    /// Default: ["dotnet"]
    /// </summary>
    public List<string>? PlaceholderTypes { get; set; }

    /// <summary>
    /// Whether to enable placeholder validation.
    /// Default: true
    /// </summary>
    public bool EnablePlaceholderValidation { get; set; } = true;
}

/// <summary>
/// Configuration settings for web server.
/// </summary>
public class WebConfiguration
{
    /// <summary>
    /// Port to bind the web server to.
    /// Environment variable: LRM_WEB_PORT
    /// Default: 5000
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Address to bind the web server to (e.g., "localhost", "0.0.0.0", "*").
    /// Environment variable: LRM_WEB_BIND_ADDRESS
    /// Default: localhost
    /// </summary>
    public string? BindAddress { get; set; }

    /// <summary>
    /// Whether to automatically open browser on startup.
    /// Environment variable: LRM_WEB_AUTO_OPEN_BROWSER (true/false)
    /// Default: true
    /// </summary>
    public bool? AutoOpenBrowser { get; set; }

    /// <summary>
    /// Whether to enable HTTPS.
    /// Environment variable: LRM_WEB_HTTPS_ENABLED (true/false)
    /// Default: false
    /// </summary>
    public bool? EnableHttps { get; set; }

    /// <summary>
    /// Path to HTTPS certificate file (.pfx).
    /// Environment variable: LRM_WEB_HTTPS_CERT_PATH
    /// Required if EnableHttps is true.
    /// </summary>
    public string? HttpsCertificatePath { get; set; }

    /// <summary>
    /// Password for HTTPS certificate.
    /// Environment variable: LRM_WEB_HTTPS_CERT_PASSWORD
    /// Required if EnableHttps is true and certificate is password-protected.
    /// </summary>
    public string? HttpsCertificatePassword { get; set; }

    /// <summary>
    /// CORS configuration for API access.
    /// </summary>
    public CorsConfiguration? Cors { get; set; }
}

/// <summary>
/// CORS configuration for web server.
/// </summary>
public class CorsConfiguration
{
    /// <summary>
    /// Whether to enable CORS.
    /// Default: false
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Allowed origins for CORS (e.g., ["http://localhost:3000", "https://example.com"]).
    /// Use ["*"] to allow all origins (not recommended for production).
    /// Default: empty list
    /// </summary>
    public List<string>? AllowedOrigins { get; set; }

    /// <summary>
    /// Whether to allow credentials in CORS requests.
    /// Default: false
    /// </summary>
    public bool AllowCredentials { get; set; } = false;
}

/// <summary>
/// Configuration settings for JSON resource format.
/// </summary>
public class JsonFormatConfiguration
{
    /// <summary>
    /// Use nested key structure (e.g., Errors.NotFound becomes {"Errors": {"NotFound": "..."}}).
    /// If false, keys are stored flat with dot notation ("Errors.NotFound": "...").
    /// Default: true
    /// </summary>
    public bool UseNestedKeys { get; set; } = true;

    /// <summary>
    /// Include _meta section in JSON files with language info and last modified date.
    /// Default: true
    /// </summary>
    public bool IncludeMeta { get; set; } = true;

    /// <summary>
    /// Preserve comments as _comment properties in JSON files.
    /// Default: true
    /// </summary>
    public bool PreserveComments { get; set; } = true;

    /// <summary>
    /// Base filename for resources (without extension or culture code).
    /// Example: "strings" produces "strings.json", "strings.fr.json", etc.
    /// Default: "strings"
    /// </summary>
    public string BaseName { get; set; } = "strings";

    /// <summary>
    /// Interpolation format for variable placeholders.
    /// Supported values: "dotnet" ({0}, {1}), "i18next" ({{name}}), "icu" ({name}).
    /// Default: "dotnet"
    /// </summary>
    public string InterpolationFormat { get; set; } = "dotnet";

    /// <summary>
    /// Plural key suffix format.
    /// Supported values: "cldr" (i18next-style: _zero, _one, _two, _few, _many, _other),
    ///                   "simple" (_singular, _plural).
    /// Default: "cldr"
    /// </summary>
    public string PluralFormat { get; set; } = "cldr";

    /// <summary>
    /// Enable full i18next compatibility mode.
    /// When true, uses i18next interpolation ({{var}}), CLDR plural suffixes,
    /// and i18next-style nesting ($t(key)).
    /// Overrides InterpolationFormat and PluralFormat settings.
    /// Default: false
    /// </summary>
    public bool I18nextCompatible { get; set; } = false;
}

/// <summary>
/// Configuration for cloud remotes stored in .lrm/remotes.json
/// Separates deployment/cloud settings from project configuration.
/// </summary>
public class RemotesConfiguration
{
    /// <summary>
    /// The active remote URL for cloud synchronization.
    /// </summary>
    public string? Remote { get; set; }

    /// <summary>
    /// Whether cloud synchronization is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Configuration settings for synchronization behavior.
/// </summary>
public class SyncConfiguration
{
    /// <summary>
    /// Synchronization mode: "manual" (explicit push/pull) or "auto" (watch for changes).
    /// Default: "manual"
    /// </summary>
    public string Mode { get; set; } = "manual";

    /// <summary>
    /// File paths to include in synchronization (glob patterns).
    /// Example: ["Resources/**/*.resx", "locales/**/*.json"]
    /// Default: ["Resources/**/*"]
    /// </summary>
    public List<string> Paths { get; set; } = new() { "Resources/**/*" };

    /// <summary>
    /// File patterns to exclude from synchronization.
    /// Example: ["**/*.g.resx", "**/node_modules/**", "**/*.Designer.cs"]
    /// Default: ["**/*.g.resx", "**/node_modules/**"]
    /// </summary>
    public List<string> Exclude { get; set; } = new() { "**/*.g.resx", "**/node_modules/**" };

    /// <summary>
    /// Whether to automatically translate missing keys during sync.
    /// When enabled, missing translations will be auto-translated using the configured provider.
    /// Default: false
    /// </summary>
    public bool AutoTranslate { get; set; } = false;

    /// <summary>
    /// Whether to automatically create pull requests for synced changes.
    /// When enabled, push operations create PRs instead of direct commits.
    /// Default: true
    /// </summary>
    public bool CreatePr { get; set; } = true;
}
