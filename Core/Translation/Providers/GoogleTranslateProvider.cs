// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Translate.V3;
using Grpc.Core;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using Google Cloud Translation API V3.
/// </summary>
public class GoogleTranslateProvider : ITranslationProvider
{
    private readonly string? _apiKey;
    private readonly string _projectId;
    private readonly TranslationServiceClient? _client;
    private readonly RateLimiter? _rateLimiter;

    /// <summary>
    /// Creates a new Google Translate provider.
    /// </summary>
    /// <param name="apiKey">The Google Cloud API key.</param>
    /// <param name="projectId">The Google Cloud project ID (default: "default-project").</param>
    /// <param name="rateLimitRequestsPerMinute">Rate limit (default: 100 requests/min).</param>
    public GoogleTranslateProvider(
        string? apiKey,
        string? projectId = null,
        int rateLimitRequestsPerMinute = 100)
    {
        _apiKey = apiKey;
        _projectId = projectId ?? "default-project";

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                // Google Cloud uses service account JSON for authentication
                // Store the API key (service account JSON) in GOOGLE_APPLICATION_CREDENTIALS env var
                // For simplicity, we'll use the standard credential flow
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", apiKey);

                _client = TranslationServiceClient.Create();
                _rateLimiter = new RateLimiter(rateLimitRequestsPerMinute);
            }
            catch
            {
                // If client creation fails, we'll handle it in IsConfigured()
                _client = null;
            }
        }
    }

    /// <inheritdoc />
    public string Name => "google";

    /// <inheritdoc />
    public bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey) && _client != null;

    /// <inheritdoc />
    public int? GetRateLimit() => 100; // Google Cloud Translation: varies by quota

    /// <inheritdoc />
    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || _rateLimiter == null)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Google Translate API key is not configured or client initialization failed.",
                Name,
                isRetryable: false);
        }

        try
        {
            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            // Build request
            var translateRequest = new TranslateTextRequest
            {
                Contents = { request.SourceText },
                TargetLanguageCode = request.TargetLanguage,
                Parent = $"projects/{_projectId}/locations/global"
            };

            if (!string.IsNullOrWhiteSpace(request.SourceLanguage))
            {
                translateRequest.SourceLanguageCode = request.SourceLanguage;
            }

            // Translate
            var response = await _client.TranslateTextAsync(translateRequest, cancellationToken);

            if (response.Translations.Count == 0)
            {
                throw new TranslationException(
                    TranslationErrorCode.InvalidRequest,
                    "Google Translate returned empty response.",
                    Name,
                    isRetryable: false);
            }

            var translation = response.Translations[0];

            return new TranslationResponse
            {
                TranslatedText = translation.TranslatedText,
                DetectedSourceLanguage = translation.DetectedLanguageCode,
                Provider = Name,
                FromCache = false
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Invalid Google Cloud API key or authentication failed.",
                Name,
                isRetryable: false,
                ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
        {
            throw new TranslationException(
                TranslationErrorCode.QuotaExceeded,
                "Google Cloud quota exceeded. Check your billing and quotas.",
                Name,
                isRetryable: false,
                ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            throw new TranslationException(
                TranslationErrorCode.UnsupportedLanguage,
                $"Unsupported language pair or invalid request: {ex.Message}",
                Name,
                isRetryable: false,
                ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new TranslationException(
                TranslationErrorCode.ServiceUnavailable,
                "Google Translate service is temporarily unavailable.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "Google Translate request timed out.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Timeout,
                "Google Translate request was cancelled.",
                Name,
                isRetryable: true,
                ex);
        }
        catch (RpcException ex)
        {
            throw new TranslationException(
                TranslationErrorCode.Unknown,
                $"Google Translate API error: {ex.Status.Detail}",
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
        if (_client == null || _rateLimiter == null)
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Google Translate API key is not configured.",
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

                // Build batch request
                var translateRequest = new TranslateTextRequest
                {
                    Contents = { group.Select(r => r.SourceText) },
                    TargetLanguageCode = group.Key.TargetLanguage,
                    Parent = $"projects/{_projectId}/locations/global"
                };

                if (!string.IsNullOrWhiteSpace(group.Key.SourceLanguage))
                {
                    translateRequest.SourceLanguageCode = group.Key.SourceLanguage;
                }

                // Translate batch
                var response = await _client.TranslateTextAsync(translateRequest, cancellationToken);

                // Map responses back
                for (int i = 0; i < response.Translations.Count; i++)
                {
                    var translation = response.Translations[i];
                    allResponses.Add(new TranslationResponse
                    {
                        TranslatedText = translation.TranslatedText,
                        DetectedSourceLanguage = translation.DetectedLanguageCode,
                        Provider = Name,
                        FromCache = false
                    });
                }
            }
            catch
            {
                // If batch fails, fall back to individual translations
                foreach (var request in group)
                {
                    try
                    {
                        var response = await TranslateAsync(request, cancellationToken);
                        allResponses.Add(response);
                    }
                    catch
                    {
                        // Skip failed individual translations
                        throw;
                    }
                }
            }
        }

        return allResponses;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return null;
        }

        try
        {
            var request = new GetSupportedLanguagesRequest
            {
                Parent = $"projects/{_projectId}/locations/global",
                DisplayLanguageCode = "en"
            };

            var response = await _client.GetSupportedLanguagesAsync(request, cancellationToken);
            return response.Languages.Select(l => l.LanguageCode).ToList();
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
        // Google Translate supports the same languages for source and target
        return GetSupportedSourceLanguagesAsync(cancellationToken);
    }
}
