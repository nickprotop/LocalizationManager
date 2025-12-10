using LrmCloud.Api.Services.Translation;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services.Translation;

public class ApiKeyEncryptionServiceTests
{
    private readonly IApiKeyEncryptionService _encryptionService;

    public ApiKeyEncryptionServiceTests()
    {
        // Setup configuration with a test master secret
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ApiKeyMasterSecret", "test-master-secret-for-unit-tests-only-12345" }
            })
            .Build();

        _encryptionService = new ApiKeyEncryptionService(configuration);
    }

    [Fact]
    public void Encrypt_ShouldReturnBase64String()
    {
        // Arrange
        var plainKey = "sk-test-api-key-12345";

        // Act
        var encrypted = _encryptionService.Encrypt(plainKey);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.NotEqual(plainKey, encrypted);

        // Should be valid base64
        var bytes = Convert.FromBase64String(encrypted);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Decrypt_ShouldReturnOriginalKey()
    {
        // Arrange
        var plainKey = "sk-test-api-key-12345";
        var encrypted = _encryptionService.Encrypt(plainKey);

        // Act
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainKey, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_ShouldWorkWithSpecialCharacters()
    {
        // Arrange
        var plainKey = "sk-test_key!@#$%^&*()+={}[]|\\:\";<>,.?/~`";

        // Act
        var encrypted = _encryptionService.Encrypt(plainKey);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainKey, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_ShouldWorkWithLongKeys()
    {
        // Arrange
        var plainKey = new string('x', 1000); // 1000 character key

        // Act
        var encrypted = _encryptionService.Encrypt(plainKey);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainKey, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_ShouldWorkWithUnicodeCharacters()
    {
        // Arrange
        var plainKey = "sk-测试密钥-κλειδί-مفتاح-🔑";

        // Act
        var encrypted = _encryptionService.Encrypt(plainKey);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainKey, decrypted);
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentOutputsForSameInput()
    {
        // Due to random IV, encrypting the same text twice should produce different results
        var plainKey = "sk-test-api-key-12345";

        // Act
        var encrypted1 = _encryptionService.Encrypt(plainKey);
        var encrypted2 = _encryptionService.Encrypt(plainKey);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);

        // But both should decrypt to the same value
        Assert.Equal(plainKey, _encryptionService.Decrypt(encrypted1));
        Assert.Equal(plainKey, _encryptionService.Decrypt(encrypted2));
    }

    [Fact]
    public void Encrypt_ShouldThrowForNullOrEmptyKey()
    {
        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.Encrypt(""));
    }

    [Fact]
    public void Decrypt_ShouldThrowForInvalidInput()
    {
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(null!));
        Assert.Throws<ArgumentException>(() => _encryptionService.Decrypt(""));
        Assert.ThrowsAny<Exception>(() => _encryptionService.Decrypt("not-valid-base64!@#"));
    }

    [Fact]
    public void Mask_ShouldShowFirstAndLastFourChars()
    {
        // Arrange
        var plainKey = "sk-test-api-key-12345";

        // Act
        var masked = _encryptionService.Mask(plainKey);

        // Assert
        Assert.Equal("sk-t...2345", masked);
    }

    [Fact]
    public void Mask_ShouldHandleShortKeys()
    {
        Assert.Equal("****", _encryptionService.Mask("short"));
        Assert.Equal("****", _encryptionService.Mask("12345678"));
    }

    [Fact]
    public void Mask_ShouldHandleEmptyOrNullKeys()
    {
        Assert.Equal("(empty)", _encryptionService.Mask(null!));
        Assert.Equal("(empty)", _encryptionService.Mask(""));
    }

    [Fact]
    public void Constructor_ShouldThrowWithoutMasterSecret()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ApiKeyEncryptionService(configuration));
    }
}
