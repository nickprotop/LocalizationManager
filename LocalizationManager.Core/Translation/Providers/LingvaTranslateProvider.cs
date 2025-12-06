// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using Lingva Translate API.
/// Lingva is an alternative frontend for Google Translate that doesn't require an API key.
/// </summary>
public class LingvaTranslateProvider : ITranslationProvider
{
    private readonly string _instanceUrl;
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Creates a new Lingva Translate provider.
    /// </summary>
    /// <param name="instanceUrl">The Lingva instance URL (default: https://lingva.ml).</param>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 30 requests/min).</param>
    public LingvaTranslateProvider(
        string? instanceUrl = null,
        int rateLimitRequestsPerMinute = 30)
    {
        _instanceUrl = (instanceUrl ?? "https://lingva.ml").TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_instanceUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
    }

    /// <inheritdoc />
    public string Name => "lingva";

    /// <inheritdoc />
    public bool IsConfigured() => true; // Lingva doesn't require an API key

    /// <inheritdoc />
    public int? GetRateLimit() => 30;

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // Lingva API format: /api/v1/{source}/{target}/{query}
            var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "auto" : request.SourceLanguage;
            var targetLang = request.TargetLanguage;
            var encodedText = Uri.EscapeDataString(request.SourceText);

            var requestUrl = $"/api/v1/{sourceLang}/{targetLang}/{encodedText}";

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TranslationException(
                    response.StatusCode == HttpStatusCode.TooManyRequests
                        ? TranslationErrorCode.RateLimitExceeded
                        : TranslationErrorCode.Unknown,
                    $"Lingva API error: {response.StatusCode} - {errorContent}",
                    Name,
                    isRetryable: response.StatusCode == HttpStatusCode.TooManyRequests ||
                                 response.StatusCode == HttpStatusCode.ServiceUnavailable);
            }

            var result = await response.Content.ReadFromJsonAsync<LingvaResponse>(cancellationToken);

            if (result == null)
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "Lingva returned empty response.",
                    Name,
                    isRetryable: false);
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new TranslationException(
                    TranslationErrorCode.Unknown,
                    $"Lingva error: {result.Error}",
                    Name,
                    isRetryable: false);
            }

            if (string.IsNullOrEmpty(result.Translation))
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "Lingva returned empty translation.",
                    Name,
                    isRetryable: false);
            }

            return new TranslationResponse
            {
                TranslatedText = result.Translation,
                DetectedSourceLanguage = result.Info?.DetectedSource,
                Confidence = null, // Lingva doesn't provide confidence scores
                Provider = Name,
                FromCache = false
            };
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.NetworkError,
                "Network error occurred while contacting Lingva API.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "Lingva translation request timed out.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (TranslationException)
        {
            throw; // Re-throw our own exceptions
        }
        catch (Exception ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Unknown,
                $"Unexpected error: {ex.Message}",
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

        // Translate one by one (Lingva doesn't support batch requests)
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
        try
        {
            var response = await _httpClient.GetFromJsonAsync<LingvaLanguagesResponse>(
                "/api/v1/languages/source",
                cancellationToken);

            return response?.Languages?.Select(l => l.Code).ToList();
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
        try
        {
            var response = await _httpClient.GetFromJsonAsync<LingvaLanguagesResponse>(
                "/api/v1/languages/target",
                cancellationToken);

            return response?.Languages?.Select(l => l.Code).ToList();
        }
        catch
        {
            // If fetching languages fails, return null (unknown)
            return null;
        }
    }

    // Response models
    private class LingvaResponse
    {
        public string? Translation { get; set; }
        public string? Error { get; set; }
        public LingvaInfo? Info { get; set; }
    }

    private class LingvaInfo
    {
        public string? DetectedSource { get; set; }
    }

    private class LingvaLanguagesResponse
    {
        public List<LingvaLanguage>? Languages { get; set; }
    }

    private class LingvaLanguage
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
