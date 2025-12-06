// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Manages secure storage of API keys using AES encryption.
/// Credentials are stored encrypted in the user's application data directory.
/// This is cross-platform and works on both Windows and Linux.
/// </summary>
public static class SecureCredentialManager
{
    private const int KeySize = 256; // AES-256
    private const int Iterations = 100000; // PBKDF2 iterations

    /// <summary>
    /// Sets an encrypted API key for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name (e.g., "google", "deepl").</param>
    /// <param name="apiKey">The API key to store.</param>
    public static void SetApiKey(string provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
        }

        var credentials = LoadCredentials();
        credentials[provider.ToLowerInvariant()] = apiKey;
        SaveCredentials(credentials);
    }

    /// <summary>
    /// Gets a decrypted API key for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>The API key if found, otherwise null.</returns>
    public static string? GetApiKey(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        try
        {
            var credentials = LoadCredentials();
            return credentials.TryGetValue(provider.ToLowerInvariant(), out var apiKey) ? apiKey : null;
        }
        catch
        {
            // If credentials file is corrupt or unreadable, return null
            return null;
        }
    }

    /// <summary>
    /// Deletes the API key for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>True if the key was deleted, false if it didn't exist.</returns>
    public static bool DeleteApiKey(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        var credentials = LoadCredentials();
        var removed = credentials.Remove(provider.ToLowerInvariant());

        if (removed)
        {
            SaveCredentials(credentials);
        }

        return removed;
    }

    /// <summary>
    /// Gets all configured provider names.
    /// </summary>
    /// <returns>A list of provider names that have API keys stored.</returns>
    public static IReadOnlyList<string> GetConfiguredProviders()
    {
        try
        {
            var credentials = LoadCredentials();
            return new List<string>(credentials.Keys);
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Checks if the credentials file exists.
    /// </summary>
    /// <returns>True if the credentials file exists.</returns>
    public static bool CredentialsFileExists()
    {
        return File.Exists(AppDataPaths.GetCredentialsFilePath());
    }

    private static Dictionary<string, string> LoadCredentials()
    {
        var filePath = AppDataPaths.GetCredentialsFilePath();

        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var container = JsonSerializer.Deserialize<CredentialContainer>(json);

            if (container == null || string.IsNullOrWhiteSpace(container.EncryptedData))
            {
                return new Dictionary<string, string>();
            }

            // Decrypt the credentials
            var decrypted = Decrypt(container.EncryptedData, container.Salt, container.IV);
            var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
            return credentials ?? new Dictionary<string, string>();
        }
        catch
        {
            // If anything goes wrong (corrupt file, wrong format, etc.), return empty
            return new Dictionary<string, string>();
        }
    }

    private static void SaveCredentials(Dictionary<string, string> credentials)
    {
        var filePath = AppDataPaths.GetCredentialsFilePath();

        // Serialize credentials to JSON
        var json = JsonSerializer.Serialize(credentials);

        // Generate salt and IV
        var salt = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);

        // Encrypt
        var encrypted = Encrypt(json, salt, iv);

        // Create container
        var container = new CredentialContainer
        {
            Version = "1.0",
            EncryptedData = encrypted,
            Salt = Convert.ToBase64String(salt),
            IV = Convert.ToBase64String(iv)
        };

        // Save to file
        var containerJson = JsonSerializer.Serialize(container, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, containerJson);

        // Set restrictive permissions (only current user can read)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // chmod 600 (read/write for owner only)
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string Encrypt(string plaintext, byte[] salt, byte[] iv)
    {
        // Derive key from machine-specific entropy
        var key = DeriveKey(salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        return Convert.ToBase64String(ciphertextBytes);
    }

    private static string Decrypt(string ciphertext, string saltBase64, string ivBase64)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var iv = Convert.FromBase64String(ivBase64);
        var key = DeriveKey(salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var ciphertextBytes = Convert.FromBase64String(ciphertext);
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] DeriveKey(byte[] salt)
    {
        // Use machine-specific entropy as the password
        // This makes the encrypted file machine-specific (can't be copied to another machine)
        var machineEntropy = GetMachineEntropy();

        using var pbkdf2 = new Rfc2898DeriveBytes(
            machineEntropy,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySize / 8);
    }

    private static string GetMachineEntropy()
    {
        // Combine machine name and user name as entropy
        // This is cross-platform and provides reasonable machine-specific identification
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        return $"{machineName}|{userName}";
    }

    private class CredentialContainer
    {
        public string Version { get; set; } = "1.0";
        public string EncryptedData { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string IV { get; set; } = string.Empty;
    }
}
