using LrmCloud.Api.Data;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services.Translation;

public class ApiKeyHierarchyServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IApiKeyEncryptionService _encryptionService;
    private readonly IApiKeyHierarchyService _hierarchyService;
    private readonly CloudConfiguration _cloudConfiguration;

    public ApiKeyHierarchyServiceTests()
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
            LrmProvider = new LrmProviderConfiguration()
        };

        _encryptionService = new ApiKeyEncryptionService(_cloudConfiguration);

        // Create a mock IConfiguration for ApiKeyHierarchyService
        var mockConfiguration = new Mock<IConfiguration>();
        _hierarchyService = new ApiKeyHierarchyService(_db, _encryptionService, mockConfiguration.Object);
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

    private async Task<Project> CreateTestProjectAsync(int? userId = null, int? orgId = null)
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = userId,
            OrganizationId = orgId,
            Format = "json",
            DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    // ============================================================
    // ResolveApiKeyAsync Tests
    // ============================================================

    [Fact]
    public async Task ResolveApiKey_ShouldReturnProjectKey_WhenProjectHasKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        await _hierarchyService.SetApiKeyAsync("google", "project-google-key", "project", project.Id);
        await _hierarchyService.SetApiKeyAsync("google", "user-google-key", "user", user.Id);

        // Act
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("google", project.Id, user.Id);

        // Assert
        Assert.Equal("project-google-key", apiKey);
        Assert.Equal("project", source);
    }

    [Fact]
    public async Task ResolveApiKey_ShouldReturnUserKey_WhenNoProjectKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        await _hierarchyService.SetApiKeyAsync("deepl", "user-deepl-key", "user", user.Id);

        // Act
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("deepl", project.Id, user.Id);

        // Assert
        Assert.Equal("user-deepl-key", apiKey);
        Assert.Equal("user", source);
    }

    [Fact]
    public async Task ResolveApiKey_ShouldReturnOrgKey_WhenNoUserOrProjectKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        var project = await CreateTestProjectAsync(orgId: org.Id);

        await _hierarchyService.SetApiKeyAsync("openai", "org-openai-key", "organization", org.Id);

        // Act
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("openai", project.Id, user.Id, org.Id);

        // Assert
        Assert.Equal("org-openai-key", apiKey);
        Assert.Equal("organization", source);
    }

    [Fact]
    public async Task ResolveApiKey_ShouldReturnNull_WhenNoKeysConfigured()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        // Act - no keys configured at any level
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("google", project.Id, user.Id);

        // Assert - should return null when no keys are configured
        Assert.Null(apiKey);
        Assert.Null(source);
    }

    [Fact]
    public async Task ResolveApiKey_ShouldReturnNull_WhenNoKeyConfigured()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        // Act - claude has no platform key configured
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("claude", project.Id, user.Id);

        // Assert
        Assert.Null(apiKey);
        Assert.Null(source);
    }

    [Fact]
    public async Task ResolveApiKey_ShouldBeCaseInsensitive()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _hierarchyService.SetApiKeyAsync("GOOGLE", "user-google-key", "user", user.Id);

        // Act
        var (apiKey, source) = await _hierarchyService.ResolveApiKeyAsync("google", userId: user.Id);

        // Assert
        Assert.Equal("user-google-key", apiKey);
        Assert.Equal("user", source);
    }

    // ============================================================
    // SetApiKeyAsync Tests
    // ============================================================

    [Fact]
    public async Task SetApiKey_ShouldCreateNewUserKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        await _hierarchyService.SetApiKeyAsync("deepl", "my-deepl-key", "user", user.Id);

        // Assert
        var savedKey = await _db.UserApiKeys.FirstOrDefaultAsync(k => k.UserId == user.Id && k.Provider == "deepl");
        Assert.NotNull(savedKey);

        var decrypted = _encryptionService.Decrypt(savedKey!.EncryptedKey!);
        Assert.Equal("my-deepl-key", decrypted);
    }

    [Fact]
    public async Task SetApiKey_ShouldUpdateExistingKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _hierarchyService.SetApiKeyAsync("deepl", "old-key", "user", user.Id);

        // Act
        await _hierarchyService.SetApiKeyAsync("deepl", "new-key", "user", user.Id);

        // Assert
        var keys = await _db.UserApiKeys.Where(k => k.UserId == user.Id && k.Provider == "deepl").ToListAsync();
        Assert.Single(keys);

        var decrypted = _encryptionService.Decrypt(keys[0].EncryptedKey!);
        Assert.Equal("new-key", decrypted);
    }

    [Fact]
    public async Task SetApiKey_ShouldCreateProjectKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(userId: user.Id);

        // Act
        await _hierarchyService.SetApiKeyAsync("openai", "project-openai-key", "project", project.Id);

        // Assert
        var savedKey = await _db.ProjectApiKeys.FirstOrDefaultAsync(k => k.ProjectId == project.Id && k.Provider == "openai");
        Assert.NotNull(savedKey);

        var decrypted = _encryptionService.Decrypt(savedKey!.EncryptedKey!);
        Assert.Equal("project-openai-key", decrypted);
    }

    [Fact]
    public async Task SetApiKey_ShouldCreateOrganizationKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        // Act
        await _hierarchyService.SetApiKeyAsync("claude", "org-claude-key", "organization", org.Id);

        // Assert
        var savedKey = await _db.OrganizationApiKeys.FirstOrDefaultAsync(k => k.OrganizationId == org.Id && k.Provider == "claude");
        Assert.NotNull(savedKey);

        var decrypted = _encryptionService.Decrypt(savedKey!.EncryptedKey!);
        Assert.Equal("org-claude-key", decrypted);
    }

    [Fact]
    public async Task SetApiKey_ShouldThrowForInvalidLevel()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _hierarchyService.SetApiKeyAsync("google", "key", "invalid", 1));
    }

    // ============================================================
    // RemoveApiKeyAsync Tests
    // ============================================================

    [Fact]
    public async Task RemoveApiKey_ShouldRemoveUserKey()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _hierarchyService.SetApiKeyAsync("deepl", "my-key", "user", user.Id);

        // Act
        var result = await _hierarchyService.RemoveApiKeyAsync("deepl", "user", user.Id);

        // Assert
        Assert.True(result);
        var key = await _db.UserApiKeys.FirstOrDefaultAsync(k => k.UserId == user.Id && k.Provider == "deepl");
        Assert.Null(key);
    }

    [Fact]
    public async Task RemoveApiKey_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var result = await _hierarchyService.RemoveApiKeyAsync("deepl", "user", user.Id);

        // Assert
        Assert.False(result);
    }

    // ============================================================
    // GetConfiguredProvidersAsync Tests
    // ============================================================

    [Fact]
    public async Task GetConfiguredProviders_ShouldReturnAllConfiguredProviders()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        var project = await CreateTestProjectAsync(orgId: org.Id);

        await _hierarchyService.SetApiKeyAsync("deepl", "user-deepl", "user", user.Id);
        await _hierarchyService.SetApiKeyAsync("openai", "org-openai", "organization", org.Id);
        await _hierarchyService.SetApiKeyAsync("claude", "project-claude", "project", project.Id);

        // Act
        var configured = await _hierarchyService.GetConfiguredProvidersAsync(project.Id, user.Id, org.Id);

        // Assert - only user, organization, and project keys (no platform keys anymore)
        Assert.Contains("deepl", configured.Keys);
        Assert.Contains("openai", configured.Keys);
        Assert.Contains("claude", configured.Keys);

        Assert.Equal("user", configured["deepl"]);
        Assert.Equal("organization", configured["openai"]);
        Assert.Equal("project", configured["claude"]);
    }
}
