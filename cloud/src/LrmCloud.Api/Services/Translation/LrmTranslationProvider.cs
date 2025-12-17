using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// LRM managed translation provider.
/// Acts as a meta-provider that selects from configured backends.
/// Usage counts against user's plan limits.
/// </summary>
public interface ILrmTranslationProvider
{
    /// <summary>
    /// Translate text using LRM managed backends.
    /// Note: Usage tracking is handled by CloudTranslationService, not here.
    /// </summary>
    /// <param name="billableUserId">The user whose quota to check (org owner for org projects).</param>
    /// <param name="sourceText">Text to translate.</param>
    /// <param name="sourceLanguage">Source language code.</param>
    /// <param name="targetLanguage">Target language code.</param>
    /// <param name="context">Optional context for AI providers.</param>
    Task<LrmTranslationResult> TranslateAsync(
        int billableUserId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? context = null);

    /// <summary>
    /// Check if LRM provider is available (enabled and user has chars remaining).
    /// </summary>
    /// <param name="billableUserId">The user whose quota to check (org owner for org projects).</param>
    Task<(bool Available, string? Reason)> IsAvailableAsync(int billableUserId);

    /// <summary>
    /// Check if user has sufficient LRM chars for the given text.
    /// </summary>
    /// <param name="billableUserId">The user whose quota to check (org owner for org projects).</param>
    Task<bool> HasSufficientCharsAsync(int billableUserId, int charCount);

    /// <summary>
    /// Get the remaining LRM chars for a user.
    /// </summary>
    /// <param name="billableUserId">The user whose quota to check (org owner for org projects).</param>
    Task<int> GetRemainingCharsAsync(int billableUserId);
}

public class LrmTranslationResult
{
    public bool Success { get; set; }
    public string? TranslatedText { get; set; }
    public string? Error { get; set; }
    public int CharsUsed { get; set; }
    public bool FromCache { get; set; }
}

public class LrmTranslationProvider : ILrmTranslationProvider
{
    private readonly AppDbContext _db;
    private readonly CloudConfiguration _config;
    private readonly ILogger<LrmTranslationProvider> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Round-robin state (thread-safe)
    private int _roundRobinIndex;
    private readonly object _roundRobinLock = new();

    public LrmTranslationProvider(
        AppDbContext db,
        CloudConfiguration config,
        ILogger<LrmTranslationProvider> logger,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _serviceProvider = serviceProvider;

        ValidateBackendConfiguration();
    }

    /// <summary>
    /// Validate that enabled backends have required configuration.
    /// Logs warnings for misconfigured backends on startup.
    /// </summary>
    private void ValidateBackendConfiguration()
    {
        if (!_config.LrmProvider.Enabled)
        {
            _logger.LogInformation("LRM provider is disabled");
            return;
        }

        var backends = _config.LrmProvider.Backends;
        var enabled = _config.LrmProvider.EnabledBackends;

        if (!enabled.Any())
        {
            _logger.LogWarning("LRM provider is enabled but no backends are configured in EnabledBackends");
            return;
        }

        foreach (var backend in enabled)
        {
            var (isConfigured, issue) = backend.ToLowerInvariant() switch
            {
                "mymemory" => (true, null),  // Free, always works
                "lingva" => (true, null),    // Free, always works
                "deepl" => (!string.IsNullOrEmpty(backends.DeepL?.ApiKey), "missing ApiKey"),
                "google" => (!string.IsNullOrEmpty(backends.Google?.ApiKey), "missing ApiKey"),
                "openai" => (!string.IsNullOrEmpty(backends.OpenAI?.ApiKey), "missing ApiKey"),
                "claude" => (!string.IsNullOrEmpty(backends.Claude?.ApiKey), "missing ApiKey"),
                "azureopenai" => (
                    !string.IsNullOrEmpty(backends.AzureOpenAI?.ApiKey) && !string.IsNullOrEmpty(backends.AzureOpenAI?.Endpoint),
                    "missing ApiKey or Endpoint"),
                "azuretranslator" => (!string.IsNullOrEmpty(backends.AzureTranslator?.ApiKey), "missing ApiKey"),
                "libretranslate" => (true, null),  // API key optional
                "ollama" => (true, null),  // Local, no key needed
                _ => (false, "unknown backend")
            };

            if (!isConfigured)
            {
                _logger.LogWarning(
                    "LRM backend '{Backend}' is enabled but not properly configured: {Issue}. " +
                    "Configure it in LrmProvider.Backends section of config.json",
                    backend, issue);
            }
            else
            {
                _logger.LogDebug("LRM backend '{Backend}' is configured and ready", backend);
            }
        }

        _logger.LogInformation(
            "LRM provider initialized with {Count} backend(s): [{Backends}]",
            enabled.Count, string.Join(", ", enabled));
    }

