// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using LibreTranslate API.
/// </summary>
public class LibreTranslateProvider : ITranslationProvider
{
    private readonly string? _apiKey;
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Creates a new LibreTranslate provider.
    /// </summary>
    /// <param name="apiKey">The API key (optional for some instances).</param>
    /// <param name="apiUrl">The LibreTranslate API URL (default: https://libretranslate.com).</param>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 20 requests/min for public instance).</param>
    public LibreTranslateProvider(
        string? apiKey = null,
        string? apiUrl = null,
        int rateLimitRequestsPerMinute = 20)
    {
        _apiKey = apiKey;
        _apiUrl = apiUrl ?? "https://libretranslate.com";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
    }

    /// <inheritdoc />
    public string Name => "libretranslate";

    /// <inheritdoc />
    public bool IsConfigured() => true; // LibreTranslate can work without API key on public instances

    /// <inheritdoc />
    public int? GetRateLimit() => 20; // Conservative limit for public instance

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // Build request payload
            var payload = new Dictionary<string, object>
            {
                ["q"] = request.SourceText,
                ["target"] = request.TargetLanguage
            };

            if (!string.IsNullOrWhiteSpace(request.SourceLanguage))
            {
                payload["source"] = request.SourceLanguage;
            }
            else
            {
                payload["source"] = "auto"; // Auto-detect
            }

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                payload["api_key"] = _apiKey;
            }

            // Send request
            var response = await _httpClient.PostAsJsonAsync(
                "/translate",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TranslationException(
                    response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                        ? TranslationErrorCode.RateLimitExceeded
                        : TranslationErrorCode.Unknown,
                    $"LibreTranslate API error: {response.StatusCode} - {errorContent}",
                    Name,
                    isRetryable: response.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
            }

            var result = await response.Content.ReadFromJsonAsync<LibreTranslateResponse>(cancellationToken);

            if (result == null || string.IsNullOrEmpty(result.TranslatedText))
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "LibreTranslate returned empty response.",
                    Name,
                    isRetryable: false);
            }

            return new TranslationResponse
            {
                TranslatedText = result.TranslatedText,
                DetectedSourceLanguage = result.DetectedLanguage?.Language,
                Confidence = result.DetectedLanguage?.Confidence,
                Provider = Name,
                FromCache = false
            };
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.NetworkError,
                "Network error occurred while contacting LibreTranslate API.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "LibreTranslate translation request timed out.",
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

        // Translate one by one (LibreTranslate doesn't support true batch)
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
            var response = await _httpClient.GetFromJsonAsync<LibreTranslateLanguage[]>(
                "/languages",
                cancellationToken);

            return response?.Select(l => l.Code).ToList();
        }
        catch
        {
            // If fetching languages fails, return null (unknown)
            return null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        // LibreTranslate supports the same languages for source and target
        return GetSupportedSourceLanguagesAsync(cancellationToken);
    }

    // Response models
    private class LibreTranslateResponse
    {
        public string TranslatedText { get; set; } = string.Empty;
        public DetectedLanguageInfo? DetectedLanguage { get; set; }
    }

    private class DetectedLanguageInfo
    {
        public double Confidence { get; set; }
        public string Language { get; set; } = string.Empty;
    }

    private class LibreTranslateLanguage
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
