using LrmCloud.Shared.DTOs.Auth;

namespace LrmCloud.Api.Services;

/// <summary>
/// Result of API key validation with full details for authentication.
/// </summary>
public class ApiKeyValidationResult
{
    public int UserId { get; set; }
    public int? ProjectId { get; set; }
    public string Scopes { get; set; } = "read,write";
    public string KeyPrefix { get; set; } = "";
}

/// <summary>
/// Service for managing CLI API keys.
/// </summary>
public interface ICliApiKeyService
{
    /// <summary>
    /// Get all API keys for a user.
    /// </summary>
    Task<List<CliApiKeyDto>> GetUserKeysAsync(int userId);

    /// <summary>
    /// Create a new API key.
    /// </summary>
    Task<(string ApiKey, CliApiKeyDto KeyInfo)?> CreateKeyAsync(int userId, CreateCliApiKeyRequest request);

    /// <summary>
    /// Delete an API key.
    /// </summary>
    Task<bool> DeleteKeyAsync(int userId, int keyId);

    /// <summary>
    /// Validate an API key and return the user ID if valid.
    /// </summary>
    Task<(int UserId, int? ProjectId)?> ValidateKeyAsync(string apiKey);

    /// <summary>
    /// Validate an API key and return full details including scopes for authentication.
    /// </summary>
    Task<ApiKeyValidationResult?> ValidateKeyWithScopesAsync(string apiKey);
}
