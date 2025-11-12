// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using LocalizationManager.Core.Configuration;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Configuration;

public class SecureCredentialManagerTests : IDisposable
{
    private readonly string _testCredentialsPath;

    public SecureCredentialManagerTests()
    {
        // Backup existing credentials file if it exists
        _testCredentialsPath = AppDataPaths.GetCredentialsFilePath();
        if (File.Exists(_testCredentialsPath))
        {
            File.Move(_testCredentialsPath, _testCredentialsPath + ".backup");
        }
    }

    public void Dispose()
    {
        // Restore backed up credentials file
        if (File.Exists(_testCredentialsPath + ".backup"))
        {
            if (File.Exists(_testCredentialsPath))
            {
                File.Delete(_testCredentialsPath);
            }
            File.Move(_testCredentialsPath + ".backup", _testCredentialsPath);
        }
        else if (File.Exists(_testCredentialsPath))
        {
            // Clean up test credentials
            File.Delete(_testCredentialsPath);
        }
    }

    [Fact]
    public void SetApiKey_ValidInput_StoresKey()
    {
        // Arrange
        var provider = "deepl";
        var apiKey = "test-api-key-12345";

        // Act
        SecureCredentialManager.SetApiKey(provider, apiKey);

        // Assert
        var retrieved = SecureCredentialManager.GetApiKey(provider);
        Assert.Equal(apiKey, retrieved);
    }

    [Fact]
    public void SetApiKey_NullProvider_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecureCredentialManager.SetApiKey(null!, "test-key"));
    }

    [Fact]
    public void SetApiKey_EmptyProvider_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecureCredentialManager.SetApiKey("", "test-key"));
    }

    [Fact]
    public void SetApiKey_NullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecureCredentialManager.SetApiKey("deepl", null!));
    }

    [Fact]
    public void GetApiKey_NonExistentProvider_ReturnsNull()
    {
        // Act
        var apiKey = SecureCredentialManager.GetApiKey("nonexistent");

        // Assert
        Assert.Null(apiKey);
    }

    [Fact]
    public void GetApiKey_NullProvider_ReturnsNull()
    {
        // Act
        var apiKey = SecureCredentialManager.GetApiKey(null!);

        // Assert
        Assert.Null(apiKey);
    }

    [Fact]
    public void DeleteApiKey_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var provider = "deepl";
        SecureCredentialManager.SetApiKey(provider, "test-key");

        // Act
        var result = SecureCredentialManager.DeleteApiKey(provider);

        // Assert
        Assert.True(result);
        Assert.Null(SecureCredentialManager.GetApiKey(provider));
    }

    [Fact]
    public void DeleteApiKey_NonExistentKey_ReturnsFalse()
    {
        // Act
        var result = SecureCredentialManager.DeleteApiKey("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetApiKey_MultipleProviders_StoresAllCorrectly()
    {
        // Arrange & Act
        SecureCredentialManager.SetApiKey("deepl", "deepl-key");
        SecureCredentialManager.SetApiKey("google", "google-key");
        SecureCredentialManager.SetApiKey("libretranslate", "libre-key");

        // Assert
        Assert.Equal("deepl-key", SecureCredentialManager.GetApiKey("deepl"));
        Assert.Equal("google-key", SecureCredentialManager.GetApiKey("google"));
        Assert.Equal("libre-key", SecureCredentialManager.GetApiKey("libretranslate"));
    }

    [Fact]
    public void SetApiKey_UpdateExisting_Overwrites()
    {
        // Arrange
        var provider = "deepl";
        SecureCredentialManager.SetApiKey(provider, "old-key");

        // Act
        SecureCredentialManager.SetApiKey(provider, "new-key");

        // Assert
        var retrieved = SecureCredentialManager.GetApiKey(provider);
        Assert.Equal("new-key", retrieved);
    }

    [Fact]
    public void GetConfiguredProviders_WithKeys_ReturnsProviders()
    {
        // Arrange
        SecureCredentialManager.SetApiKey("deepl", "key1");
        SecureCredentialManager.SetApiKey("google", "key2");

        // Act
        var providers = SecureCredentialManager.GetConfiguredProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.Contains("deepl", providers);
        Assert.Contains("google", providers);
    }

    [Fact]
    public void GetConfiguredProviders_NoKeys_ReturnsEmpty()
    {
        // Act
        var providers = SecureCredentialManager.GetConfiguredProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.Empty(providers);
    }

    [Fact]
    public void CredentialsFileExists_AfterSetApiKey_ReturnsTrue()
    {
        // Arrange
        SecureCredentialManager.SetApiKey("deepl", "test-key");

        // Act
        var exists = SecureCredentialManager.CredentialsFileExists();

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void SetApiKey_CreatesEncryptedFile()
    {
        // Arrange & Act
        SecureCredentialManager.SetApiKey("deepl", "test-key");

        // Assert
        Assert.True(File.Exists(_testCredentialsPath));

        // Verify file contains encrypted data (not plain text)
        var fileContent = File.ReadAllText(_testCredentialsPath);
        Assert.DoesNotContain("test-key", fileContent); // Key should be encrypted
        Assert.Contains("EncryptedData", fileContent); // Should contain JSON structure
    }

    [Fact]
    public void SetApiKey_OnLinux_SetsRestrictivePermissions()
    {
        // Skip if not Linux
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Arrange & Act
        SecureCredentialManager.SetApiKey("deepl", "test-key");

        // Assert
        var fileInfo = new FileInfo(_testCredentialsPath);
        var mode = fileInfo.UnixFileMode;

        // Should have user read/write only (0600)
        Assert.True(mode.HasFlag(UnixFileMode.UserRead));
        Assert.True(mode.HasFlag(UnixFileMode.UserWrite));
        Assert.False(mode.HasFlag(UnixFileMode.GroupRead));
        Assert.False(mode.HasFlag(UnixFileMode.OtherRead));
    }
}
