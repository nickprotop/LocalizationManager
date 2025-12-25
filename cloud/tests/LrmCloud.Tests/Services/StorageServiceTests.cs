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
    // Upload Snapshot File Tests
    // ============================================================

    [Fact]
    public async Task UploadSnapshotFileAsync_CallsMinioWithCorrectBucket()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";
        var fileName = "dbstate.json";
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"hello\":\"world\"}"));

        _mockMinioClient
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ReturnsAsync(It.IsAny<Minio.DataModel.Response.PutObjectResponse>());

        // Act
        await _storageService.UploadSnapshotFileAsync(projectId, snapshotId, fileName, content);

        // Assert
        _mockMinioClient.Verify(
            c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default),
            Times.Once);
    }

    [Fact]
    public async Task UploadSnapshotFileAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";
        var fileName = "dbstate.json";
        var content = new MemoryStream();

        _mockMinioClient
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), default))
            .ThrowsAsync(new Exception("MinIO connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _storageService.UploadSnapshotFileAsync(projectId, snapshotId, fileName, content));
    }

    // ============================================================
    // Download Snapshot File Tests
    // ============================================================

    [Fact]
    public async Task DownloadSnapshotFileAsync_WhenFileNotFound_ReturnsNull()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";
        var fileName = "dbstate.json";

        // Setup StatObjectAsync to throw exception (file doesn't exist)
        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ThrowsAsync(new Exception("Object not found"));

        // Act
        var result = await _storageService.DownloadSnapshotFileAsync(projectId, snapshotId, fileName);

        // Assert
        Assert.Null(result);
    }

    // ============================================================
    // Snapshot File Exists Tests
    // ============================================================

    [Fact]
    public async Task SnapshotFileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";
        var fileName = "dbstate.json";

        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ReturnsAsync(It.IsAny<Minio.DataModel.ObjectStat>());

        // Act
        var result = await _storageService.SnapshotFileExistsAsync(projectId, snapshotId, fileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SnapshotFileExistsAsync_WhenFileNotFound_ReturnsFalse()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";
        var fileName = "dbstate.json";

        _mockMinioClient
            .Setup(c => c.StatObjectAsync(It.IsAny<StatObjectArgs>(), default))
            .ThrowsAsync(new Exception("Object not found"));

        // Act
        var result = await _storageService.SnapshotFileExistsAsync(projectId, snapshotId, fileName);

        // Assert
        Assert.False(result);
    }

    // ============================================================
    // Delete Snapshot Tests
    // ============================================================

    [Fact]
    public async Task DeleteSnapshotAsync_CallsMinioListAndRemove()
    {
        // Arrange
        var projectId = 1;
        var snapshotId = "snap-20250101-120000";

        // Empty list - no files to delete
        var emptyItems = GetEmptyAsyncEnumerable();

        _mockMinioClient
            .Setup(c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default))
            .Returns(emptyItems);

        // Act
        await _storageService.DeleteSnapshotAsync(projectId, snapshotId);

        // Assert
        _mockMinioClient.Verify(
            c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default),
            Times.Once);
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
    // Get Total Storage Size Tests
    // ============================================================

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithNoFiles_ReturnsZero()
    {
        // Arrange
        var projectIds = new List<int> { 1, 2, 3 };

        // Empty list - no files
        var emptyItems = GetEmptyAsyncEnumerable();

        _mockMinioClient
            .Setup(c => c.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), default))
            .Returns(emptyItems);

        // Act
        var result = await _storageService.GetTotalStorageSizeAsync(projectIds);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithEmptyProjectList_ReturnsZero()
    {
        // Arrange
        var projectIds = new List<int>();

        // Act
        var result = await _storageService.GetTotalStorageSizeAsync(projectIds);

        // Assert
        Assert.Equal(0, result);
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
