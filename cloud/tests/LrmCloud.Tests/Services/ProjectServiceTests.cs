using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Organizations;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using Translation = LrmCloud.Shared.Entities.Translation;

namespace LrmCloud.Tests.Services;

public class ProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectService _projectService;
    private readonly Mock<IOrganizationService> _mockOrgService;
    private readonly Mock<ILogger<ProjectService>> _mockLogger;
    private readonly CloudConfiguration _config;

    public ProjectServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        // Mock configuration
        _config = new CloudConfiguration
        {
            Server = new ServerConfiguration { Urls = "http://localhost:5000", Environment = "Test" },
            Database = new DatabaseConfiguration { ConnectionString = "test" },
            Redis = new RedisConfiguration { ConnectionString = "test" },
            Storage = new StorageConfiguration
            {
                Endpoint = "localhost:9000",
                AccessKey = "test",
                SecretKey = "test",
                Bucket = "test"
            },
            Encryption = new EncryptionConfiguration { TokenKey = "test1234567890test1234567890test" },
            Auth = new AuthConfiguration { JwtSecret = "test1234567890test1234567890test1234567890test1234567890test" },
            Mail = new MailConfiguration { Host = "localhost", FromAddress = "test@test.com", FromName = "Test" },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration()
        };

        // Setup mocks
        _mockOrgService = new Mock<IOrganizationService>();
        _mockLogger = new Mock<ILogger<ProjectService>>();

        _projectService = new ProjectService(_db, _mockOrgService.Object, _config, _mockLogger.Object);
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

    private async Task<Organization> CreateTestOrganizationAsync(int ownerId, string slug = "test-org")
    {
        var org = new Organization
        {
            Name = "Test Organization",
            Slug = slug,
            Description = "Test description",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    private async Task<OrganizationMember> AddOrganizationMemberAsync(
        int organizationId, int userId, string role = OrganizationRole.Member)
    {
        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        _db.OrganizationMembers.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    // ============================================================
    // Project CRUD Tests
    // ============================================================

    [Fact]
    public async Task CreateProjectAsync_PersonalProject_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new CreateProjectRequest
        {
            Name = "My Project",
            Description = "Test project",
            Format = ProjectFormat.Json,
            DefaultLanguage = "en"
        };

        // Act
        var (success, project, errorMessage) = await _projectService.CreateProjectAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(project);
        Assert.Null(errorMessage);
        Assert.Equal("My Project", project.Name);
        Assert.Equal(user.Id, project.UserId);
        Assert.Null(project.OrganizationId);
        Assert.Equal(ProjectFormat.Json, project.Format);
        Assert.Equal(SyncStatus.Pending, project.SyncStatus);
    }

    [Fact]
    public async Task CreateProjectAsync_OrganizationProject_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _mockOrgService.Setup(s => s.IsAdminOrOwnerAsync(org.Id, owner.Id))
            .ReturnsAsync(true);

        var request = new CreateProjectRequest
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Resx
        };

        // Act
        var (success, project, errorMessage) = await _projectService.CreateProjectAsync(owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(project);
        Assert.Equal(org.Id, project.OrganizationId);
        Assert.Null(project.UserId);
        Assert.Equal("Test Organization", project.OrganizationName);
    }

    [Fact]
    public async Task CreateProjectAsync_OrganizationProject_NonAdminUser_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _mockOrgService.Setup(s => s.IsAdminOrOwnerAsync(org.Id, member.Id))
            .ReturnsAsync(false);

        var request = new CreateProjectRequest
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json
        };

        // Act
        var (success, project, errorMessage) = await _projectService.CreateProjectAsync(member.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(project);
        Assert.Equal("Only organization admins and owners can create projects", errorMessage);
    }

    [Fact]
    public async Task CreateProjectAsync_InvalidFormat_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new CreateProjectRequest
        {
            Name = "My Project",
            Format = "invalid-format"
        };

        // Act
        var (success, project, errorMessage) = await _projectService.CreateProjectAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(project);
        Assert.Contains("Invalid format", errorMessage);
    }

    [Fact]
    public async Task GetProjectAsync_AuthorizedUser_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var result = await _projectService.GetProjectAsync(project.Id, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Project", result.Name);
        Assert.Equal(project.Id, result.Id);
    }

    [Fact]
    public async Task GetProjectAsync_UnauthorizedUser_ReturnsNull()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var otherUser = await CreateTestUserAsync("other@example.com");
        var project = new Project
        {
            Name = "Test Project",
            UserId = owner.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var result = await _projectService.GetProjectAsync(project.Id, otherUser.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProjectsAsync_ReturnsPersonalAndOrgProjects()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        await AddOrganizationMemberAsync(org.Id, user.Id);

        // Personal project
        var personalProject = new Project
        {
            Name = "Personal Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(personalProject);

        // Organization project
        var orgProject = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Resx,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(orgProject);
        await _db.SaveChangesAsync();

        // Act
        var projects = await _projectService.GetUserProjectsAsync(user.Id);

        // Assert
        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, p => p.Name == "Personal Project");
        Assert.Contains(projects, p => p.Name == "Org Project");
    }

    [Fact]
    public async Task UpdateProjectAsync_AuthorizedUser_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            AutoTranslate = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var request = new UpdateProjectRequest
        {
            Name = "Updated Project",
            AutoTranslate = true
        };

        // Act
        var (success, updatedProject, errorMessage) =
            await _projectService.UpdateProjectAsync(project.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(updatedProject);
        Assert.Equal("Updated Project", updatedProject.Name);
        Assert.True(updatedProject.AutoTranslate);
    }

    [Fact]
    public async Task UpdateProjectAsync_UnauthorizedUser_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var otherUser = await CreateTestUserAsync("other@example.com");
        var project = new Project
        {
            Name = "Test Project",
            UserId = owner.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var request = new UpdateProjectRequest { Name = "Hacked Project" };

        // Act
        var (success, updatedProject, errorMessage) =
            await _projectService.UpdateProjectAsync(project.Id, otherUser.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(updatedProject);
        Assert.Equal("You don't have permission to edit this project", errorMessage);
    }

    [Fact]
    public async Task DeleteProjectAsync_AuthorizedUser_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _projectService.DeleteProjectAsync(project.Id, user.Id);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify project is deleted
        var deletedProject = await _db.Projects.FindAsync(project.Id);
        Assert.Null(deletedProject);
    }

    [Fact]
    public async Task DeleteProjectAsync_UnauthorizedUser_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var otherUser = await CreateTestUserAsync("other@example.com");
        var project = new Project
        {
            Name = "Test Project",
            UserId = owner.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _projectService.DeleteProjectAsync(project.Id, otherUser.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("You don't have permission to delete this project", errorMessage);
    }

    // ============================================================
    // Authorization Tests
    // ============================================================

    [Fact]
    public async Task CanViewProjectAsync_PersonalProject_Owner_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var canView = await _projectService.CanViewProjectAsync(project.Id, user.Id);

        // Assert
        Assert.True(canView);
    }

    [Fact]
    public async Task CanViewProjectAsync_PersonalProject_NonOwner_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var otherUser = await CreateTestUserAsync("other@example.com");
        var project = new Project
        {
            Name = "Test Project",
            UserId = owner.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var canView = await _projectService.CanViewProjectAsync(project.Id, otherUser.Id);

        // Assert
        Assert.False(canView);
    }

    [Fact]
    public async Task CanViewProjectAsync_OrganizationProject_Member_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _mockOrgService.Setup(s => s.IsMemberAsync(org.Id, member.Id))
            .ReturnsAsync(true);

        // Act
        var canView = await _projectService.CanViewProjectAsync(project.Id, member.Id);

        // Assert
        Assert.True(canView);
    }

    [Fact]
    public async Task CanEditProjectAsync_PersonalProject_Owner_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var canEdit = await _projectService.CanEditProjectAsync(project.Id, user.Id);

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public async Task CanEditProjectAsync_OrganizationProject_Admin_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var admin = await CreateTestUserAsync("admin@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _mockOrgService.Setup(s => s.IsAdminOrOwnerAsync(org.Id, admin.Id))
            .ReturnsAsync(true);

        // Act
        var canEdit = await _projectService.CanEditProjectAsync(project.Id, admin.Id);

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public async Task CanEditProjectAsync_OrganizationProject_RegularMember_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _mockOrgService.Setup(s => s.IsAdminOrOwnerAsync(org.Id, member.Id))
            .ReturnsAsync(false);

        // Act
        var canEdit = await _projectService.CanEditProjectAsync(project.Id, member.Id);

        // Assert
        Assert.False(canEdit);
    }

    [Fact]
    public async Task CanManageResourcesAsync_OrganizationProject_Viewer_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var viewer = await CreateTestUserAsync("viewer@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _mockOrgService.Setup(s => s.GetUserRoleAsync(org.Id, viewer.Id))
            .ReturnsAsync(OrganizationRole.Viewer);

        // Act
        var canManage = await _projectService.CanManageResourcesAsync(project.Id, viewer.Id);

        // Assert
        Assert.False(canManage);
    }

    [Fact]
    public async Task CanManageResourcesAsync_OrganizationProject_Member_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = new Project
        {
            Name = "Org Project",
            OrganizationId = org.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _mockOrgService.Setup(s => s.GetUserRoleAsync(org.Id, member.Id))
            .ReturnsAsync(OrganizationRole.Member);

        // Act
        var canManage = await _projectService.CanManageResourcesAsync(project.Id, member.Id);

        // Assert
        Assert.True(canManage);
    }

    // ============================================================
    // Stats Calculation Tests
    // ============================================================

    [Fact]
    public async Task GetProjectAsync_CalculatesStatsCorrectly()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Add resource keys with translations
        var key1 = new ResourceKey
        {
            ProjectId = project.Id,
            KeyName = "hello",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var key2 = new ResourceKey
        {
            ProjectId = project.Id,
            KeyName = "goodbye",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ResourceKeys.AddRange(key1, key2);
        await _db.SaveChangesAsync();

        // Add translations
        _db.Translations.AddRange(
            new Shared.Entities.Translation { ResourceKeyId = key1.Id, LanguageCode = "en", Value = "Hello", Status = TranslationStatus.Translated, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key1.Id, LanguageCode = "fr", Value = "Bonjour", Status = TranslationStatus.Translated, UpdatedAt = DateTime.UtcNow },
            new Shared.Entities.Translation { ResourceKeyId = key2.Id, LanguageCode = "en", Value = "Goodbye", Status = TranslationStatus.Pending, UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _projectService.GetProjectAsync(project.Id, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.KeyCount);
        Assert.Equal(3, result.TranslationCount);
        Assert.Equal(100.0, result.CompletionPercentage); // All 3 translations have non-empty values
    }

    [Fact]
    public async Task TriggerSyncAsync_WithoutGitHubRepo_Fails()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            GitHubRepo = null, // No GitHub repo configured
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _projectService.TriggerSyncAsync(project.Id, user.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("Project does not have a GitHub repository configured", errorMessage);
    }

    [Fact]
    public async Task TriggerSyncAsync_WithGitHubRepo_UpdatesStatus()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = new Project
        {
            Name = "Test Project",
            UserId = user.Id,
            Format = ProjectFormat.Json,
            GitHubRepo = "owner/repo",
            SyncStatus = SyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _projectService.TriggerSyncAsync(project.Id, user.Id);

        // Assert
        Assert.True(success);

        // Verify status was updated
        var updatedProject = await _db.Projects.FindAsync(project.Id);
        Assert.Equal(SyncStatus.Syncing, updatedProject!.SyncStatus);
    }
}
