using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation.Providers;

/// <summary>
/// Translation provider using Ollama (local LLM server).
/// Supports running open-source models locally without API keys.
/// </summary>
public class OllamaProvider : AITranslationProviderBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public OllamaProvider(
        string? apiUrl = null,
        string? model = null,
        string? customSystemPrompt = null,
        int rateLimitRequestsPerMinute = 10)
        : base(model ?? "llama3.2", customSystemPrompt, rateLimitRequestsPerMinute)
    {
        _apiUrl = apiUrl ?? "http://localhost:11434";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiUrl),
            Timeout = TimeSpan.FromMinutes(5) // Ollama can be slow for large models
        };
    }

    public override string Name => "ollama";

    /// <summary>
    /// Ollama is configured if we can reach the endpoint.
    /// No API key required for local instance.
    /// </summary>
    public override bool IsConfigured()
    {
        // Consider it configured if we have a valid URL
        // Actual connectivity will be tested during translation
        return !string.IsNullOrWhiteSpace(_apiUrl) && Uri.TryCreate(_apiUrl, UriKind.Absolute, out _);
    }

    public override async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (RateLimiter != null)
        {
            await RateLimiter.WaitAsync(cancellationToken);
        }

        try
        {
            var userPrompt = BuildUserPrompt(request);

            // Ollama API format: https://github.com/ollama/ollama/blob/main/docs/api.md
            var ollamaRequest = new OllamaGenerateRequest
            {
                Model = Model!,
                Prompt = $"{SystemPrompt}\n\n{userPrompt}",
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.3, // Lower temperature for more consistent translations
                    TopP = 0.9
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Ollama API returned {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);

            if (result?.Response == null)
            {
                throw new InvalidOperationException("Ollama API returned null or invalid response");
            }

            var translatedText = CleanTranslationResponse(result.Response);

            return new TranslationResponse
            {
                TranslatedText = translatedText,
                DetectedSourceLanguage = request.SourceLanguage,
                Confidence = null, // Ollama doesn't provide confidence scores
                Provider = Name,
                FromCache = false
            };
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            throw CreateTranslationException(ex, "translation request");
        }
    }

    #region Ollama API Models

    private class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double? TopP { get; set; }
    }

    private class OllamaGenerateResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    #endregion
}
