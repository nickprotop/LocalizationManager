using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.DTOs.Files;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// Alias to avoid conflict with LrmCloud.Shared.DTOs.Translation namespace
using TranslationEntity = LrmCloud.Shared.Entities.Translation;

namespace LrmCloud.Tests.Services;

public class FileOperationsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FileOperationsService _service;
    private readonly Mock<IFileImportService> _mockFileImportService;
    private readonly Mock<IFileExportService> _mockFileExportService;
    private readonly Mock<IKeySyncService> _mockKeySyncService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ILogger<FileOperationsService>> _mockLogger;

    public FileOperationsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        _mockFileImportService = new Mock<IFileImportService>();
        _mockFileExportService = new Mock<IFileExportService>();
        _mockKeySyncService = new Mock<IKeySyncService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockLogger = new Mock<ILogger<FileOperationsService>>();

        _service = new FileOperationsService(
            _db,
            _mockFileImportService.Object,
            _mockFileExportService.Object,
            _mockKeySyncService.Object,
            _mockProjectService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private async Task<User> CreateTestUserAsync()
    {
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Project> CreateTestProjectAsync(int userId)
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = userId,
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task CreateTestDataAsync(int projectId)
    {
        // Create some resource keys and translations
        var key1 = new ResourceKey
        {
            ProjectId = projectId,
            KeyName = "greeting",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var key2 = new ResourceKey
        {
            ProjectId = projectId,
            KeyName = "farewell",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ResourceKeys.AddRange(key1, key2);
        await _db.SaveChangesAsync();

        var translations = new[]
        {
            new TranslationEntity { ResourceKeyId = key1.Id, LanguageCode = "en", Value = "Hello", Status = "translated", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TranslationEntity { ResourceKeyId = key1.Id, LanguageCode = "fr", Value = "Bonjour", Status = "translated", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TranslationEntity { ResourceKeyId = key2.Id, LanguageCode = "en", Value = "Goodbye", Status = "translated", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TranslationEntity { ResourceKeyId = key2.Id, LanguageCode = "fr", Value = "Au revoir", Status = "translated", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _db.Translations.AddRange(translations);
        await _db.SaveChangesAsync();
    }

    // ============================================================
    // Import Tests
    // ============================================================

    [Fact]
    public async Task ImportFilesAsync_NoPermission_ReturnsError()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(false);

        var request = new FileImportRequest
        {
            Files = new List<FileDto>
            {
                new() { Path = "strings.json", Content = "{\"greeting\": \"Hello\"}" }
            },
            Format = "json"
        };

        // Act
        var result = await _service.ImportFilesAsync(project.Id, user.Id, request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("permission"));
    }

    [Fact]
    public async Task ImportFilesAsync_EmptyFiles_ReturnsError()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new FileImportRequest
        {
            Files = new List<FileDto>(),
            Format = "json"
        };

        // Act
        var result = await _service.ImportFilesAsync(project.Id, user.Id, request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("No files"));
    }

    [Fact]
    public async Task ImportFilesAsync_ValidFiles_CallsKeySyncService()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var parsedEntries = new Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry>
        {
            { ("greeting", "en", ""), new GitHubEntry { Key = "greeting", LanguageCode = "en", Value = "Hello", Hash = "abc123" } },
            { ("farewell", "en", ""), new GitHubEntry { Key = "farewell", LanguageCode = "en", Value = "Goodbye", Hash = "def456" } }
        };

        _mockFileImportService.Setup(x => x.ParseFiles(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string>()))
            .Returns(parsedEntries);

        _mockKeySyncService.Setup(x => x.PushAsync(
            project.Id,
            user.Id,
            It.IsAny<KeySyncPushRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KeySyncPushResponse { Applied = 2 });

        var request = new FileImportRequest
        {
            Files = new List<FileDto>
            {
                new() { Path = "strings.json", Content = "{\"greeting\": \"Hello\", \"farewell\": \"Goodbye\"}" }
            },
            Format = "json"
        };

        // Act
        var result = await _service.ImportFilesAsync(project.Id, user.Id, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Applied);

        _mockKeySyncService.Verify(x => x.PushAsync(
            project.Id,
            user.Id,
            It.Is<KeySyncPushRequest>(r => r.Entries.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportFilesAsync_AutoDetectsFormat_Json()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var parsedEntries = new Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry>
        {
            { ("test", "en", ""), new GitHubEntry { Key = "test", LanguageCode = "en", Value = "Test", Hash = "abc" } }
        };

        _mockFileImportService.Setup(x => x.ParseFiles(
            "json", // Should auto-detect json
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string>()))
            .Returns(parsedEntries);

        _mockKeySyncService.Setup(x => x.PushAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<KeySyncPushRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KeySyncPushResponse { Applied = 1 });

        var request = new FileImportRequest
        {
            Files = new List<FileDto>
            {
                new() { Path = "strings.json", Content = "{}" }
            }
            // No Format specified - should auto-detect
        };

        // Act
        var result = await _service.ImportFilesAsync(project.Id, user.Id, request);

        // Assert
        _mockFileImportService.Verify(x => x.ParseFiles(
            "json",
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string>()), Times.Once);
    }

    // ============================================================
    // Export Tests
    // ============================================================

    [Fact]
    public async Task ExportFilesAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ExportFilesAsync(project.Id, user.Id, "json", null));
    }

    [Fact]
    public async Task ExportFilesAsync_ValidRequest_ReturnsZipBytes()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var exportedFiles = new Dictionary<string, string>
        {
            { "./strings.json", "{\"greeting\": \"Hello\"}" },
            { "./strings.fr.json", "{\"greeting\": \"Bonjour\"}" }
        };

        _mockFileExportService.Setup(x => x.ExportProjectAsync(
            project.Id,
            ".",
            "json",
            It.IsAny<IEnumerable<string>?>()))
            .ReturnsAsync(exportedFiles);

        // Act
        var result = await _service.ExportFilesAsync(project.Id, user.Id, "json", null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        // Verify it's a valid ZIP file (starts with PK)
        Assert.Equal((byte)'P', result[0]);
        Assert.Equal((byte)'K', result[1]);
    }

    [Fact]
    public async Task ExportFilesAsync_WithLanguageFilter_PassesLanguagesToService()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var exportedFiles = new Dictionary<string, string>
        {
            { "./strings.json", "{}" }
        };

        _mockFileExportService.Setup(x => x.ExportProjectAsync(
            project.Id,
            ".",
            "json",
            It.Is<IEnumerable<string>?>(langs => langs != null && langs.Contains("en") && langs.Contains("fr"))))
            .ReturnsAsync(exportedFiles);

        var languages = new[] { "en", "fr" };

        // Act
        var result = await _service.ExportFilesAsync(project.Id, user.Id, "json", languages);

        // Assert
        _mockFileExportService.Verify(x => x.ExportProjectAsync(
            project.Id,
            ".",
            "json",
            It.Is<IEnumerable<string>?>(langs => langs != null && langs.Contains("en") && langs.Contains("fr"))),
            Times.Once);
    }

    // ============================================================
    // Export Preview Tests
    // ============================================================

    [Fact]
    public async Task GetExportPreviewAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetExportPreviewAsync(project.Id, user.Id, "json", null));
    }

    [Fact]
    public async Task GetExportPreviewAsync_ValidRequest_ReturnsPreview()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestDataAsync(project.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _service.GetExportPreviewAsync(project.Id, user.Id, "json", null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalKeys); // greeting and farewell
        Assert.True(result.Files.Count >= 2); // en and fr
    }

    [Fact]
    public async Task GetExportPreviewAsync_IncludesDefaultLanguage()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestDataAsync(project.Id);

        _mockProjectService.Setup(x => x.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Request only French, but should still include English (default)
        var languages = new[] { "fr" };

        // Act
        var result = await _service.GetExportPreviewAsync(project.Id, user.Id, "json", languages);

        // Assert
        Assert.Contains(result.Files, f => f.Language == "en" && f.IsDefault);
        Assert.Contains(result.Files, f => f.Language == "fr");
    }
}
