// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using Azure AI Translator (Cognitive Services).
/// Uses the REST API v3.0 for text translation.
/// </summary>
public class AzureTranslatorProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _region;
    private readonly string _endpoint;
    private readonly RateLimiter? _rateLimiter;

    /// <summary>
    /// Creates a new Azure Translator provider.
    /// </summary>
    /// <param name="apiKey">The Azure Translator subscription key (Ocp-Apim-Subscription-Key).</param>
    /// <param name="region">The Azure resource region (e.g., "westus", "eastus"). Optional for global resources.</param>
    /// <param name="endpoint">Custom endpoint URL. Defaults to global endpoint.</param>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 100 requests/min).</param>
    public AzureTranslatorProvider(
        string? apiKey,
        string? region = null,
        string? endpoint = null,
        int rateLimitRequestsPerMinute = 100)
    {
        _apiKey = apiKey;
        _region = region;
        _endpoint = endpoint ?? "https://api.cognitive.microsofttranslator.com";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_endpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            // Add region header if specified (required for multi-service/regional resources)
            if (!string.IsNullOrWhiteSpace(region))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", region);
            }

            _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
        }
    }

    /// <inheritdoc />
    public string Name => "azuretranslator";

    /// <inheritdoc />
    public bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey);

    /// <inheritdoc />
    public int? GetRateLimit() => 100; // Azure Translator: varies by tier

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured() || _rateLimiter == null)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Azure Translator API key is not configured.",
                Name,
                isRetryable: false);
        }

        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // Build URL with query parameters
            var urlBuilder = new System.Text.StringBuilder("/translate?api-version=3.0");
            urlBuilder.Append($"&to={request.TargetLanguage}");

            if (!string.IsNullOrWhiteSpace(request.SourceLanguage))
            {
                urlBuilder.Append($"&from={request.SourceLanguage}");
            }

            // Build request body
            var requestBody = new[] { new AzureTranslateTextRequest { Text = request.SourceText } };

            // Send request
            var response = await _httpClient.PostAsJsonAsync(
                urlBuilder.ToString(),
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TranslationException(
                    MapStatusCodeToErrorCode((int)response.StatusCode),
                    $"Azure Translator API error: {response.StatusCode} - {errorContent}",
                    Name,
                    isRetryable: response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                 response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);
            }

            var result = await response.Content.ReadFromJsonAsync<List<AzureTranslateResponse>>(cancellationToken);

            if (result == null || result.Count == 0 || result[0]?.Translations == null || result[0]?.Translations?.Length == 0)
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "Azure Translator returned empty or invalid response.",
                    Name,
                    isRetryable: false);
            }

            var translation = result[0]!.Translations![0];
            var detectedLanguage = result[0]!.DetectedLanguage?.Language;

            return new TranslationResponse
            {
                TranslatedText = translation.Text ?? string.Empty,
                DetectedSourceLanguage = detectedLanguage,
                Confidence = result[0]!.DetectedLanguage?.Score,
                Provider = Name,
                FromCache = false
            };
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.NetworkError,
                $"Network error communicating with Azure Translator: {ex.Message}",
                Name,
                isRetryable: true,
                ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "Azure Translator request was cancelled or timed out.",
                Name,
                isRetryable: true,
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranslationResponse>> TranslateBatchAsync(
        IEnumerable<TranslationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured() || _rateLimiter == null)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Azure Translator API key is not configured.",
                Name,
                isRetryable: false);
        }

        var requestList = requests.ToList();

        // Group by language pair for efficient batching
        var grouped = requestList
            .GroupBy(r => (r.SourceLanguage, r.TargetLanguage))
            .ToList();

        var allResponses = new List<TranslationResponse>();

        foreach (var group in grouped)
        {
            try
            {
                // Apply rate limiting
                await _rateLimiter.WaitAsync(cancellationToken);

                // Build URL with query parameters
                var urlBuilder = new System.Text.StringBuilder("/translate?api-version=3.0");
                urlBuilder.Append($"&to={group.Key.TargetLanguage}");

                if (!string.IsNullOrWhiteSpace(group.Key.SourceLanguage))
                {
                    urlBuilder.Append($"&from={group.Key.SourceLanguage}");
                }

                // Build batch request body (Azure supports up to 100 text elements per request)
                var requestBody = group.Select(r => new AzureTranslateTextRequest { Text = r.SourceText }).ToArray();

                // Send batch request
                var response = await _httpClient.PostAsJsonAsync(
                    urlBuilder.ToString(),
                    requestBody,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // If batch fails, fall back to individual translations
                    foreach (var request in group)
                    {
                        var individualResponse = await TranslateAsync(request, cancellationToken);
                        allResponses.Add(individualResponse);
                    }
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<List<AzureTranslateResponse>>(cancellationToken);

                if (result != null)
                {
                    foreach (var item in result)
                    {
                        if (item?.Translations != null && item.Translations.Length > 0)
                        {
                            var translation = item.Translations[0];
                            var detectedLanguage = item.DetectedLanguage?.Language;

                            allResponses.Add(new TranslationResponse
                            {
                                TranslatedText = translation?.Text ?? string.Empty,
                                DetectedSourceLanguage = detectedLanguage,
                                Confidence = item.DetectedLanguage?.Score,
                                Provider = Name,
                                FromCache = false
                            });
                        }
                    }
                }
            }
            catch
            {
                // If batch processing fails, fall back to individual translations
                foreach (var request in group)
                {
                    try
                    {
                        var individualResponse = await TranslateAsync(request, cancellationToken);
                        allResponses.Add(individualResponse);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
        }

        return allResponses;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        // Azure Translator supports 100+ languages
        // Return null to indicate unknown (would require separate API call to /languages endpoint)
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        // Azure Translator supports the same languages for source and target
        return GetSupportedSourceLanguagesAsync(cancellationToken);
    }

    private static TranslationErrorCode MapStatusCodeToErrorCode(int statusCode)
    {
        return statusCode switch
        {
            401 or 403 => TranslationErrorCode.InvalidApiKey,
            429 => TranslationErrorCode.QuotaExceeded,
            400 => TranslationErrorCode.InvalidRequest,
            408 => TranslationErrorCode.Timeout,
            500 or 502 or 503 or 504 => TranslationErrorCode.ServiceUnavailable,
            _ => TranslationErrorCode.Unknown
        };
    }

    #region Azure Translator API Models

    private class AzureTranslateTextRequest
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    private class AzureTranslateResponse
    {
        [JsonPropertyName("detectedLanguage")]
        public AzureDetectedLanguage? DetectedLanguage { get; set; }

        [JsonPropertyName("translations")]
        public AzureTranslation[]? Translations { get; set; }
    }

    private class AzureDetectedLanguage
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }
    }

    private class AzureTranslation
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }
    }

    #endregion
}
