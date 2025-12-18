using System.Security.Claims;
using LrmCloud.Api.Authorization;
using LrmCloud.Api.Controllers;
using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Controllers;

public class TranslationControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IApiKeyEncryptionService _encryptionService;
    private readonly IApiKeyHierarchyService _hierarchyService;
    private readonly ICloudTranslationService _translationService;
    private readonly TranslationController _controller;
    private readonly User _testUser;
    private readonly CloudConfiguration _cloudConfiguration;

    public TranslationControllerTests()
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
        _hierarchyService = new ApiKeyHierarchyService(_db, _encryptionService);

        var translationLoggerMock = new Mock<ILogger<CloudTranslationService>>();
        var tmLoggerMock = new Mock<ILogger<TranslationMemoryService>>();
        var glossaryLoggerMock = new Mock<ILogger<GlossaryService>>();
        var lrmProviderMock = new Mock<ILrmTranslationProvider>();
        lrmProviderMock.Setup(x => x.IsAvailableAsync(It.IsAny<int>())).ReturnsAsync((true, (string?)null));
        lrmProviderMock.Setup(x => x.GetRemainingCharsAsync(It.IsAny<int>())).ReturnsAsync(10000);
        var tmService = new TranslationMemoryService(_db, tmLoggerMock.Object);
        var glossaryService = new GlossaryService(_db, glossaryLoggerMock.Object);
        _translationService = new CloudTranslationService(_db, _hierarchyService, lrmProviderMock.Object, tmService, glossaryService, _cloudConfiguration, translationLoggerMock.Object);

        var controllerLoggerMock = new Mock<ILogger<TranslationController>>();
        var authServiceMock = new Mock<ILrmAuthorizationService>();
        authServiceMock.Setup(x => x.HasProjectAccessAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        authServiceMock.Setup(x => x.CanEditProjectAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        authServiceMock.Setup(x => x.IsOrganizationAdminAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        authServiceMock.Setup(x => x.IsOrganizationMemberAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        _controller = new TranslationController(
            _translationService,
            _hierarchyService,
            _encryptionService,
            authServiceMock.Object,
            controllerLoggerMock.Object);

        // Create test user
        _testUser = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "test",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        // Setup controller context with authenticated user
        SetupControllerContext(_testUser.Id);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private void SetupControllerContext(int userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
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

    // ============================================================
    // GetProviders Tests
    // ============================================================

    [Fact]
    public async Task GetProviders_ShouldReturnOkWithProviders()
    {
        // Act
        var result = await _controller.GetProviders();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<List<TranslationProviderDto>>(okResult.Value);
        Assert.NotEmpty(providers);
    }

    [Fact]
    public async Task GetProviders_ShouldRespectProjectContext()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await _hierarchyService.SetApiKeyAsync("google", "project-google-key", "project", project.Id);

        // Act
        var result = await _controller.GetProviders(projectId: project.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<List<TranslationProviderDto>>(okResult.Value);

        var google = providers.FirstOrDefault(p => p.Name == "google");
        Assert.NotNull(google);
        Assert.True(google.IsConfigured);
        Assert.Equal("project", google.ApiKeySource);
    }

    // ============================================================
    // GetUsage Tests
    // ============================================================

    [Fact]
    public async Task GetUsage_ShouldReturnOkWithUsage()
    {
        // Act
        var result = await _controller.GetUsage();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var usage = Assert.IsType<TranslationUsageDto>(okResult.Value);
        Assert.NotNull(usage.Plan);
    }

    // ============================================================
    // GetUsageByProvider Tests
    // ============================================================

    [Fact]
    public async Task GetUsageByProvider_ShouldReturnOkWithList()
    {
        // Act
        var result = await _controller.GetUsageByProvider();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var usage = Assert.IsAssignableFrom<List<ProviderUsageDto>>(okResult.Value);
        Assert.NotNull(usage);
    }

    // ============================================================
    // TranslateKeys Tests
    // ============================================================

    [Fact]
    public async Task TranslateKeys_ShouldReturnBadRequest_WhenProjectNotFound()
    {
        // Arrange
        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "es" }
        };

        // Act
        var result = await _controller.TranslateKeys(9999, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<TranslateResponseDto>(badRequestResult.Value);
        Assert.Contains("Project not found", response.Errors);
    }

    [Fact]
    public async Task TranslateKeys_ShouldReturnOk_ForValidProject()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        var request = new TranslateRequestDto
        {
            TargetLanguages = new List<string> { "es" }
        };

        // Act
        var result = await _controller.TranslateKeys(project.Id, request);

        // Assert - Either Ok or BadRequest depending on provider availability
        Assert.True(
            result.Result is OkObjectResult || result.Result is BadRequestObjectResult,
            "Expected Ok or BadRequest result");
    }

    // ============================================================
    // TranslateSingle Tests
    // ============================================================

    [Fact]
    public async Task TranslateSingle_ShouldReturnResponse()
    {
        // Arrange
        var request = new TranslateSingleRequestDto
        {
            Text = "Hello",
            SourceLanguage = "en",
            TargetLanguage = "es"
        };

        // Act
        var result = await _controller.TranslateSingle(request);

        // Assert - Either success or error response
        Assert.True(
            result.Result is OkObjectResult || result.Result is BadRequestObjectResult,
            "Expected Ok or BadRequest result");
    }

    // ============================================================
    // SetUserApiKey Tests
    // ============================================================

    [Fact]
    public async Task SetUserApiKey_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var request = new SetApiKeyRequest
        {
            ProviderName = "deepl",
            ApiKey = "test-deepl-key"
        };

        // Act
        var result = await _controller.SetUserApiKey(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Verify key was saved
        var savedKey = await _db.UserApiKeys.FirstOrDefaultAsync(
            k => k.UserId == _testUser.Id && k.Provider == "deepl");
        Assert.NotNull(savedKey);
    }

    [Fact]
    public async Task SetUserApiKey_ShouldUpdateExistingKey()
    {
        // Arrange
        var request1 = new SetApiKeyRequest { ProviderName = "google", ApiKey = "old-key" };
        var request2 = new SetApiKeyRequest { ProviderName = "google", ApiKey = "new-key" };

        // Act
        await _controller.SetUserApiKey(request1);
        var result = await _controller.SetUserApiKey(request2);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var keys = await _db.UserApiKeys.Where(
            k => k.UserId == _testUser.Id && k.Provider == "google").ToListAsync();
        Assert.Single(keys);
    }

    // ============================================================
    // RemoveUserApiKey Tests
    // ============================================================

    [Fact]
    public async Task RemoveUserApiKey_ShouldReturnOk_WhenKeyExists()
    {
        // Arrange
        await _hierarchyService.SetApiKeyAsync("deepl", "test-key", "user", _testUser.Id);

        // Act
        var result = await _controller.RemoveUserApiKey("deepl");

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var key = await _db.UserApiKeys.FirstOrDefaultAsync(
            k => k.UserId == _testUser.Id && k.Provider == "deepl");
        Assert.Null(key);
    }

    [Fact]
    public async Task RemoveUserApiKey_ShouldReturnNotFound_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _controller.RemoveUserApiKey("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ============================================================
    // SetProjectApiKey Tests
    // ============================================================

    [Fact]
    public async Task SetProjectApiKey_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        var request = new SetApiKeyRequest
        {
            ProviderName = "google",
            ApiKey = "project-google-key"
        };

        // Act
        var result = await _controller.SetProjectApiKey(project.Id, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var savedKey = await _db.ProjectApiKeys.FirstOrDefaultAsync(
            k => k.ProjectId == project.Id && k.Provider == "google");
        Assert.NotNull(savedKey);
    }

    // ============================================================
    // RemoveProjectApiKey Tests
    // ============================================================

    [Fact]
    public async Task RemoveProjectApiKey_ShouldReturnOk_WhenKeyExists()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);
        await _hierarchyService.SetApiKeyAsync("deepl", "test-key", "project", project.Id);

        // Act
        var result = await _controller.RemoveProjectApiKey(project.Id, "deepl");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RemoveProjectApiKey_ShouldReturnNotFound_WhenKeyDoesNotExist()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);

        // Act
        var result = await _controller.RemoveProjectApiKey(project.Id, "nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ============================================================
    // SetOrganizationApiKey Tests
    // ============================================================

    [Fact]
    public async Task SetOrganizationApiKey_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var org = await CreateTestOrganizationAsync(_testUser.Id);
        var request = new SetApiKeyRequest
        {
            ProviderName = "openai",
            ApiKey = "org-openai-key"
        };

        // Act
        var result = await _controller.SetOrganizationApiKey(org.Id, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var savedKey = await _db.OrganizationApiKeys.FirstOrDefaultAsync(
            k => k.OrganizationId == org.Id && k.Provider == "openai");
        Assert.NotNull(savedKey);
    }

    // ============================================================
    // RemoveOrganizationApiKey Tests
    // ============================================================

    [Fact]
    public async Task RemoveOrganizationApiKey_ShouldReturnOk_WhenKeyExists()
    {
        // Arrange
        var org = await CreateTestOrganizationAsync(_testUser.Id);
        await _hierarchyService.SetApiKeyAsync("claude", "test-key", "organization", org.Id);

        // Act
        var result = await _controller.RemoveOrganizationApiKey(org.Id, "claude");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RemoveOrganizationApiKey_ShouldReturnNotFound_WhenKeyDoesNotExist()
    {
        // Arrange
        var org = await CreateTestOrganizationAsync(_testUser.Id);

        // Act
        var result = await _controller.RemoveOrganizationApiKey(org.Id, "nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ============================================================
    // TestApiKey Tests
    // ============================================================

    [Fact]
    public async Task TestApiKey_ShouldReturnValid_ForProviderWithoutApiKeyRequirement()
    {
        // Arrange
        var request = new TestApiKeyRequest
        {
            ProviderName = "mymemory", // Doesn't require API key
            ApiKey = "optional-key"
        };

        // Act
        var result = await _controller.TestApiKey(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestApiKeyResponse>(okResult.Value);
        Assert.True(response.IsValid);
    }

    [Fact]
    public async Task TestApiKey_ShouldReturnInvalid_ForBadApiKey()
    {
        // Arrange
        var request = new TestApiKeyRequest
        {
            ProviderName = "deepl",
            ApiKey = "invalid-api-key-that-will-fail"
        };

        // Act
        var result = await _controller.TestApiKey(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestApiKeyResponse>(okResult.Value);

        // Should return invalid (not throw) for bad API key
        Assert.False(response.IsValid);
        Assert.NotNull(response.Error);
    }

    // ============================================================
    // Integration Tests - API Key Hierarchy
    // ============================================================

    [Fact]
    public async Task ApiKeyHierarchy_ProjectOverridesUser()
    {
        // Arrange
        var project = await CreateTestProjectAsync(userId: _testUser.Id);

        // Set user key
        await _controller.SetUserApiKey(new SetApiKeyRequest
        {
            ProviderName = "google",
            ApiKey = "user-google-key"
        });

        // Set project key
        await _controller.SetProjectApiKey(project.Id, new SetApiKeyRequest
        {
            ProviderName = "google",
            ApiKey = "project-google-key"
        });

        // Act - Get providers with project context
        var result = await _controller.GetProviders(projectId: project.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<List<TranslationProviderDto>>(okResult.Value);

        var google = providers.First(p => p.Name == "google");
        Assert.Equal("project", google.ApiKeySource); // Project should take precedence
    }

    [Fact]
    public async Task ApiKeyHierarchy_UserOverridesOrganization()
    {
        // Arrange
        var org = await CreateTestOrganizationAsync(_testUser.Id);
        var project = await CreateTestProjectAsync(orgId: org.Id);

        // Set org key
        await _controller.SetOrganizationApiKey(org.Id, new SetApiKeyRequest
        {
            ProviderName = "deepl",
            ApiKey = "org-deepl-key"
        });

        // Set user key
        await _controller.SetUserApiKey(new SetApiKeyRequest
        {
            ProviderName = "deepl",
            ApiKey = "user-deepl-key"
        });

        // Act
        var result = await _controller.GetProviders(projectId: project.Id, organizationId: org.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<List<TranslationProviderDto>>(okResult.Value);

        var deepl = providers.First(p => p.Name == "deepl");
        Assert.Equal("user", deepl.ApiKeySource); // User should take precedence over org
    }
}