    public async Task<LrmTranslationResult> TranslateAsync(
        int billableUserId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? context = null)
    {
        var result = new LrmTranslationResult { CharsUsed = sourceText.Length };

        // Check if LRM is enabled
        if (!_config.LrmProvider.Enabled)
        {
            result.Error = "LRM translation is disabled";
            return result;
        }

        // Check billable user's remaining chars
        var remaining = await GetRemainingCharsAsync(billableUserId);
        if (remaining < sourceText.Length)
        {
            result.Error = $"Insufficient LRM translation quota. Need {sourceText.Length} chars, have {remaining} remaining.";
            return result;
        }

        // Get backends to try (in order based on strategy)
        var backends = GetBackendsForFailover();
        if (!backends.Any())
        {
            result.Error = "No LRM backend providers configured";
            return result;
        }

        var request = new TranslationRequest
        {
            SourceText = sourceText,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Context = context
        };

        var errors = new List<string>();

        // Try each backend in order until one succeeds
        foreach (var backend in backends)
        {
            try
            {
                // Create and call the actual backend provider
                var provider = CreateBackendProvider(backend);
                if (provider == null)
                {
                    errors.Add($"{backend}: Failed to initialize");
                    continue;
                }

                var response = await provider.TranslateAsync(request);

                result.Success = true;
                result.TranslatedText = response.TranslatedText;
                result.FromCache = response.FromCache;

                // Note: Usage tracking is handled by CloudTranslationService, not here
                _logger.LogDebug(
                    "LRM translation via {Backend}: {Chars} chars (billable to user {BillableUserId})",
                    backend, sourceText.Length, billableUserId);

                return result; // Success - return immediately
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LRM backend {Backend} failed (billable user {BillableUserId}), trying next", backend, billableUserId);
                errors.Add($"{backend}: {ex.Message}");
                // Continue to next backend
            }
        }

        // All backends failed
        result.Error = $"All LRM backends failed: {string.Join("; ", errors)}";
        _logger.LogError("All LRM backends failed (billable user {BillableUserId}): {Errors}", billableUserId, result.Error);

        return result;
    }

    public async Task<(bool Available, string? Reason)> IsAvailableAsync(int userId)
    {
        if (!_config.LrmProvider.Enabled)
        {
            return (false, "LRM translation is disabled");
        }

        if (!_config.LrmProvider.EnabledBackends.Any())
        {
            return (false, "No LRM backends configured");
        }

        var remaining = await GetRemainingCharsAsync(userId);
        if (remaining <= 0)
        {
            return (false, "LRM translation quota exhausted");
        }

        return (true, null);
    }

    public async Task<bool> HasSufficientCharsAsync(int userId, int charCount)
    {
        var remaining = await GetRemainingCharsAsync(userId);
        return remaining >= charCount;
    }

