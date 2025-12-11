using System.Security.Cryptography;
using System.Text;
using LrmCloud.Shared.Configuration;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// AES-256 encryption service for API keys.
/// The encryption key is derived from the apiKeyMasterSecret configuration or API_KEY_MASTER_SECRET environment variable.
/// </summary>
public class ApiKeyEncryptionService : IApiKeyEncryptionService
{
    private readonly byte[] _encryptionKey;
    private const int KeySize = 256;
    private const int IvSize = 16; // 128 bits for AES

    public ApiKeyEncryptionService(CloudConfiguration config)
    {
        // Get master secret from strongly-typed configuration or environment variable
        var masterSecret = config.ApiKeyMasterSecret
            ?? Environment.GetEnvironmentVariable("API_KEY_MASTER_SECRET")
            ?? throw new InvalidOperationException(
                "apiKeyMasterSecret configuration or API_KEY_MASTER_SECRET environment variable is required");

        // Derive a 256-bit key from the master secret using PBKDF2
        _encryptionKey = DeriveKey(masterSecret);
    }

    public string Encrypt(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey))
            throw new ArgumentException("Key cannot be null or empty", nameof(plainKey));

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainKey);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedKey)
    {
        if (string.IsNullOrEmpty(encryptedKey))
            throw new ArgumentException("Encrypted key cannot be null or empty", nameof(encryptedKey));

        var encryptedData = Convert.FromBase64String(encryptedKey);

        if (encryptedData.Length < IvSize)
            throw new ArgumentException("Invalid encrypted data", nameof(encryptedKey));

        // Extract IV and encrypted content
        var iv = new byte[IvSize];
        var cipherText = new byte[encryptedData.Length - IvSize];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedData, IvSize, cipherText, 0, cipherText.Length);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Key = _encryptionKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    public string Mask(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey))
            return "(empty)";

        if (plainKey.Length <= 8)
            return "****";

        // Show first 4 chars and last 4 chars
        return $"{plainKey[..4]}...{plainKey[^4..]}";
    }

    private static byte[] DeriveKey(string masterSecret)
    {
        // Use a fixed salt (application-specific)
        // In production, consider using a per-user salt stored separately
        var salt = Encoding.UTF8.GetBytes("LrmCloud.ApiKeyEncryption.v1");

        using var pbkdf2 = new Rfc2898DeriveBytes(
            masterSecret,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32); // 256 bits
    }
}
