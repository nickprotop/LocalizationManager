namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// Information about an available translation provider.
/// </summary>
public class TranslationProviderDto
{
    /// <summary>
    /// Provider identifier (e.g., "google", "deepl", "openai").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider requires an API key.
    /// </summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>
    /// Whether the provider is configured and ready to use.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Provider type: "managed" for LRM, "api" for cloud APIs, "local" for self-hosted.
    /// </summary>
    public string Type { get; set; } = "api";

    /// <summary>
    /// Whether this is the LRM managed translation service.
    /// </summary>
    public bool IsManagedProvider { get; set; }

    /// <summary>
    /// Whether this is an AI/LLM-based provider.
    /// </summary>
    public bool IsAiProvider { get; set; }

    /// <summary>
    /// Optional description of the provider.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Source of the API key (if configured): "project", "user", "organization", "platform".
    /// </summary>
    public string? ApiKeySource { get; set; }
}
