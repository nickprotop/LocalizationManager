// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services;

public class OtaServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OtaService _service;
    private readonly User _testUser;

    public OtaServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        var loggerMock = new Mock<ILogger<OtaService>>();
        _service = new OtaService(_db, loggerMock.Object);

        // Create test user
        _testUser = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(_testUser);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private async Task<Project> CreateTestProjectAsync(int? userId = null, int? orgId = null)
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = userId,
            OrganizationId = orgId,
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task AddResourceKeyWithTranslationAsync(int projectId, string keyName, string language, string value, bool isPlural = false, string? pluralForm = null)
    {
        // Check if key exists
        var existingKey = await _db.ResourceKeys
            .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == keyName);

        if (existingKey == null)
        {
            existingKey = new ResourceKey
            {
                ProjectId = projectId,
                KeyName = keyName,
                IsPlural = isPlural,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ResourceKeys.Add(existingKey);
            await _db.SaveChangesAsync();
        }

        var translationEntity = new LrmCloud.Shared.Entities.Translation
        {
            ResourceKeyId = existingKey.Id,
            LanguageCode = language == "en" ? "" : language, // Default language stored as empty
            Value = value,
            PluralForm = pluralForm ?? "", // Empty string for non-plural translations
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Translations.Add(translationEntity);
        await _db.SaveChangesAsync();
    }

    #region GetBundleAsync Tests

    [Fact]
    public async Task GetBundleAsync_EmptyProject_ReturnsEmptyBundle()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.NotNull(bundle);
        Assert.Empty(bundle.Translations);
    }

    [Fact]
    public async Task GetBundleAsync_WithResources_ReturnsTranslations()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Welcome", "en", "Welcome!");
        await AddResourceKeyWithTranslationAsync(project.Id, "Welcome", "fr", "Bienvenue!");
        await AddResourceKeyWithTranslationAsync(project.Id, "Goodbye", "en", "Goodbye!");

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.NotNull(bundle);
        Assert.Contains("en", bundle.Languages);
        Assert.Contains("fr", bundle.Languages);
        Assert.Equal("Welcome!", bundle.Translations["en"]["Welcome"]);
        Assert.Equal("Bienvenue!", bundle.Translations["fr"]["Welcome"]);
    }

    [Fact]
    public async Task GetBundleAsync_WithLanguageFilter_FiltersLanguages()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Welcome", "en", "Welcome!");
        await AddResourceKeyWithTranslationAsync(project.Id, "Welcome", "fr", "Bienvenue!");
        await AddResourceKeyWithTranslationAsync(project.Id, "Welcome", "de", "Willkommen!");

        // Act
        var bundle = await _service.GetBundleAsync(
            project.Id,
            $"@{_testUser.Username}/{project.Slug}",
            languages: new[] { "fr", "de" });

        // Assert
        Assert.NotNull(bundle);
        Assert.Contains("fr", bundle.Languages);
        Assert.Contains("de", bundle.Languages);
        Assert.DoesNotContain("en", bundle.Languages);
    }

    [Fact]
    public async Task GetBundleAsync_SetsCorrectProjectPath()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        var projectPath = $"@{_testUser.Username}/{project.Slug}";

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, projectPath);

        // Assert
        Assert.Equal(projectPath, bundle!.Project);
    }

    [Fact]
    public async Task GetBundleAsync_SetsDefaultLanguage()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Key", "en", "Value");

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.Equal("en", bundle!.DefaultLanguage);
    }

    [Fact]
    public async Task GetBundleAsync_SetsVersionTimestamp()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        var beforeCall = DateTime.UtcNow;

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.NotNull(bundle!.Version);
        var version = DateTime.Parse(bundle.Version);
        Assert.True(version >= beforeCall.AddSeconds(-1));
    }

    [Fact]
    public async Task GetBundleAsync_NonExistentProject_ReturnsNull()
    {
        // Act
        var bundle = await _service.GetBundleAsync(99999, "@nonexistent/project");

        // Assert
        Assert.Null(bundle);
    }

    #endregion

    #region GetVersionAsync Tests

    [Fact]
    public async Task GetVersionAsync_ReturnsVersionDto()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Key", "en", "Value");

        // Act
        var version = await _service.GetVersionAsync(project.Id);

        // Assert
        Assert.NotNull(version);
        Assert.NotNull(version.Version);
    }

    [Fact]
    public async Task GetVersionAsync_NoResources_StillReturnsVersion()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);

        // Act
        var version = await _service.GetVersionAsync(project.Id);

        // Assert
        Assert.NotNull(version);
        Assert.NotNull(version.Version);
    }

    [Fact]
    public async Task GetVersionAsync_NonExistentProject_ReturnsNull()
    {
        // Act
        var version = await _service.GetVersionAsync(99999);

        // Assert
        Assert.Null(version);
    }

    #endregion

    #region ComputeETag Tests

    [Fact]
    public void ComputeETag_SameInput_ReturnsSameETag()
    {
        // Arrange
        var version = "2025-01-01T00:00:00Z";

        // Act
        var etag1 = _service.ComputeETag(version);
        var etag2 = _service.ComputeETag(version);

        // Assert
        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void ComputeETag_DifferentInput_ReturnsDifferentETag()
    {
        // Arrange
        var version1 = "2025-01-01T00:00:00Z";
        var version2 = "2025-01-02T00:00:00Z";

        // Act
        var etag1 = _service.ComputeETag(version1);
        var etag2 = _service.ComputeETag(version2);

        // Assert
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void ComputeETag_ReturnsNonEmptyString()
    {
        // Arrange
        var version = "2025-01-01T00:00:00Z";

        // Act
        var etag = _service.ComputeETag(version);

        // Assert
        Assert.False(string.IsNullOrEmpty(etag));
    }

    #endregion

    #region Nested Keys Tests

    [Fact]
    public async Task GetBundleAsync_NestedKeys_PreservesDotNotation()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Navigation.Home", "en", "Home");
        await AddResourceKeyWithTranslationAsync(project.Id, "Navigation.Settings", "en", "Settings");
        await AddResourceKeyWithTranslationAsync(project.Id, "Errors.NotFound", "en", "Not Found");

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.Equal("Home", bundle!.Translations["en"]["Navigation.Home"]);
        Assert.Equal("Settings", bundle.Translations["en"]["Navigation.Settings"]);
        Assert.Equal("Not Found", bundle.Translations["en"]["Errors.NotFound"]);
    }

    #endregion

    #region Plural Keys Tests

    [Fact]
    public async Task GetBundleAsync_PluralKeys_ReturnsAsObject()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await AddResourceKeyWithTranslationAsync(project.Id, "Items", "en", "{0} item", isPlural: true, pluralForm: "one");
        await AddResourceKeyWithTranslationAsync(project.Id, "Items", "en", "{0} items", isPlural: true, pluralForm: "other");

        // Act
        var bundle = await _service.GetBundleAsync(project.Id, $"@{_testUser.Username}/{project.Slug}");

        // Assert
        Assert.NotNull(bundle);
        Assert.Contains("Items", bundle.Translations["en"].Keys);
        var pluralDict = bundle.Translations["en"]["Items"] as Dictionary<string, string>;
        Assert.NotNull(pluralDict);
        Assert.Equal("{0} item", pluralDict["one"]);
        Assert.Equal("{0} items", pluralDict["other"]);
    }

    #endregion
}
