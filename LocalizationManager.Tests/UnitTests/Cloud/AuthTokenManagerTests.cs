using LocalizationManager.Core.Cloud;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class AuthTokenManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public AuthTokenManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm_auth_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region GetTokenAsync Tests

    [Fact]
    public async Task GetTokenAsync_NoFile_ReturnsNull()
    {
        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenAsync_ValidToken_ReturnsToken()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "test-token-123");

        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Equal("test-token-123", result);
    }

    [Fact]
    public async Task GetTokenAsync_DifferentHost_ReturnsNull()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token1");

        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "other.host.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenAsync_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var expiredDate = DateTime.UtcNow.AddHours(-1);
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "expired-token", expiredDate);

        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenAsync_ValidNotExpiredToken_ReturnsToken()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddHours(1);
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "valid-token", futureDate);

        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Equal("valid-token", result);
    }

    [Fact]
    public async Task GetTokenAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var authPath = Path.Combine(lrmDir, "auth.json");
        await File.WriteAllTextAsync(authPath, "{ invalid json }");

        // Act
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region SetTokenAsync Tests

    [Fact]
    public async Task SetTokenAsync_CreatesLrmDirectory()
    {
        // Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".lrm")));
    }

    [Fact]
    public async Task SetTokenAsync_CreatesAuthFile()
    {
        // Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Assert
        Assert.True(File.Exists(Path.Combine(_testDirectory, ".lrm", "auth.json")));
    }

    [Fact]
    public async Task SetTokenAsync_OverwritesExistingToken()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "old-token");

        // Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "new-token");

        // Assert
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        Assert.Equal("new-token", result);
    }

    [Fact]
    public async Task SetTokenAsync_MultipleHosts_StoresSeparately()
    {
        // Arrange & Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token1");
        await AuthTokenManager.SetTokenAsync(_testDirectory, "staging.lrm.cloud", "token2");

        // Assert
        var token1 = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        var token2 = await AuthTokenManager.GetTokenAsync(_testDirectory, "staging.lrm.cloud");

        Assert.Equal("token1", token1);
        Assert.Equal("token2", token2);
    }

    [Fact]
    public async Task SetTokenAsync_WritesIndentedJson()
    {
        // Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Assert
        var authPath = Path.Combine(_testDirectory, ".lrm", "auth.json");
        var content = await File.ReadAllTextAsync(authPath);
        Assert.Contains("\n", content); // Indented JSON has newlines
    }

    #endregion

    #region SetAuthenticationAsync Tests

    [Fact]
    public async Task SetAuthenticationAsync_StoresAllFields()
    {
        // Arrange
        var accessExpiry = DateTime.UtcNow.AddHours(1);
        var refreshExpiry = DateTime.UtcNow.AddDays(7);

        // Act
        await AuthTokenManager.SetAuthenticationAsync(
            _testDirectory,
            "lrm.cloud",
            "access-token",
            accessExpiry,
            "refresh-token",
            refreshExpiry);

        // Assert
        var accessToken = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        var refreshToken = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "lrm.cloud");

        Assert.Equal("access-token", accessToken);
        Assert.Equal("refresh-token", refreshToken);
    }

    [Fact]
    public async Task SetAuthenticationAsync_NullRefreshToken_StoresAccessOnly()
    {
        // Act
        await AuthTokenManager.SetAuthenticationAsync(
            _testDirectory,
            "lrm.cloud",
            "access-token",
            DateTime.UtcNow.AddHours(1),
            null,
            null);

        // Assert
        var accessToken = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        var refreshToken = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "lrm.cloud");

        Assert.Equal("access-token", accessToken);
        Assert.Null(refreshToken);
    }

    #endregion

    #region GetRefreshTokenAsync Tests

    [Fact]
    public async Task GetRefreshTokenAsync_NoFile_ReturnsNull()
    {
        // Act
        var result = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_ValidRefreshToken_ReturnsToken()
    {
        // Arrange
        await AuthTokenManager.SetAuthenticationAsync(
            _testDirectory,
            "lrm.cloud",
            "access",
            DateTime.UtcNow.AddHours(1),
            "refresh-token-123",
            DateTime.UtcNow.AddDays(7));

        // Act
        var result = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Equal("refresh-token-123", result);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_ExpiredRefreshToken_ReturnsNull()
    {
        // Arrange
        await AuthTokenManager.SetAuthenticationAsync(
            _testDirectory,
            "lrm.cloud",
            "access",
            DateTime.UtcNow.AddHours(1),
            "refresh-token",
            DateTime.UtcNow.AddHours(-1)); // Expired

        // Act
        var result = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_DifferentHost_ReturnsNull()
    {
        // Arrange
        await AuthTokenManager.SetAuthenticationAsync(
            _testDirectory,
            "lrm.cloud",
            "access",
            DateTime.UtcNow.AddHours(1),
            "refresh",
            DateTime.UtcNow.AddDays(7));

        // Act
        var result = await AuthTokenManager.GetRefreshTokenAsync(_testDirectory, "other.host.com");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region RemoveTokenAsync Tests

    [Fact]
    public async Task RemoveTokenAsync_NoFile_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "lrm.cloud");
    }

    [Fact]
    public async Task RemoveTokenAsync_ExistingToken_RemovesToken()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Act
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveTokenAsync_OnlyTokenForHost_DeletesFile()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");
        var authPath = Path.Combine(_testDirectory, ".lrm", "auth.json");

        // Act
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.False(File.Exists(authPath));
    }

    [Fact]
    public async Task RemoveTokenAsync_MultipleHosts_PreservesOthers()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token1");
        await AuthTokenManager.SetTokenAsync(_testDirectory, "staging.lrm.cloud", "token2");

        // Act
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        var token1 = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        var token2 = await AuthTokenManager.GetTokenAsync(_testDirectory, "staging.lrm.cloud");

        Assert.Null(token1);
        Assert.Equal("token2", token2);
    }

    [Fact]
    public async Task RemoveTokenAsync_NonexistentHost_DoesNothing()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Act
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "other.host.com");

        // Assert
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");
        Assert.Equal("token", result);
    }

    [Fact]
    public async Task RemoveTokenAsync_InvalidJson_DoesNotThrow()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var authPath = Path.Combine(lrmDir, "auth.json");
        await File.WriteAllTextAsync(authPath, "{ invalid }");

        // Act & Assert - Should not throw
        await AuthTokenManager.RemoveTokenAsync(_testDirectory, "lrm.cloud");
    }

    #endregion

    #region HasTokenAsync Tests

    [Fact]
    public async Task HasTokenAsync_NoToken_ReturnsFalse()
    {
        // Act
        var result = await AuthTokenManager.HasTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasTokenAsync_ValidToken_ReturnsTrue()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Act
        var result = await AuthTokenManager.HasTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasTokenAsync_ExpiredToken_ReturnsFalse()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(
            _testDirectory,
            "lrm.cloud",
            "token",
            DateTime.UtcNow.AddHours(-1));

        // Act
        var result = await AuthTokenManager.HasTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasTokenAsync_DifferentHost_ReturnsFalse()
    {
        // Arrange
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "token");

        // Act
        var result = await AuthTokenManager.HasTokenAsync(_testDirectory, "other.host.com");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TokenWithSpecialCharacters_PreservesCorrectly()
    {
        // Arrange
        var specialToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWUsImlhdCI6MTUxNjIzOTAyMn0";

        // Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", specialToken);
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert
        Assert.Equal(specialToken, result);
    }

    [Fact]
    public async Task HostWithPort_StoresSeparately()
    {
        // Arrange & Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "localhost:5000", "token1");
        await AuthTokenManager.SetTokenAsync(_testDirectory, "localhost:5001", "token2");

        // Assert
        var token1 = await AuthTokenManager.GetTokenAsync(_testDirectory, "localhost:5000");
        var token2 = await AuthTokenManager.GetTokenAsync(_testDirectory, "localhost:5001");

        Assert.Equal("token1", token1);
        Assert.Equal("token2", token2);
    }

    [Fact]
    public async Task EmptyToken_StoresAndRetrievesCorrectly()
    {
        // Arrange & Act
        await AuthTokenManager.SetTokenAsync(_testDirectory, "lrm.cloud", "");
        var result = await AuthTokenManager.GetTokenAsync(_testDirectory, "lrm.cloud");

        // Assert - Empty token should still be stored but HasToken returns false for empty strings
        // Based on the implementation, empty tokens are valid
        Assert.Equal("", result);
    }

    #endregion
}
