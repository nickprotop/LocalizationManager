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

        // Select backend provider
        var backend = SelectBackend();
        if (string.IsNullOrEmpty(backend))
        {
            result.Error = "No LRM backend providers configured";
            return result;
        }

        try
        {
            // Create and call the actual backend provider
            var provider = CreateBackendProvider(backend);
            if (provider == null)
            {
                result.Error = $"Failed to initialize LRM backend: {backend}";
                return result;
            }

            var request = new TranslationRequest
            {
                SourceText = sourceText,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Context = context
            };

            var response = await provider.TranslateAsync(request);

            result.Success = true;
            result.TranslatedText = response.TranslatedText;
            result.FromCache = response.FromCache;

            // Track usage (decrement user's char balance)
            await TrackLrmUsageAsync(userId, sourceText.Length);

            _logger.LogDebug(
                "LRM translation via {Backend}: {Chars} chars for user {UserId}",
                backend, sourceText.Length, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LRM backend {Backend} failed, user {UserId}", backend, userId);
            result.Error = $"Translation failed: {ex.Message}";

            // TODO: Try next backend on failure (failover)
        }

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

    private string? SelectBackend()
    {
        var backends = _config.LrmProvider.EnabledBackends;
        if (!backends.Any())
            return null;

        if (_config.LrmProvider.SelectionStrategy == "roundrobin")
        {
            lock (_roundRobinLock)
            {
                var index = _roundRobinIndex % backends.Count;
                _roundRobinIndex++;
                return backends[index];
            }
        }

        // Default: priority (first available)
        return backends.First();
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
