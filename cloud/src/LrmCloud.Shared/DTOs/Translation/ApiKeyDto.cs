using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Translation;

/// <summary>
/// API key configuration for a translation provider.
/// </summary>
public class ApiKeyDto
{
    public int Id { get; set; }

    /// <summary>
    /// Provider name (e.g., "google", "deepl").
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Masked API key for display (e.g., "sk-...abc123").
    /// </summary>
    public string MaskedKey { get; set; } = string.Empty;

    /// <summary>
    /// When the key was added.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the key was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Optional label for the key.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Whether this key is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request to add or update an API key.
/// </summary>
public class SetApiKeyRequest
{
    [Required]
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional label for the key.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Additional configuration (e.g., endpoint URL for Azure).
    /// </summary>
    public Dictionary<string, string>? AdditionalConfig { get; set; }
}

/// <summary>
/// Request to test an API key.
/// </summary>
public class TestApiKeyRequest
{
    [Required]
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public Dictionary<string, string>? AdditionalConfig { get; set; }
}

/// <summary>
/// Response from testing an API key.
/// </summary>
public class TestApiKeyResponse
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string? ProviderMessage { get; set; }
}