    public async Task<int> GetRemainingCharsAsync(int userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.TranslationCharsUsed, u.TranslationCharsLimit })
            .FirstOrDefaultAsync();

        if (user == null)
            return 0;

        return Math.Max(0, user.TranslationCharsLimit - user.TranslationCharsUsed);
    }

    /// <summary>
    /// Get backends ordered for failover attempts.
    /// Uses round-robin for initial selection, then continues with remaining backends.
    /// </summary>
    private List<string> GetBackendsForFailover()
    {
        var backends = _config.LrmProvider.EnabledBackends;
        if (!backends.Any())
            return new List<string>();

        if (_config.LrmProvider.SelectionStrategy == "roundrobin")
        {
            // Round-robin: start with next backend, then cycle through all
            lock (_roundRobinLock)
            {
                var startIndex = _roundRobinIndex % backends.Count;
                _roundRobinIndex++;

                // Create ordered list starting from round-robin index
                var ordered = new List<string>(backends.Count);
                for (int i = 0; i < backends.Count; i++)
                {
                    ordered.Add(backends[(startIndex + i) % backends.Count]);
                }
                return ordered;
            }
        }

        // Default: priority order (as configured)
        return backends.ToList();
    }

    private string? SelectBackend()
    {
        var backends = GetBackendsForFailover();
        return backends.FirstOrDefault();
    }

    private ITranslationProvider? CreateBackendProvider(string backend)
    {
        try
        {
            // For free providers (mymemory, lingva), no API key needed
            var config = new ConfigurationModel
            {
                Translation = new TranslationConfiguration
                {
                    DefaultProvider = backend
                }
            };

            // Add any platform-level API keys here if we have them
            // For now, we rely on free providers or self-hosted ones
            ApplyPlatformConfig(config, backend);

            return TranslationProviderFactory.Create(backend, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create LRM backend provider: {Backend}", backend);
            return null;
        }
    }

    private void ApplyPlatformConfig(ConfigurationModel config, string backend)
    {
        config.Translation ??= new TranslationConfiguration();
        config.Translation.ApiKeys ??= new TranslationApiKeys();
        config.Translation.AIProviders ??= new AIProviderConfiguration();

        var backends = _config.LrmProvider.Backends;

        switch (backend.ToLowerInvariant())
        {
            case "mymemory":
                var mm = backends.MyMemory ?? new LrmMyMemoryConfig();
                config.Translation.AIProviders.MyMemory = new MyMemorySettings
                {
                    RateLimitPerMinute = mm.RateLimitPerMinute
                };
                break;

            case "lingva":
                var lingva = backends.Lingva ?? new LrmLingvaConfig();
                config.Translation.AIProviders.Lingva = new LingvaSettings
                {
                    InstanceUrl = lingva.InstanceUrl,
                    RateLimitPerMinute = lingva.RateLimitPerMinute
                };
                break;

            case "deepl":
                var deepl = backends.DeepL;
                if (string.IsNullOrEmpty(deepl?.ApiKey))
                {
                    _logger.LogWarning("DeepL backend enabled but no API key configured in LrmProvider.Backends.DeepL");
                    break;
                }
                config.Translation.ApiKeys.DeepL = deepl.ApiKey;
                break;

            case "google":
                var google = backends.Google;
                if (string.IsNullOrEmpty(google?.ApiKey))
                {
                    _logger.LogWarning("Google backend enabled but no API key configured in LrmProvider.Backends.Google");
                    break;
                }
                config.Translation.ApiKeys.Google = google.ApiKey;
                break;

            case "openai":
                var openai = backends.OpenAI;
                if (string.IsNullOrEmpty(openai?.ApiKey))
                {
                    _logger.LogWarning("OpenAI backend enabled but no API key configured in LrmProvider.Backends.OpenAI");
                    break;
                }
                config.Translation.ApiKeys.OpenAI = openai.ApiKey;
                config.Translation.AIProviders.OpenAI = new OpenAISettings
                {
                    Model = openai.Model,
                    CustomSystemPrompt = openai.CustomSystemPrompt,
                    RateLimitPerMinute = openai.RateLimitPerMinute
                };
                break;

            case "claude":
                var claude = backends.Claude;
                if (string.IsNullOrEmpty(claude?.ApiKey))
                {
                    _logger.LogWarning("Claude backend enabled but no API key configured in LrmProvider.Backends.Claude");
                    break;
                }
                config.Translation.ApiKeys.Claude = claude.ApiKey;
                config.Translation.AIProviders.Claude = new ClaudeSettings
                {
                    Model = claude.Model,
                    CustomSystemPrompt = claude.CustomSystemPrompt,
                    RateLimitPerMinute = claude.RateLimitPerMinute
                };
                break;

            case "azureopenai":
                var azureOai = backends.AzureOpenAI;
                if (string.IsNullOrEmpty(azureOai?.ApiKey) || string.IsNullOrEmpty(azureOai?.Endpoint))
                {
                    _logger.LogWarning("AzureOpenAI backend enabled but missing ApiKey or Endpoint in LrmProvider.Backends.AzureOpenAI");
                    break;
                }
                config.Translation.ApiKeys.AzureOpenAI = azureOai.ApiKey;
                config.Translation.AIProviders.AzureOpenAI = new AzureOpenAISettings
                {
                    Endpoint = azureOai.Endpoint,
                    DeploymentName = azureOai.DeploymentName,
                    CustomSystemPrompt = azureOai.CustomSystemPrompt,
                    RateLimitPerMinute = azureOai.RateLimitPerMinute
                };
                break;

            case "azuretranslator":
                var azureTr = backends.AzureTranslator;
                if (string.IsNullOrEmpty(azureTr?.ApiKey))
                {
                    _logger.LogWarning("AzureTranslator backend enabled but no API key configured in LrmProvider.Backends.AzureTranslator");
                    break;
                }
                config.Translation.ApiKeys.AzureTranslator = azureTr.ApiKey;
                config.Translation.AIProviders.AzureTranslator = new AzureTranslatorSettings
                {
                    Region = azureTr.Region,
                    Endpoint = azureTr.Endpoint,
                    RateLimitPerMinute = azureTr.RateLimitPerMinute
                };
                break;

            case "libretranslate":
                var libre = backends.LibreTranslate ?? new LrmLibreTranslateConfig();
                config.Translation.ApiKeys.LibreTranslate = libre.ApiKey;
                break;

            case "ollama":
                var ollama = backends.Ollama ?? new LrmOllamaConfig();
                config.Translation.AIProviders.Ollama = new OllamaSettings
                {
                    ApiUrl = ollama.ApiUrl,
                    Model = ollama.Model,
                    CustomSystemPrompt = ollama.CustomSystemPrompt,
                    RateLimitPerMinute = ollama.RateLimitPerMinute
                };
                break;

            default:
                _logger.LogWarning("Unknown LRM backend: {Backend}", backend);
                break;
        }
    }

}
