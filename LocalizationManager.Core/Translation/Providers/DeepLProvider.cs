// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeepL;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using DeepL API.
/// </summary>
public class DeepLProvider : ITranslationProvider
{
    private readonly string? _apiKey;
    private readonly Translator? _translator;
    private readonly RateLimiter? _rateLimiter;

    /// <summary>
    /// Creates a new DeepL provider.
    /// </summary>
    /// <param name="apiKey">The DeepL API key.</param>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 60 requests/min for free tier).</param>
    public DeepLProvider(string? apiKey, int rateLimitRequestsPerMinute = 60)
    {
        _apiKey = apiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _translator = new Translator(apiKey);
            _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
        }
    }

    /// <inheritdoc />
    public string Name => "deepl";

    /// <inheritdoc />
    public bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey);

    /// <inheritdoc />
    public int? GetRateLimit() => 60; // DeepL free tier: 60 requests/min (approximation)

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_translator == null || _rateLimiter == null)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "DeepL API key is not configured.",
                Name,
                isRetryable: false);
        }

        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // Normalize language codes for DeepL API
            // DeepL expects uppercase 2-letter codes (e.g., "EL" not "el_GR")
            // Special cases: EN-US, EN-GB, PT-BR, PT-PT, etc. use hyphens
            var sourceLanguage = NormalizeLanguageCode(request.SourceLanguage);
            var targetLanguage = NormalizeLanguageCode(request.TargetLanguage);

            // Translate
            var result = await _translator.TranslateTextAsync(
                request.SourceText,
                sourceLanguage,
                targetLanguage,
                cancellationToken: cancellationToken);

            return new TranslationResponse
            {
                TranslatedText = result.Text,
                DetectedSourceLanguage = result.DetectedSourceLanguageCode,
                Provider = Name,
                FromCache = false
            };
        }
        catch (AuthorizationException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Invalid DeepL API key or authorization failed.",
                Name,
                isRetryable: false,
                ex);
        }
        catch (QuotaExceededException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.QuotaExceeded,
                "DeepL quota exceeded. Consider upgrading your plan.",
                Name,
                isRetryable: false,
                ex);
        }
        catch (TooManyRequestsException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.RateLimitExceeded,
                "DeepL rate limit exceeded. Please try again later.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.NetworkError,
                "Network error occurred while contacting DeepL API.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "DeepL translation request timed out.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (DeepLException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Unknown,
                $"DeepL API error: {ex.Message}",
                Name,
                isRetryable: false,
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranslationResponse>> TranslateBatchAsync(
        IEnumerable<TranslationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToList();
        var responses = new List<TranslationResponse>();

        // DeepL SDK doesn't support batch translation in the same way
        // So we translate one by one (could be optimized by grouping by language pair)
        foreach (var request in requestList)
        {
            var response = await TranslateAsync(request, cancellationToken);
            responses.Add(response);
        }

        return responses;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_translator == null)
        {
            return null;
        }

        try
        {
            var languages = await _translator.GetSourceLanguagesAsync(cancellationToken);
            return languages.Select(l => l.Code).ToList();
        }
        catch
        {
            // If fetching languages fails, return null (unknown)
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_translator == null)
        {
            return null;
        }

        try
        {
            var languages = await _translator.GetTargetLanguagesAsync(cancellationToken);
            return languages.Select(l => l.Code).ToList();
        }
        catch
        {
            // If fetching languages fails, return null (unknown)
            return null;
        }
    }

    /// <summary>
    /// Normalizes language codes for DeepL API.
    /// DeepL expects uppercase 2-letter ISO 639-1 codes (e.g., "EL", "FR").
    /// For regional variants, it uses hyphens (e.g., "EN-US", "PT-BR").
    /// </summary>
    /// <param name="languageCode">Language code in any format (e.g., "el_GR", "en-US", "fr").</param>
    /// <returns>Normalized language code for DeepL API, or null for auto-detection.</returns>
    private static string? NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null; // Auto-detect
        }

        // Replace underscores with hyphens (e.g., "el_GR" → "el-GR")
        languageCode = languageCode.Replace('_', '-');

        // DeepL supports these regional variants with hyphens
        var supportedRegionalVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EN-US", "EN-GB",
            "PT-BR", "PT-PT",
            "ES-ES", "ES-MX"
        };

        // Check if it's a supported regional variant
        if (supportedRegionalVariants.Contains(languageCode))
        {
            return languageCode.ToUpperInvariant();
        }

        // For all other cases, take only the base language code (first 2 chars)
        // and convert to uppercase (e.g., "el-GR" → "EL", "fr" → "FR")
        var baseCode = languageCode.Length >= 2
            ? languageCode.Substring(0, 2).ToUpperInvariant()
            : languageCode.ToUpperInvariant();

        return baseCode;
    }
}
