namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// CLI API key information (without the actual key value).
/// </summary>
public class CliApiKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string KeyPrefix { get; set; } = null!;
    public string Scopes { get; set; } = null!;
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

/// <summary>
/// Request to create a new CLI API key.
/// </summary>
public class CreateCliApiKeyRequest
{
    public string Name { get; set; } = null!;
    public int? ProjectId { get; set; }
    public string Scopes { get; set; } = "read,write";
    public int? ExpiresInDays { get; set; }
}

/// <summary>
/// Response when creating a new CLI API key.
/// </summary>
public class CreateCliApiKeyResponse
{
    /// <summary>
    /// The full API key (only shown once during creation).
    /// </summary>
    public string ApiKey { get; set; } = null!;

    /// <summary>
    /// The key metadata.
    /// </summary>
    public CliApiKeyDto KeyInfo { get; set; } = null!;
}
