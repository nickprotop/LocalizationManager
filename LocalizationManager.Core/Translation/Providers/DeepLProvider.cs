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

            // Translate
            var result = await _translator.TranslateTextAsync(
                request.SourceText,
                request.SourceLanguage,
                request.TargetLanguage,
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
}
