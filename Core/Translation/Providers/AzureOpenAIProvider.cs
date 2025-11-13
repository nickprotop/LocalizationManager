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
/// Translation provider using Azure OpenAI Service.
/// Requires Azure endpoint, API key, and deployment name.
/// </summary>
public class AzureOpenAIProvider : AITranslationProviderBase
{
    private readonly string? _apiKey;
    private readonly string? _endpoint;
    private readonly string? _deploymentName;
    private readonly HttpClient _httpClient;

    public AzureOpenAIProvider(
        string? apiKey,
        string? endpoint = null,
        string? deploymentName = null,
        string? customSystemPrompt = null,
        int rateLimitRequestsPerMinute = 60)
        : base(deploymentName, customSystemPrompt, rateLimitRequestsPerMinute)
    {
        _apiKey = apiKey;
        _endpoint = endpoint?.TrimEnd('/');
        _deploymentName = deploymentName;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }
    }

    public override string Name => "azureopenai";

    public override bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_apiKey) &&
               !string.IsNullOrWhiteSpace(_endpoint) &&
               !string.IsNullOrWhiteSpace(_deploymentName);
    }

    public override async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            throw new TranslationException(
                TranslationErrorCode.InvalidApiKey,
                "Azure OpenAI provider is not configured. Please set the API key, endpoint, and deployment name.",
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

            var chatRequest = new AzureOpenAIChatRequest
            {
                Messages = new[]
                {
                    new AzureOpenAIChatMessage
                    {
                        Role = "system",
                        Content = SystemPrompt
                    },
                    new AzureOpenAIChatMessage
                    {
                        Role = "user",
                        Content = userPrompt
                    }
                },
                Temperature = 0.3,
                MaxTokens = 4000
            };

            // Azure OpenAI URL format: {endpoint}/openai/deployments/{deployment-name}/chat/completions?api-version=2024-02-15-preview
            var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";

            var response = await _httpClient.PostAsJsonAsync(url, chatRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Azure OpenAI API returned {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<AzureOpenAIChatResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Azure OpenAI API returned no choices");
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

    #region Azure OpenAI API Models

    private class AzureOpenAIChatRequest
    {
        [JsonPropertyName("messages")]
        public required AzureOpenAIChatMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    private class AzureOpenAIChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class AzureOpenAIChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("choices")]
        public AzureOpenAIChatChoice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public AzureOpenAIUsage? Usage { get; set; }
    }

    private class AzureOpenAIChatChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public required AzureOpenAIChatMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class AzureOpenAIUsage
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
