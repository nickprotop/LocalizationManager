using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Resources;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using Translation = LrmCloud.Shared.Entities.Translation;

namespace LrmCloud.Tests.Services;

public class ResourceServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ResourceService _resourceService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ILogger<ResourceService>> _mockLogger;

    public ResourceServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        // Setup mocks
        _mockProjectService = new Mock<IProjectService>();
        _mockLogger = new Mock<ILogger<ResourceService>>();

        // Create mocks for services - pass null for ISyncHistoryService since tests don't use history functionality
        _resourceService = new ResourceService(_db, _mockProjectService.Object, null!, _mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private async Task<User> CreateTestUserAsync(string email = "test@example.com")
    {
        var user = new User
        {
            AuthType = "email",
            Email = email,
            Username = email.Split('@')[0],
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
            Format = ProjectFormat.Json,
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task<ResourceKey> CreateTestResourceKeyAsync(int projectId, string keyName = "test.key")
    {
        var key = new ResourceKey
        {
            ProjectId = projectId,
            KeyName = keyName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ResourceKeys.Add(key);
        await _db.SaveChangesAsync();
        return key;
    }

    // ============================================================
    // Resource Key CRUD Tests
    // ============================================================

    [Fact]
    public async Task CreateResourceKeyAsync_ValidRequest_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new CreateResourceKeyRequest
        {
            KeyName = "hello.world",
            Comment = "Test key"
        };

        // Act
        var (success, key, errorMessage) = await _resourceService.CreateResourceKeyAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(key);
        Assert.Null(errorMessage);
        Assert.Equal("hello.world", key.KeyName);
        Assert.Equal("Test key", key.Comment);
        Assert.Equal(1, key.Version);
    }

    [Fact]
    public async Task CreateResourceKeyAsync_WithDefaultLanguageValue_CreatesTranslation()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new CreateResourceKeyRequest
        {
            KeyName = "hello.world",
            DefaultLanguageValue = "Hello World"
        };

        // Act
        var (success, key, errorMessage) = await _resourceService.CreateResourceKeyAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(key);
        Assert.Equal(1, key.TranslationCount);

        // Verify translation was created
        var translation = await _db.Translations
            .FirstOrDefaultAsync(t => t.ResourceKeyId == key.Id && t.LanguageCode == "en");
        Assert.NotNull(translation);
        Assert.Equal("Hello World", translation.Value);
        Assert.Equal(TranslationStatus.Translated, translation.Status);
    }

    [Fact]
    public async Task CreateResourceKeyAsync_DuplicateKey_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestResourceKeyAsync(project.Id, "duplicate.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new CreateResourceKeyRequest
        {
            KeyName = "duplicate.key"
        };

        // Act
        var (success, key, errorMessage) = await _resourceService.CreateResourceKeyAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(key);
        Assert.Equal("Resource key 'duplicate.key' already exists", errorMessage);
    }

    [Fact]
    public async Task CreateResourceKeyAsync_NoPermission_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(false);

        var request = new CreateResourceKeyRequest
        {
            KeyName = "hello.world"
        };

        // Act
        var (success, key, errorMessage) = await _resourceService.CreateResourceKeyAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(key);
        Assert.Equal("You don't have permission to manage resources in this project", errorMessage);
    }

    [Fact]
    public async Task GetResourceKeysAsync_ReturnsAllKeys()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestResourceKeyAsync(project.Id, "key1");
        await CreateTestResourceKeyAsync(project.Id, "key2");
        await CreateTestResourceKeyAsync(project.Id, "key3");

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var keys = await _resourceService.GetResourceKeysAsync(project.Id, user.Id);

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Contains(keys, k => k.KeyName == "key1");
        Assert.Contains(keys, k => k.KeyName == "key2");
        Assert.Contains(keys, k => k.KeyName == "key3");
    }

    [Fact]
    public async Task GetResourceKeyAsync_WithTranslations_ReturnsDetail()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        // Add translations
        _db.Translations.AddRange(
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "en", Value = "Hello", UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "fr", Value = "Bonjour", UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.GetResourceKeyAsync(project.Id, "test.key", user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test.key", result.KeyName);
        Assert.Equal(2, result.Translations.Count);
        Assert.Contains(result.Translations, t => t.LanguageCode == "en");
        Assert.Contains(result.Translations, t => t.LanguageCode == "fr");
    }

    [Fact]
    public async Task UpdateResourceKeyAsync_ValidRequest_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateResourceKeyRequest
        {
            Comment = "Updated comment",
            IsPlural = true
        };

        // Act
        var (success, updatedKey, errorMessage) = await _resourceService.UpdateResourceKeyAsync(
            project.Id, "test.key", user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(updatedKey);
        Assert.Equal("Updated comment", updatedKey.Comment);
        Assert.True(updatedKey.IsPlural);
        Assert.Equal(2, updatedKey.Version); // Version should increment
    }

    [Fact]
    public async Task DeleteResourceKeyAsync_Success_CascadesToTranslations()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        // Add translations
        var translation = new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Hello",
            UpdatedAt = DateTime.UtcNow
        };
        _db.Translations.Add(translation);
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var (success, errorMessage) = await _resourceService.DeleteResourceKeyAsync(
            project.Id, "test.key", user.Id);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify key and translations are deleted
        var deletedKey = await _db.ResourceKeys.FindAsync(key.Id);
        Assert.Null(deletedKey);

        var deletedTranslation = await _db.Translations.FindAsync(translation.Id);
        Assert.Null(deletedTranslation);
    }

    // ============================================================
    // Translation Tests
    // ============================================================

    [Fact]
    public async Task UpdateTranslationAsync_NewTranslation_CreatesTranslation()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "Bonjour",
            Status = TranslationStatus.Translated
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "fr", user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(translation);
        Assert.Equal("Bonjour", translation.Value);
        Assert.Equal("fr", translation.LanguageCode);
        Assert.Equal(TranslationStatus.Translated, translation.Status);
        Assert.Equal(1, translation.Version);
    }

    [Fact]
    public async Task UpdateTranslationAsync_ExistingTranslation_UpdatesTranslation()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        var existingTranslation = new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Old Value",
            Status = TranslationStatus.Pending,
            Version = 1,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Translations.Add(existingTranslation);
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "New Value",
            Status = TranslationStatus.Translated,
            Version = 1 // Optimistic locking
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "en", user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(translation);
        Assert.Equal("New Value", translation.Value);
        Assert.Equal(TranslationStatus.Translated, translation.Status);
        Assert.Equal(2, translation.Version); // Version incremented
    }

    [Fact]
    public async Task UpdateTranslationAsync_VersionConflict_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        var existingTranslation = new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Current Value",
            Version = 5, // Current version is 5
            UpdatedAt = DateTime.UtcNow
        };
        _db.Translations.Add(existingTranslation);
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "New Value",
            Version = 3 // User has stale version
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "en", user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(translation);
        Assert.Contains("modified by another user", errorMessage);
    }

    [Fact]
    public async Task UpdateTranslationAsync_InvalidStatus_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "Test",
            Status = "invalid-status"
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "en", user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(translation);
        Assert.Contains("Invalid status", errorMessage);
    }

    [Fact]
    public async Task BulkUpdateTranslationsAsync_UpdatesMultipleTranslations()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestResourceKeyAsync(project.Id, "key1");
        await CreateTestResourceKeyAsync(project.Id, "key2");
        await CreateTestResourceKeyAsync(project.Id, "key3");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var updates = new List<BulkTranslationUpdate>
        {
            new BulkTranslationUpdate { KeyName = "key1", Value = "Value 1", Status = TranslationStatus.Translated },
            new BulkTranslationUpdate { KeyName = "key2", Value = "Value 2", Status = TranslationStatus.Translated },
            new BulkTranslationUpdate { KeyName = "key3", Value = "Value 3", Status = TranslationStatus.Reviewed }
        };

        // Act
        var (success, updatedCount, errorMessage) = await _resourceService.BulkUpdateTranslationsAsync(
            project.Id, "fr", user.Id, updates);

        // Assert
        Assert.True(success);
        Assert.Equal(3, updatedCount);
        Assert.Null(errorMessage);

        // Verify translations were created
        var translations = await _db.Translations
            .Where(t => t.LanguageCode == "fr")
            .ToListAsync();
        Assert.Equal(3, translations.Count);
    }

    [Fact]
    public async Task BulkUpdateTranslationsAsync_SkipsNonExistentKeys()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestResourceKeyAsync(project.Id, "key1");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var updates = new List<BulkTranslationUpdate>
        {
            new BulkTranslationUpdate { KeyName = "key1", Value = "Value 1", Status = TranslationStatus.Translated },
            new BulkTranslationUpdate { KeyName = "nonexistent", Value = "Value 2", Status = TranslationStatus.Translated }
        };

        // Act
        var (success, updatedCount, errorMessage) = await _resourceService.BulkUpdateTranslationsAsync(
            project.Id, "en", user.Id, updates);

        // Assert
        Assert.True(success);
        Assert.Equal(1, updatedCount); // Only 1 key exists
    }

    // ============================================================
    // Stats Tests
    // ============================================================

    [Fact]
    public async Task GetProjectStatsAsync_CalculatesCorrectly()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key1 = await CreateTestResourceKeyAsync(project.Id, "key1");
        var key2 = await CreateTestResourceKeyAsync(project.Id, "key2");

        // Add translations in different states
        _db.Translations.AddRange(
            // English translations
            new Shared.Entities.Translation { ResourceKeyId = key1.Id, LanguageCode = "en", Value = "Hello", Status = TranslationStatus.Translated, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key2.Id, LanguageCode = "en", Value = "Goodbye", Status = TranslationStatus.Approved, UpdatedAt = DateTime.UtcNow },

            // French translations
            new Shared.Entities.Translation { ResourceKeyId = key1.Id, LanguageCode = "fr", Value = "Bonjour", Status = TranslationStatus.Reviewed, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key2.Id, LanguageCode = "fr", Value = "", Status = TranslationStatus.Pending, UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var stats = await _resourceService.GetProjectStatsAsync(project.Id, user.Id);

        // Assert
        Assert.Equal(2, stats.TotalKeys);
        Assert.Equal(2, stats.Languages.Count);

        // English stats - both keys have non-empty translations
        var enStats = stats.Languages["en"];
        Assert.Equal(2, enStats.TranslatedCount); // Keys with non-empty values
        Assert.Equal(0, enStats.PendingCount);    // Keys without values
        Assert.Equal(100.0, enStats.CompletionPercentage); // 2 keys with values / 2 keys = 100%

        // French stats - key1 has value, key2 is empty
        var frStats = stats.Languages["fr"];
        Assert.Equal(1, frStats.TranslatedCount); // Keys with non-empty values
        Assert.Equal(1, frStats.PendingCount);    // Keys without values (key2 has empty string)
        Assert.Equal(50.0, frStats.CompletionPercentage); // 1 key with value / 2 keys = 50%

        // Overall completion
        Assert.Equal(75.0, stats.OverallCompletion); // Average of 100% and 50%
    }

    // ============================================================
    // Validation Tests
    // ============================================================

    [Fact]
    public async Task ValidateProjectAsync_EmptyProject_IsValid()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.ValidateProjectAsync(project.Id, user.Id);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ValidateProjectAsync_MissingTranslations_CreatesWarnings()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        // Add only English translation
        _db.Translations.Add(new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Hello",
            UpdatedAt = DateTime.UtcNow
        });

        // Add empty French translation
        _db.Translations.Add(new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "fr",
            Value = "",
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.ValidateProjectAsync(project.Id, user.Id);

        // Assert
        // Warnings don't make IsValid false, only errors do
        Assert.Contains(result.Issues, i => i.Severity == "warning" && i.LanguageCode == "fr");
    }

    [Fact]
    public async Task ValidateProjectAsync_PendingTranslations_CreatesInfoIssues()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _db.Translations.Add(new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Hello",
            Status = TranslationStatus.Pending,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.ValidateProjectAsync(project.Id, user.Id);

        // Assert
        Assert.Contains(result.Issues, i => i.Severity == "info" && i.Message.Contains("Pending review"));
    }

    [Fact]
    public async Task ValidateProjectAsync_EmptyKeyName_CreatesError()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        var badKey = new ResourceKey
        {
            ProjectId = project.Id,
            KeyName = "", // Empty key name
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ResourceKeys.Add(badKey);
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _resourceService.ValidateProjectAsync(project.Id, user.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Severity == "error" && i.Message.Contains("empty name"));
    }

    [Fact]
    public async Task ValidateProjectAsync_NoPermission_ReturnsInvalid()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(false);

        // Act
        var result = await _resourceService.ValidateProjectAsync(project.Id, user.Id);

        // Assert
        Assert.False(result.IsValid);
    }

    // ============================================================
    // Translation Status Workflow Tests
    // ============================================================

    [Fact]
    public async Task UpdateTranslationAsync_SetReviewedStatus_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "Reviewed translation",
            Status = TranslationStatus.Reviewed
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "fr", user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(translation);
        Assert.Equal(TranslationStatus.Reviewed, translation.Status);
    }

    [Fact]
    public async Task UpdateTranslationAsync_SetApprovedStatus_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new UpdateTranslationRequest
        {
            Value = "Approved translation",
            Status = TranslationStatus.Approved
        };

        // Act
        var (success, translation, errorMessage) = await _resourceService.UpdateTranslationAsync(
            project.Id, "test.key", "fr", user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(translation);
        Assert.Equal(TranslationStatus.Approved, translation.Status);
    }

    [Fact]
    public async Task BulkUpdateTranslationsAsync_MixedStatuses_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateTestResourceKeyAsync(project.Id, "key1");
        await CreateTestResourceKeyAsync(project.Id, "key2");
        await CreateTestResourceKeyAsync(project.Id, "key3");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var updates = new List<BulkTranslationUpdate>
        {
            new BulkTranslationUpdate { KeyName = "key1", Value = "Value 1", Status = TranslationStatus.Pending },
            new BulkTranslationUpdate { KeyName = "key2", Value = "Value 2", Status = TranslationStatus.Translated },
            new BulkTranslationUpdate { KeyName = "key3", Value = "Value 3", Status = TranslationStatus.Approved }
        };

        // Act
        var (success, updatedCount, errorMessage) = await _resourceService.BulkUpdateTranslationsAsync(
            project.Id, "fr", user.Id, updates);

        // Assert
        Assert.True(success);
        Assert.Equal(3, updatedCount);

        // Verify each status
        var translations = await _db.Translations
            .Where(t => t.LanguageCode == "fr")
            .ToListAsync();

        Assert.Contains(translations, t => t.Status == TranslationStatus.Pending);
        Assert.Contains(translations, t => t.Status == TranslationStatus.Translated);
        Assert.Contains(translations, t => t.Status == TranslationStatus.Approved);
    }

    [Fact]
    public async Task GetProjectStatsAsync_CountsByWorkflowStatus()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "key1");

        // Add translations with different workflow statuses
        _db.Translations.AddRange(
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "en", Value = "Pending", Status = TranslationStatus.Pending, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "fr", Value = "Translated", Status = TranslationStatus.Translated, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "de", Value = "Reviewed", Status = TranslationStatus.Reviewed, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "es", Value = "Approved", Status = TranslationStatus.Approved, UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var stats = await _resourceService.GetProjectStatsAsync(project.Id, user.Id);

        // Assert
        Assert.Equal(1, stats.TotalKeys);
        Assert.Equal(4, stats.Languages.Count);
    }

    // ============================================================
    // Language Management Tests
    // ============================================================

    [Fact]
    public async Task GetProjectLanguagesAsync_ReturnsAllLanguages()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _db.Translations.AddRange(
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "en", Value = "English", UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "fr", Value = "French", UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key.Id, LanguageCode = "de", Value = "German", UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanViewProjectAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        // Act
        var languages = await _resourceService.GetProjectLanguagesAsync(project.Id, user.Id);

        // Assert
        Assert.Equal(3, languages.Count);
        Assert.Contains(languages, l => l.LanguageCode == "en");
        Assert.Contains(languages, l => l.LanguageCode == "fr");
        Assert.Contains(languages, l => l.LanguageCode == "de");
    }

    [Fact]
    public async Task AddLanguageAsync_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new AddLanguageRequest { LanguageCode = "es" };

        // Act
        var (success, language, errorMessage) = await _resourceService.AddLanguageAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(language);
        Assert.Equal("es", language.LanguageCode);
    }

    [Fact]
    public async Task RemoveLanguageAsync_DeletesTranslations()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var key = await CreateTestResourceKeyAsync(project.Id, "test.key");

        _db.Translations.Add(new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "fr",
            Value = "French value",
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _mockProjectService.Setup(s => s.CanManageResourcesAsync(project.Id, user.Id))
            .ReturnsAsync(true);

        var request = new RemoveLanguageRequest { LanguageCode = "fr", ConfirmDelete = true };

        // Act
        var (success, errorMessage) = await _resourceService.RemoveLanguageAsync(
            project.Id, user.Id, request);

        // Assert
        Assert.True(success);

        var remainingTranslations = await _db.Translations
            .Where(t => t.ResourceKey!.ProjectId == project.Id && t.LanguageCode == "fr")
            .CountAsync();
        Assert.Equal(0, remainingTranslations);
    }
}
