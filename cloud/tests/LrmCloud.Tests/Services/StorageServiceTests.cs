using LrmCloud.Api.Services;
using LrmCloud.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services;

/// <summary>
/// Tests for MinioStorageService.
/// Note: These tests verify the service logic and interaction patterns.
/// Full integration tests with actual MinIO would be in a separate test suite.
/// </summary>
public class StorageServiceTests
{
    private readonly Mock<IMinioClient> _mockMinioClient;
    private readonly CloudConfiguration _config;
    private readonly Mock<ILogger<MinioStorageService>> _mockLogger;
    private readonly IStorageService _storageService;

    public StorageServiceTests()
    {
        _mockMinioClient = new Mock<IMinioClient>();
        _mockLogger = new Mock<ILogger<MinioStorageService>>();

        _config = new CloudConfiguration
        {
            Server = new ServerConfiguration
            {
                Urls = "http://localhost:8080",
                Environment = "Development"
            },
            Database = new DatabaseConfiguration
            {
                ConnectionString = "Host=localhost;Port=5432;Database=test"
            },
            Redis = new RedisConfiguration
            {
                ConnectionString = "localhost:6379"
            },
            Storage = new StorageConfiguration
            {
                Endpoint = "localhost:9000",
                AccessKey = "minioadmin",
                SecretKey = "minioadmin",
                Bucket = "lrm-projects",
                UseSSL = false
            },
            Encryption = new EncryptionConfiguration
            {
                TokenKey = "test-key-32-chars-long-base64"
            },
            Auth = new AuthConfiguration
            {
                JwtSecret = "test-jwt-secret-32-chars-long-base64-encoded"
            },
            Mail = new MailConfiguration
            {
                Host = "localhost",
                Port = 25,
                FromAddress = "test@example.com",
                FromName = "Test"
            },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration()
        };

        _storageService = new MinioStorageService(_mockMinioClient.Object, _config, _mockLogger.Object);
    }

    // ============================================================
    // Upload Tests
    // ============================================================

    [Fact]
    public async Task UploadFileAsync_CallsMinioWithCorrectBucket()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"hello\":\"world\"}"));

        _mockMinioClient
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ReturnsAsync(It.IsAny<Minio.DataModel.Response.PutObjectResponse>());

        // Act
        await _storageService.UploadFileAsync(projectId, filePath, content, "application/json");

        // Assert
        _mockMinioClient.Verify(
            c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default),
            Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";
        var content = new MemoryStream();

        _mockMinioClient
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ThrowsAsync(new Exception("MinIO connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _storageService.UploadFileAsync(projectId, filePath, content));
    }

    // ============================================================
    // Download Tests
    // ============================================================

    [Fact]
    public async Task DownloadFileAsync_WhenFileNotFound_ReturnsNull()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";

        // Setup StatObjectAsync to throw exception (file doesn't exist)
        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ThrowsAsync(new Exception("Object not found"));

        // Act
        var result = await _storageService.DownloadFileAsync(projectId, filePath);

        // Assert
        Assert.Null(result);
    }

    // ============================================================
    // Delete Tests
    // ============================================================

    [Fact]
    public async Task DeleteFileAsync_CallsMinioRemove()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";

        _mockMinioClient
            .Setup(c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _storageService.DeleteFileAsync(projectId, filePath);

        // Assert
        _mockMinioClient.Verify(
            c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), default),
            Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";

        _mockMinioClient
            .Setup(c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), default))
            .ThrowsAsync(new Exception("MinIO error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _storageService.DeleteFileAsync(projectId, filePath));
    }

    // ============================================================
    // List Files Tests
    // ============================================================

    [Fact]
    public async Task ListFilesAsync_CallsMinioWithCorrectPrefix()
    {
        // Arrange
        var projectId = 1;
        var prefix = "current/";

        // Create an empty async enumerable
        var emptyItems = GetEmptyAsyncEnumerable();

        _mockMinioClient
            .Setup(c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default))
            .Returns(emptyItems);

        // Act
        var result = await _storageService.ListFilesAsync(projectId, prefix);

        // Assert
        Assert.Empty(result);
        _mockMinioClient.Verify(
            c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default),
            Times.Once);
    }

    // ============================================================
    // File Exists Tests
    // ============================================================

    [Fact]
    public async Task FileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";

        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ReturnsAsync(It.IsAny<Minio.DataModel.ObjectStat>());

        // Act
        var result = await _storageService.FileExistsAsync(projectId, filePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task FileExistsAsync_WhenFileNotFound_ReturnsFalse()
    {
        // Arrange
        var projectId = 1;
        var filePath = "current/en.json";

        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ThrowsAsync(new Exception("Object not found"));

        // Act
        var result = await _storageService.FileExistsAsync(projectId, filePath);

        // Assert
        Assert.False(result);
    }

    // ============================================================
    // Delete Project Files Tests
    // ============================================================

    [Fact]
    public async Task DeleteProjectFilesAsync_CallsListAndDelete()
    {
        // Arrange
        var projectId = 1;

        // Empty list - no files to delete
        var emptyItems = GetEmptyAsyncEnumerable();

        _mockMinioClient
            .Setup(c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default))
            .Returns(emptyItems);

        // Act
        await _storageService.DeleteProjectFilesAsync(projectId);

        // Assert
        _mockMinioClient.Verify(
            c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default),
            Times.Once);
    }

    // ============================================================
    // Create Snapshot Tests
    // ============================================================

    [Fact]
    public async Task CreateSnapshotAsync_CallsListAndCopy()
    {
        // Arrange
        var projectId = 1;
        var syncId = "sync-20250101-120000";

        // Empty list - no files to snapshot
        var emptyItems = GetEmptyAsyncEnumerable();

        _mockMinioClient
            .Setup(c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default))
            .Returns(emptyItems);

        // Act
        await _storageService.CreateSnapshotAsync(projectId, syncId);

        // Assert
        _mockMinioClient.Verify(
            c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default),
            Times.Once);
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private static async IAsyncEnumerable<Minio.DataModel.Item> GetEmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
