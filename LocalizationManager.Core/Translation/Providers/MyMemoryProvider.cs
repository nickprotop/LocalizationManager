// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using MyMemory Translation API.
/// MyMemory offers free anonymous usage (5,000 characters/day).
/// </summary>
public class MyMemoryProvider : ITranslationProvider
{
    private const string ApiBaseUrl = "https://api.mymemory.translated.net";
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Creates a new MyMemory provider.
    /// </summary>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 20 requests/min - conservative for free tier).</param>
    public MyMemoryProvider(int rateLimitRequestsPerMinute = 20)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
    }

    /// <inheritdoc />
    public string Name => "mymemory";

    /// <inheritdoc />
    public bool IsConfigured() => true; // MyMemory doesn't require an API key for anonymous usage

    /// <inheritdoc />
    public int? GetRateLimit() => 20;

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // MyMemory API format: /get?q={text}&langpair={source}|{target}
            var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "en" : request.SourceLanguage;
            var targetLang = request.TargetLanguage;
            var encodedText = Uri.EscapeDataString(request.SourceText);

            var requestUrl = $"/get?q={encodedText}&langpair={sourceLang}|{targetLang}";

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TranslationException(
                    response.StatusCode == HttpStatusCode.TooManyRequests
                        ? TranslationErrorCode.RateLimitExceeded
                        : TranslationErrorCode.Unknown,
                    $"MyMemory API error: {response.StatusCode} - {errorContent}",
                    Name,
                    isRetryable: response.StatusCode == HttpStatusCode.TooManyRequests ||
                                 response.StatusCode == HttpStatusCode.ServiceUnavailable);
            }

            var result = await response.Content.ReadFromJsonAsync<MyMemoryResponse>(cancellationToken);

            if (result == null)
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "MyMemory returned empty response.",
                    Name,
                    isRetryable: false);
            }

            // Check response status (200 = success, 403 = daily limit exceeded, etc.)
            if (result.ResponseStatus != 200)
            {
                var errorCode = result.ResponseStatus == 403
                    ? TranslationErrorCode.QuotaExceeded
                    : TranslationErrorCode.Unknown;

                var errorMessage = result.ResponseStatus == 403
                    ? "MyMemory daily quota exceeded (5,000 characters/day for anonymous users)."
                    : $"MyMemory error: status {result.ResponseStatus} - {result.ResponseDetails}";

                throw new TranslationException(
                    errorCode,
                    errorMessage,
                    Name,
                    isRetryable: false);
            }

            if (result.ResponseData == null || string.IsNullOrEmpty(result.ResponseData.TranslatedText))
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "MyMemory returned empty translation.",
                    Name,
                    isRetryable: false);
            }

            // MyMemory returns "QUERY LENGTH LIMIT EXCEEDED" if text is too long
            if (result.ResponseData.TranslatedText.Contains("QUERY LENGTH LIMIT EXCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                throw new TranslationException(
                    TranslationErrorCode.TextTooLong,
                    "MyMemory: Text exceeds maximum length (500 bytes).",
                    Name,
                    isRetryable: false);
            }

            // Match score (0-1) can be used as confidence
            double? confidence = result.ResponseData.Match > 0 ? result.ResponseData.Match : null;

            return new TranslationResponse
            {
                TranslatedText = result.ResponseData.TranslatedText,
                DetectedSourceLanguage = null, // MyMemory doesn't return detected language
                Confidence = confidence,
                Provider = Name,
                FromCache = false
            };
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.NetworkError,
                "Network error occurred while contacting MyMemory API.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "MyMemory translation request timed out.",
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

        // Translate one by one (MyMemory doesn't support batch requests)
        foreach (var request in requestList)
        {
            var response = await TranslateAsync(request, cancellationToken);
            responses.Add(response);
        }

        return responses;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        // MyMemory supports most common languages but doesn't have a languages endpoint
        // Return null to indicate "unknown/all supported"
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        // MyMemory supports most common languages
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    // Response models
    private class MyMemoryResponse
    {
        [JsonPropertyName("responseData")]
        public MyMemoryResponseData? ResponseData { get; set; }

        [JsonPropertyName("responseStatus")]
        public int ResponseStatus { get; set; }

        [JsonPropertyName("responseDetails")]
        public string? ResponseDetails { get; set; }
    }

    private class MyMemoryResponseData
    {
        [JsonPropertyName("translatedText")]
        public string TranslatedText { get; set; } = string.Empty;

        [JsonPropertyName("match")]
        public double Match { get; set; }
    }
}
