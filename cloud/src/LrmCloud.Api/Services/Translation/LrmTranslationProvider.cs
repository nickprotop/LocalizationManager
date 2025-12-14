using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    /// </summary>
    Task<LrmTranslationResult> TranslateAsync(
        int userId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? context = null);

    /// <summary>
    /// Check if LRM provider is available (enabled and user has chars remaining).
    /// </summary>
    Task<(bool Available, string? Reason)> IsAvailableAsync(int userId);

    /// <summary>
    /// Check if user has sufficient LRM chars for the given text.
    /// </summary>
    Task<bool> HasSufficientCharsAsync(int userId, int charCount);

    /// <summary>
    /// Get the remaining LRM chars for a user.
    /// </summary>
    Task<int> GetRemainingCharsAsync(int userId);
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
        IOptions<CloudConfiguration> config,
        ILogger<LrmTranslationProvider> logger,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _config = config.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<LrmTranslationResult> TranslateAsync(
        int userId,
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

        // Check user's remaining chars
        var remaining = await GetRemainingCharsAsync(userId);
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

                // Track usage (decrement user's char balance)
                await TrackLrmUsageAsync(userId, sourceText.Length);

                _logger.LogDebug(
                    "LRM translation via {Backend}: {Chars} chars for user {UserId}",
                    backend, sourceText.Length, userId);

                return result; // Success - return immediately
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LRM backend {Backend} failed, user {UserId}, trying next", backend, userId);
                errors.Add($"{backend}: {ex.Message}");
                // Continue to next backend
            }
        }

        // All backends failed
        result.Error = $"All LRM backends failed: {string.Join("; ", errors)}";
        _logger.LogError("All LRM backends failed for user {UserId}: {Errors}", userId, result.Error);

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

        // Platform API keys from master secret (if configured)
        // These are the keys WE own for the LRM service
        var masterSecret = _config.ApiKeyMasterSecret;

        switch (backend.ToLowerInvariant())
        {
            case "lingva":
                // Lingva is free, no API key needed
                config.Translation.AIProviders.Lingva = new LingvaSettings();
                break;

            case "mymemory":
                // MyMemory is free, no API key needed
                config.Translation.AIProviders.MyMemory = new MyMemorySettings();
                break;

            // Future: Add platform-owned API keys for paid backends
            // case "deepl":
            //     config.Translation.ApiKeys.DeepL = GetPlatformApiKey("deepl");
            //     break;
        }
    }

    private async Task TrackLrmUsageAsync(int userId, int charsUsed)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        user.TranslationCharsUsed += charsUsed;
        user.UpdatedAt = DateTime.UtcNow;

        // Reset usage if reset date has passed
        if (user.TranslationCharsResetAt.HasValue && user.TranslationCharsResetAt < DateTime.UtcNow)
        {
            user.TranslationCharsUsed = charsUsed; // Reset and add current
            user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
        }
        else if (!user.TranslationCharsResetAt.HasValue)
        {
            // Set initial reset date
            user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
        }

        await _db.SaveChangesAsync();
    }
}
