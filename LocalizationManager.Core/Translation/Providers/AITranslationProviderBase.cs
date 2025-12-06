using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Abstract base class for AI-powered translation providers (LLMs).
/// Provides common functionality for OpenAI, Claude, Azure OpenAI, Ollama, etc.
/// </summary>
public abstract class AITranslationProviderBase : ITranslationProvider
{
    protected readonly RateLimiter? RateLimiter;
    protected readonly int? RateLimitRequestsPerMinute;
    protected readonly string? Model;
    protected readonly string? CustomSystemPrompt;

    /// <summary>
    /// Default system prompt for translation tasks.
    /// Can be overridden by CustomSystemPrompt in configuration.
    /// </summary>
    protected virtual string DefaultSystemPrompt =>
        "You are a professional translator. Your task is to translate text accurately while preserving " +
        "the original meaning, tone, and style. Return ONLY the translated text without any explanations, " +
        "notes, or additional commentary.";

    /// <summary>
    /// Gets the system prompt to use for translations.
    /// Returns custom prompt if provided, otherwise returns default.
    /// </summary>
    protected string SystemPrompt => CustomSystemPrompt ?? DefaultSystemPrompt;

    protected AITranslationProviderBase(
        string? model = null,
        string? customSystemPrompt = null,
        int rateLimitRequestsPerMinute = 10)
    {
        Model = model;
        CustomSystemPrompt = customSystemPrompt;
        RateLimitRequestsPerMinute = rateLimitRequestsPerMinute;

        if (rateLimitRequestsPerMinute > 0)
        {
            RateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
        }
    }

    public abstract string Name { get; }

    public abstract bool IsConfigured();

    public abstract Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Default batch implementation that processes requests sequentially.
    /// Derived classes can override for parallel processing if needed.
    /// </summary>
    public virtual async Task<IReadOnlyList<TranslationResponse>> TranslateBatchAsync(
        IEnumerable<TranslationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TranslationResponse>();
        foreach (var request in requests)
        {
            var response = await TranslateAsync(request, cancellationToken);
            results.Add(response);
        }
        return results;
    }

    public virtual int? GetRateLimit() => RateLimitRequestsPerMinute;

    /// <summary>
    /// Most AI services support a wide range of languages.
    /// Returns null to indicate "check with the service" or override to provide specific list.
    /// </summary>
    public virtual Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    /// <summary>
    /// Most AI services support a wide range of languages.
    /// Returns null to indicate "check with the service" or override to provide specific list.
    /// </summary>
    public virtual Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    /// <summary>
    /// Builds the user prompt for translation request.
    /// Can be overridden by derived classes for provider-specific formatting.
    /// </summary>
    protected virtual string BuildUserPrompt(TranslationRequest request)
    {
        // Use language names if provided, otherwise fall back to codes or CultureInfo
        var sourceLang = request.SourceLanguageName
            ?? (request.SourceLanguage != null ? GetLanguageDisplayName(request.SourceLanguage) : "auto-detected language");
        var targetLang = request.TargetLanguageName
            ?? GetLanguageDisplayName(request.TargetLanguage);

        var prompt = $"Translate the following text from {sourceLang} to {targetLang}:\n\n{request.SourceText}";

        // Add context if provided
        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            prompt = $"Context: {request.Context}\n\n{prompt}";
        }

        return prompt;
    }

    /// <summary>
    /// Gets a display name for a language code using CultureInfo.
    /// Falls back to the code itself if not found.
    /// </summary>
    protected virtual string GetLanguageDisplayName(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            return culture.EnglishName;
        }
        catch
        {
            // If culture not found, return the code itself
            return languageCode;
        }
    }

    /// <summary>
    /// Cleans up the AI response by removing common artifacts.
    /// </summary>
    protected virtual string CleanTranslationResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return response;
        }

        var cleaned = response.Trim();

        // Remove common prefixes that LLMs might add
        var prefixes = new[]
        {
            "Translation:",
            "Translated text:",
            "Here is the translation:",
            "The translation is:",
        };

        foreach (var prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(prefix.Length).Trim();
                break;
            }
        }

        // Remove quotes if the entire response is quoted
        if ((cleaned.StartsWith("\"") && cleaned.EndsWith("\"")) ||
            (cleaned.StartsWith("'") && cleaned.EndsWith("'")))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
        }

        return cleaned;
    }

    /// <summary>
    /// Maps common AI service errors to TranslationException.
    /// </summary>
    protected TranslationException CreateTranslationException(
        Exception ex,
        string operation,
        TranslationErrorCode? errorCode = null)
    {
        var code = errorCode ?? TranslationErrorCode.Unknown;
        var isRetryable = false;

        // Try to determine error code from exception message
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("rate limit") || message.Contains("too many requests"))
        {
            code = TranslationErrorCode.RateLimitExceeded;
            isRetryable = true;
        }
        else if (message.Contains("unauthorized") || message.Contains("invalid api key") ||
                 message.Contains("authentication"))
        {
            code = TranslationErrorCode.InvalidApiKey;
            isRetryable = false;
        }
        else if (message.Contains("timeout") || message.Contains("timed out"))
        {
            code = TranslationErrorCode.Timeout;
            isRetryable = true;
        }
        else if (message.Contains("quota") || message.Contains("exceeded"))
        {
            code = TranslationErrorCode.QuotaExceeded;
            isRetryable = false;
        }
        else if (message.Contains("network") || message.Contains("connection"))
        {
            code = TranslationErrorCode.NetworkError;
            isRetryable = true;
        }
        else if (message.Contains("unavailable") || message.Contains("service"))
        {
            code = TranslationErrorCode.ServiceUnavailable;
            isRetryable = true;
        }

        return new TranslationException(
            code,
            $"{Name} translation failed during {operation}: {ex.Message}",
            Name,
            isRetryable,
            ex);
    }
}
