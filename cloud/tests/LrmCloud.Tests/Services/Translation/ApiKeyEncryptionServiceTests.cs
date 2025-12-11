using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services.Translation;

public class ApiKeyEncryptionServiceTests
{
    private readonly IApiKeyEncryptionService _encryptionService;
    private readonly CloudConfiguration _cloudConfiguration;

    public ApiKeyEncryptionServiceTests()
    {
        // Setup cloud configuration with a test master secret
        _cloudConfiguration = new CloudConfiguration
        {
            Server = new ServerConfiguration { Urls = "http://localhost:5000", Environment = "Test" },
            Database = new DatabaseConfiguration { ConnectionString = "test" },
            Redis = new RedisConfiguration { ConnectionString = "test" },
            Storage = new StorageConfiguration { Endpoint = "test", AccessKey = "test", SecretKey = "test", Bucket = "test" },
            Encryption = new EncryptionConfiguration { TokenKey = "test-token-key-for-unit-tests-123456" },
            Auth = new AuthConfiguration { JwtSecret = "test-jwt-secret-for-unit-tests-only-12345678901234567890" },
            Mail = new MailConfiguration { Host = "localhost", FromAddress = "test@test.com", FromName = "Test" },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration(),
            ApiKeyMasterSecret = "test-master-secret-for-unit-tests-only-12345",
            LrmProvider = new LrmProviderConfiguration()
        };

        _encryptionService = new ApiKeyEncryptionService(_cloudConfiguration);
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
        // Arrange - CloudConfiguration without ApiKeyMasterSecret
        var configWithoutSecret = new CloudConfiguration
        {
            Server = new ServerConfiguration { Urls = "http://localhost:5000", Environment = "Test" },
            Database = new DatabaseConfiguration { ConnectionString = "test" },
            Redis = new RedisConfiguration { ConnectionString = "test" },
            Storage = new StorageConfiguration { Endpoint = "test", AccessKey = "test", SecretKey = "test", Bucket = "test" },
            Encryption = new EncryptionConfiguration { TokenKey = "test-token-key-for-unit-tests-123456" },
            Auth = new AuthConfiguration { JwtSecret = "test-jwt-secret-for-unit-tests-only-12345678901234567890" },
            Mail = new MailConfiguration { Host = "localhost", FromAddress = "test@test.com", FromName = "Test" },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration(),
            ApiKeyMasterSecret = null, // No master secret
            LrmProvider = new LrmProviderConfiguration()
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ApiKeyEncryptionService(configWithoutSecret));
    }
}
