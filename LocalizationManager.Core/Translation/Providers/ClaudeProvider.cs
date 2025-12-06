using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using Anthropic's Claude models.
/// Supports Claude 3.5 Sonnet, Claude 3 Opus, and other models.
/// </summary>
public class ClaudeProvider : AITranslationProviderBase
{
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;

    public ClaudeProvider(
        string? apiKey,
        string? model = null,
        string? customSystemPrompt = null,
        int rateLimitRequestsPerMinute = 50)
        : base(model ?? "claude-3-5-sonnet-20241022", customSystemPrompt, rateLimitRequestsPerMinute)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/v1/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
    }

    public override string Name => "claude";

    public override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey);

    public override async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Claude provider is not configured. Please set the API key.",
                Name,
                false);
        }

        if (RateLimiter != null)
        {
            await RateLimiter.WaitAsync(cancellationToken);
        }

        try
        {
            var userPrompt = BuildUserPrompt(request);

            var claudeRequest = new ClaudeMessagesRequest
            {
                Model = Model!,
                MaxTokens = 4096,
                System = SystemPrompt,
                Messages = new[]
                {
                    new ClaudeMessage
                    {
                        Role = "user",
                        Content = userPrompt
                    }
                },
                Temperature = 0.3
            };

            var response = await _httpClient.PostAsJsonAsync("messages", claudeRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Claude API returned {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeMessagesResponse>(cancellationToken);

            if (result?.Content == null || result.Content.Length == 0)
            {
                throw new InvalidOperationException("Claude API returned no content");
            }

            var translatedText = CleanTranslationResponse(result.Content[0].Text);

            return new TranslationResponse
            {
                TranslatedText = translatedText,
                DetectedSourceLanguage = request.SourceLanguage,
                Confidence = null,
                Provider = Name,
                FromCache = false
            };
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            throw CreateTranslationException(ex, "translation request");
        }
    }

    #region Claude API Models

    private class ClaudeMessagesRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("messages")]
        public required ClaudeMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class ClaudeMessagesResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public ClaudeContentBlock[]? Content { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    private class ClaudeContentBlock
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    private class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    #endregion
}
