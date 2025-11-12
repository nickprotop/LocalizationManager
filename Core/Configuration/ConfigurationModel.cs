namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Represents the configuration model for LocalizationManager.
/// This class will be expanded in the future to include various configuration options.
/// </summary>
public class ConfigurationModel
{
    /// <summary>
    /// The language code to display for the default language (e.g., "en", "fr").
    /// If not set, displays "default". Only affects display output, not internal logic.
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
}

/// <summary>
/// Configuration settings for the machine translation feature.
/// </summary>
public class TranslationConfiguration
{
    /// <summary>
    /// The default translation provider to use when not explicitly specified.
    /// Supported values: "google", "deepl", "libretranslate".
    /// Default: "google"
    /// </summary>
    public string DefaultProvider { get; set; } = "google";

    /// <summary>
    /// The default source language for translation.
    /// If not set, the provider will attempt to auto-detect the source language.
    /// </summary>
    public string? DefaultSourceLanguage { get; set; }

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
