namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Service for encrypting and decrypting API keys stored in the database.
/// Uses AES-256 encryption with a key derived from the platform's master secret.
/// </summary>
public interface IApiKeyEncryptionService
{
    /// <summary>
    /// Encrypt an API key for storage.
    /// </summary>
    /// <param name="plainKey">The plain text API key.</param>
    /// <returns>Encrypted string (Base64 encoded).</returns>
    string Encrypt(string plainKey);

    /// <summary>
    /// Decrypt an API key from storage.
    /// </summary>
    /// <param name="encryptedKey">The encrypted key (Base64 encoded).</param>
    /// <returns>The plain text API key.</returns>
    string Decrypt(string encryptedKey);

    /// <summary>
    /// Get a masked version of a key for display (e.g., "sk-...abc123").
    /// </summary>
    /// <param name="plainKey">The plain text key.</param>
    /// <returns>Masked key string.</returns>
    string Mask(string plainKey);
}
