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
/// Translation provider using OpenAI's GPT models.
/// Supports GPT-4, GPT-3.5-turbo, and other chat models.
/// </summary>
public class OpenAIProvider : AITranslationProviderBase
{
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;

    public OpenAIProvider(
        string? apiKey,
        string? model = null,
        string? customSystemPrompt = null,
        int rateLimitRequestsPerMinute = 60)
        : base(model ?? "gpt-4o-mini", customSystemPrompt, rateLimitRequestsPerMinute)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public override string Name => "openai";

    public override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey);

    public override async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "OpenAI provider is not configured. Please set the API key.",
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

            var chatRequest = new OpenAIChatRequest
            {
                Model = Model!,
                Messages = new[]
                {
                    new OpenAIChatMessage
                    {
                        Role = "system",
                        Content = SystemPrompt
                    },
                    new OpenAIChatMessage
                    {
                        Role = "user",
                        Content = userPrompt
                    }
                },
                Temperature = 0.3,
                MaxTokens = 4000
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", chatRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"OpenAI API returned {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("OpenAI API returned no choices");
            }

            var translatedText = CleanTranslationResponse(result.Choices[0].Message.Content);

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

    #region OpenAI API Models

    private class OpenAIChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required OpenAIChatMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    private class OpenAIChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class OpenAIChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("choices")]
        public OpenAIChatChoice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class OpenAIChatChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public required OpenAIChatMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    #endregion
}
