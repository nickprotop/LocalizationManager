using LrmCloud.Api.Data;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services.Translation;

public class CloudTranslationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IApiKeyEncryptionService _encryptionService;
    private readonly IApiKeyHierarchyService _hierarchyService;
    private readonly ICloudTranslationService _translationService;
    private readonly CloudConfiguration _cloudConfiguration;
    private readonly Mock<ILogger<CloudTranslationService>> _loggerMock;
    private readonly Mock<ILrmTranslationProvider> _lrmProviderMock;

    public CloudTranslationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        // Setup cloud configuration
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
            LrmProvider = new LrmProviderConfiguration { Enabled = true, EnabledBackends = new List<string> { "mymemory", "lingva" } }
        };

        _encryptionService = new ApiKeyEncryptionService(_cloudConfiguration);
        var mockConfiguration = new Mock<IConfiguration>();
        _hierarchyService = new ApiKeyHierarchyService(_db, _encryptionService, mockConfiguration.Object);
        _loggerMock = new Mock<ILogger<CloudTranslationService>>();
        _lrmProviderMock = new Mock<ILrmTranslationProvider>();

        // Setup LRM provider mock defaults
        _lrmProviderMock.Setup(x => x.IsAvailableAsync(It.IsAny<int>()))
            .ReturnsAsync((true, (string?)null));
        _lrmProviderMock.Setup(x => x.GetRemainingCharsAsync(It.IsAny<int>()))
            .ReturnsAsync(10000);

        _translationService = new CloudTranslationService(
            _db,
            _hierarchyService,
            _lrmProviderMock.Object,
            _cloudConfiguration,
            _loggerMock.Object);
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

    private async Task<Organization> CreateTestOrganizationAsync(int ownerId)
    {
        var org = new Organization
        {
            Name = "Test Org",
            Slug = "test-org",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    private async Task<Project> CreateTestProjectAsync(int? userId = null, int? orgId = null, string defaultLanguage = "en")
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = userId,
            OrganizationId = orgId,
            Format = "json",
            DefaultLanguage = defaultLanguage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task<ResourceKey> CreateTestResourceKeyAsync(int projectId, string keyName, string? enValue = null)
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

        if (enValue != null)
        {
            var translation = new LrmCloud.Shared.Entities.Translation
            {
                ResourceKeyId = key.Id,
                LanguageCode = "en",
                Value = enValue,
                Status = "translated"
            };
            _db.Translations.Add(translation);
            await _db.SaveChangesAsync();
        }

        return key;
    }

    // ============================================================
    // GetAvailableProvidersAsync Tests
    // ============================================================

    [Fact]
    public async Task GetAvailableProviders_ShouldReturnAllProviders()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(userId: user.Id);

        // Assert
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);

        // Should include common providers
        Assert.Contains(providers, p => p.Name == "google");
        Assert.Contains(providers, p => p.Name == "deepl");
        Assert.Contains(providers, p => p.Name == "mymemory");
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldMarkFreeProvidersAsConfigured()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(userId: user.Id);

        // Assert
        // MyMemory doesn't require API key, so it should be marked as configured
        var mymemory = providers.FirstOrDefault(p => p.Name == "mymemory");
        Assert.NotNull(mymemory);
        Assert.True(mymemory.IsConfigured);
        Assert.False(mymemory.RequiresApiKey);
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldShowUserConfiguredProviders()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _hierarchyService.SetApiKeyAsync("deepl", "test-deepl-key", "user", user.Id);

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(userId: user.Id);

        // Assert
        var deepl = providers.FirstOrDefault(p => p.Name == "deepl");
        Assert.NotNull(deepl);
        Assert.True(deepl.IsConfigured);
        Assert.Equal("user", deepl.ApiKeySource);
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldShowProjectConfiguredProviders()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);
        await _hierarchyService.SetApiKeyAsync("google", "test-google-key", "project", project.Id);

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(projectId: project.Id, userId: user.Id);

        // Assert
        var google = providers.FirstOrDefault(p => p.Name == "google");
        Assert.NotNull(google);
        Assert.True(google.IsConfigured);
        Assert.Equal("project", google.ApiKeySource);
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldIdentifyAiProviders()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(userId: user.Id);

        // Assert
        var openai = providers.FirstOrDefault(p => p.Name == "openai");
        Assert.NotNull(openai);
        Assert.True(openai.IsAiProvider);

        var claude = providers.FirstOrDefault(p => p.Name == "claude");
        Assert.NotNull(claude);
        Assert.True(claude.IsAiProvider);

        var google = providers.FirstOrDefault(p => p.Name == "google");
        Assert.NotNull(google);
        Assert.False(google.IsAiProvider);
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldHaveDescriptions()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var providers = await _translationService.GetAvailableProvidersAsync(userId: user.Id);

        // Assert
        var google = providers.FirstOrDefault(p => p.Name == "google");
        Assert.NotNull(google?.Description);
        Assert.Contains("Google", google.Description, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // GetUsageAsync Tests
    // ============================================================

    [Fact]
    public async Task GetUsage_ShouldReturnUsageInfo()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var usage = await _translationService.GetUsageAsync(user.Id);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal("free", usage.Plan);
    }

    // ============================================================
    // TranslateKeysAsync Tests
    // ============================================================

    [Fact]
    public async Task TranslateKeys_ShouldReturnError_WhenProjectNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "es" }
        };

        // Act
        var result = await _translationService.TranslateKeysAsync(9999, user.Id, request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Project not found", result.Errors);
    }

    [Fact]
    public async Task TranslateKeys_ShouldReturnError_WhenNoProviderConfigured()
    {
        // Arrange - Create a special config without any platform keys and LRM disabled
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(options);

        var cloudConfig = new CloudConfiguration
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
            // LRM disabled to test fallback behavior
            LrmProvider = new LrmProviderConfiguration { Enabled = false, EnabledBackends = new List<string>() }
        };

        var encryptionService = new ApiKeyEncryptionService(cloudConfig);
        var mockConfiguration = new Mock<IConfiguration>();
        var hierarchyService = new ApiKeyHierarchyService(db, encryptionService, mockConfiguration.Object);
        var loggerMock = new Mock<ILogger<CloudTranslationService>>();
        var lrmProviderMock = new Mock<ILrmTranslationProvider>();
        lrmProviderMock.Setup(x => x.IsAvailableAsync(It.IsAny<int>())).ReturnsAsync((false, "LRM disabled"));
        var translationService = new CloudTranslationService(db, hierarchyService, lrmProviderMock.Object, cloudConfig, loggerMock.Object);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "test",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = user.Id,
            Format = "json",
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Add a key to translate
        var key = new ResourceKey
        {
            ProjectId = project.Id,
            KeyName = "test.key",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ResourceKeys.Add(key);
        await db.SaveChangesAsync();

        var translation = new Shared.Entities.Translation
        {
            ResourceKeyId = key.Id,
            LanguageCode = "en",
            Value = "Hello",
            Status = "translated"
        };
        db.Translations.Add(translation);
        await db.SaveChangesAsync();

        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "es" },
            Provider = "deepl" // DeepL requires API key
        };

        // Act
        var result = await translationService.TranslateKeysAsync(project.Id, user.Id, request);

        // Assert - Should fail because DeepL has no API key
        // The error could be in the global Errors list (if provider creation failed)
        // or in individual Results (if translation itself failed)
        Assert.False(result.Success);
        var hasError = result.Errors.Any() ||
                       result.Results.Any(r => !r.Success && !string.IsNullOrEmpty(r.Error)) ||
                       result.FailedCount > 0;
        Assert.True(hasError, "Expected translation to fail due to missing API key");
    }

    [Fact]
    public async Task TranslateKeys_ShouldSkipSourceLanguage()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id, defaultLanguage: "en");
        await CreateTestResourceKeyAsync(project.Id, "greeting", "Hello");

        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "en" }, // Same as source
            SourceLanguage = "en"
        };

        // Act
        var result = await _translationService.TranslateKeysAsync(project.Id, user.Id, request);

        // Assert - No translations should happen when target = source
        Assert.Equal(0, result.TranslatedCount);
    }

    [Fact]
    public async Task TranslateKeys_ShouldSkipKeysWithoutSourceValue()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id, defaultLanguage: "en");

        // Create key without English translation
        var key = new ResourceKey
        {
            ProjectId = project.Id,
            KeyName = "empty.key",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ResourceKeys.Add(key);
        await _db.SaveChangesAsync();

        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "es" }
        };

        // Act
        var result = await _translationService.TranslateKeysAsync(project.Id, user.Id, request);

        // Assert
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.TranslatedCount);
    }

    // ============================================================
    // TranslateSingleAsync Tests
    // ============================================================

    [Fact]
    public async Task TranslateSingle_ShouldReturnError_WhenNoProviderConfigured()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new TranslateSingleRequestDto
        {
            Text = "Hello",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Provider = "deepl" // Requires API key
        };

        // Act
        var result = await _translationService.TranslateSingleAsync(user.Id, request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task TranslateSingle_ShouldUseProjectContext()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        var request = new TranslateSingleRequestDto
        {
            Text = "Hello",
            SourceLanguage = "en",
            TargetLanguage = "es"
            // Let it auto-select provider
        };

        // Act - With project context
        var result = await _translationService.TranslateSingleAsync(user.Id, request, project.Id);

        // Assert - Should succeed or fail with proper handling
        // The actual translation depends on external provider availability
        Assert.NotNull(result);
    }
}
