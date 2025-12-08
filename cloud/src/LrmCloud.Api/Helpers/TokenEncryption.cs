using System.Security.Cryptography;
using System.Text;

namespace LrmCloud.Api.Helpers;

/// <summary>
/// Provides AES-256 encryption and decryption for sensitive tokens.
/// Used for encrypting GitHub access tokens before storing in database.
/// </summary>
public static class TokenEncryption
{
    /// <summary>
    /// Encrypts a plaintext token using AES-256-CBC encryption.
    /// </summary>
    /// <param name="plaintext">The plaintext token to encrypt</param>
    /// <param name="keyBase64">Base64-encoded 32-byte encryption key</param>
    /// <returns>Base64-encoded encrypted token with IV prepended</returns>
    /// <exception cref="ArgumentException">If key is invalid</exception>
    public static string Encrypt(string plaintext, string keyBase64)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        if (string.IsNullOrEmpty(keyBase64))
            throw new ArgumentException("Encryption key cannot be null or empty", nameof(keyBase64));

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Encryption key must be valid base64", nameof(keyBase64));
        }

        if (key.Length != 32)
            throw new ArgumentException("Encryption key must be 32 bytes (256 bits)", nameof(keyBase64));

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext for storage
        var result = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts an encrypted token using AES-256-CBC decryption.
    /// </summary>
    /// <param name="encryptedBase64">Base64-encoded encrypted token with IV prepended</param>
    /// <param name="keyBase64">Base64-encoded 32-byte encryption key</param>
    /// <returns>Decrypted plaintext token</returns>
    /// <exception cref="ArgumentException">If key or encrypted data is invalid</exception>
    /// <exception cref="CryptographicException">If decryption fails</exception>
    public static string Decrypt(string encryptedBase64, string keyBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedBase64));

        if (string.IsNullOrEmpty(keyBase64))
            throw new ArgumentException("Encryption key cannot be null or empty", nameof(keyBase64));

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Encryption key must be valid base64", nameof(keyBase64));
        }

        if (key.Length != 32)
            throw new ArgumentException("Encryption key must be 32 bytes (256 bits)", nameof(keyBase64));

        byte[] encryptedBytes;
        try
        {
            encryptedBytes = Convert.FromBase64String(encryptedBase64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Encrypted data must be valid base64", nameof(encryptedBase64));
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Extract IV from the beginning of encrypted data
        var iv = new byte[aes.IV.Length];
        var ciphertext = new byte[encryptedBytes.Length - iv.Length];

        Buffer.BlockCopy(encryptedBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(encryptedBytes, iv.Length, ciphertext, 0, ciphertext.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
