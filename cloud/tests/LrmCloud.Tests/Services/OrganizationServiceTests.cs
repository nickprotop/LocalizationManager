using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Organizations;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LrmCloud.Tests.Services;

public class OrganizationServiceTests : IDisposable
{
    private readonly Mock<IMailService> _mailServiceMock;
    private readonly Mock<ILogger<OrganizationService>> _loggerMock;
    private readonly CloudConfiguration _config;
    private readonly AppDbContext _dbContext;
    private readonly OrganizationService _organizationService;

    public OrganizationServiceTests()
    {
        _mailServiceMock = new Mock<IMailService>();
        _loggerMock = new Mock<ILogger<OrganizationService>>();

        _config = new CloudConfiguration
        {
            Server = new ServerConfiguration
            {
                Urls = "http://localhost:5000",
                Environment = "Test",
                BaseUrl = "https://test.lrm-cloud.com"
            },
            Database = new DatabaseConfiguration
            {
                ConnectionString = "test",
                AutoMigrate = false
            },
            Redis = new RedisConfiguration
            {
                ConnectionString = "localhost:6379"
            },
            Storage = new StorageConfiguration
            {
                Endpoint = "localhost:9000",
                AccessKey = "test",
                SecretKey = "test",
                Bucket = "test"
            },
            Encryption = new EncryptionConfiguration
            {
                TokenKey = "dGVzdC1rZXktZm9yLWVuY3J5cHRpb24tMzItY2hhcnM="
            },
            Auth = new AuthConfiguration
            {
                JwtSecret = "test-secret-key-for-jwt-tokens-very-long",
                EmailVerificationExpiryHours = 24
            },
            Mail = new MailConfiguration
            {
                Host = "localhost",
                Port = 25,
                FromAddress = "test@test.com",
                FromName = "Test"
            },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration()
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _organizationService = new OrganizationService(
            _dbContext,
            _mailServiceMock.Object,
            _config,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private async Task<User> CreateTestUserAsync(string email = "test@example.com", string username = "testuser")
    {
        var user = new User
        {
            AuthType = "email",
            Email = email,
            Username = username,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganizationAsync(int ownerId, string name = "Test Org", string slug = "test-org")
    {
        var org = new Organization
        {
            Name = name,
            Slug = slug,
            OwnerId = ownerId,
            Plan = "team", // Use team plan to allow member invitations in tests
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        // Add owner as member
        var ownerMember = new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = ownerId,
            Role = OrganizationRole.Owner,
            InvitedAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OrganizationMembers.Add(ownerMember);
        await _dbContext.SaveChangesAsync();

        return org;
    }

    // ============================================================
    // Organization CRUD Tests
    // ============================================================

    [Fact]
    public async Task CreateOrganizationAsync_ValidRequest_CreatesOrganization()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new CreateOrganizationRequest
        {
            Name = "My Organization",
            Slug = "my-org",
            Description = "Test organization"
        };

        // Act
        var (success, organization, errorMessage) = await _organizationService.CreateOrganizationAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(organization);
        Assert.Equal("My Organization", organization.Name);
        Assert.Equal("my-org", organization.Slug);
        Assert.Equal("Test organization", organization.Description);
        Assert.Equal(user.Id, organization.OwnerId);
        Assert.Equal(OrganizationRole.Owner, organization.UserRole);
        Assert.Equal(1, organization.MemberCount);
    }

    [Fact]
    public async Task CreateOrganizationAsync_AutoGeneratesSlug_WhenNotProvided()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new CreateOrganizationRequest
        {
            Name = "My Cool Organization"
        };

        // Act
        var (success, organization, errorMessage) = await _organizationService.CreateOrganizationAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(organization);
        Assert.Equal("my-cool-organization", organization.Slug);
    }

    [Fact]
    public async Task CreateOrganizationAsync_AutomaticallyAddsOwnerAsMember()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new CreateOrganizationRequest { Name = "Test Org" };

        // Act
        var (success, organization, errorMessage) = await _organizationService.CreateOrganizationAsync(user.Id, request);

        // Assert
        Assert.True(success);
        var member = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organization!.Id && m.UserId == user.Id);
        Assert.NotNull(member);
        Assert.Equal(OrganizationRole.Owner, member.Role);
    }

    [Fact]
    public async Task CreateOrganizationAsync_DuplicateSlug_ReturnsError()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTestOrganizationAsync(user.Id, "Org 1", "test-slug");

        var request = new CreateOrganizationRequest
        {
            Name = "Org 2",
            Slug = "test-slug"
        };

        // Act
        var (success, organization, errorMessage) = await _organizationService.CreateOrganizationAsync(user.Id, request);

        // Assert - Should succeed with modified slug
        Assert.True(success);
        Assert.NotNull(organization);
        Assert.NotEqual("test-slug", organization.Slug);
        Assert.StartsWith("test-slug-", organization.Slug);
    }

    [Fact]
    public async Task GetOrganizationAsync_ValidId_ReturnsOrganization()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        // Act
        var result = await _organizationService.GetOrganizationAsync(org.Id, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(org.Id, result.Id);
        Assert.Equal(org.Name, result.Name);
    }

    [Fact]
    public async Task GetOrganizationAsync_NonMember_ReturnsNull()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var nonMember = await CreateTestUserAsync("nonmember@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Act
        var result = await _organizationService.GetOrganizationAsync(org.Id, nonMember.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_ReturnsOnlyUserOrganizations()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("user1@example.com");
        var user2 = await CreateTestUserAsync("user2@example.com");

        var org1 = await CreateTestOrganizationAsync(user1.Id, "Org 1", "org-1");
        var org2 = await CreateTestOrganizationAsync(user1.Id, "Org 2", "org-2");
        var org3 = await CreateTestOrganizationAsync(user2.Id, "Org 3", "org-3");

        // Act
        var result = await _organizationService.GetUserOrganizationsAsync(user1.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, org => Assert.Equal(user1.Id, org.OwnerId));
    }

    [Fact]
    public async Task UpdateOrganizationAsync_Owner_UpdatesSuccessfully()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        var request = new UpdateOrganizationRequest
        {
            Name = "Updated Name",
            Description = "Updated description"
        };

        // Act
        var (success, updatedOrg, errorMessage) = await _organizationService.UpdateOrganizationAsync(org.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(updatedOrg);
        Assert.Equal("Updated Name", updatedOrg.Name);
        Assert.Equal("Updated description", updatedOrg.Description);
    }

    [Fact]
    public async Task UpdateOrganizationAsync_NonOwner_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Add member as admin
        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new UpdateOrganizationRequest { Name = "Updated Name" };

        // Act
        var (success, updatedOrg, errorMessage) = await _organizationService.UpdateOrganizationAsync(org.Id, member.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("owner", errorMessage!);
    }

    [Fact]
    public async Task DeleteOrganizationAsync_Owner_SoftDeletes()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        // Act
        var (success, errorMessage) = await _organizationService.DeleteOrganizationAsync(org.Id, user.Id);

        // Assert
        Assert.True(success);

        var deletedOrg = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == org.Id);
        Assert.NotNull(deletedOrg);
        Assert.NotNull(deletedOrg.DeletedAt);
    }

    [Fact]
    public async Task DeleteOrganizationAsync_NonOwner_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Act
        var (success, errorMessage) = await _organizationService.DeleteOrganizationAsync(org.Id, member.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("owner", errorMessage!);
    }

    // ============================================================
    // Member Management Tests
    // ============================================================

    [Fact]
    public async Task GetMembersAsync_ReturnsMembersForAuthorizedUser()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _organizationService.GetMembersAsync(org.Id, owner.Id);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetMembersAsync_NonMember_ReturnsEmpty()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var nonMember = await CreateTestUserAsync("nonmember@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Act
        var result = await _organizationService.GetMembersAsync(org.Id, nonMember.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task InviteMemberAsync_Admin_SendsInvitation()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        var request = new InviteMemberRequest
        {
            Email = "newmember@example.com",
            Role = OrganizationRole.Member
        };

        // Act
        var (success, errorMessage) = await _organizationService.InviteMemberAsync(org.Id, owner.Id, request);

        // Assert
        Assert.True(success);

        var invitation = await _dbContext.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.Email == "newmember@example.com");
        Assert.NotNull(invitation);
        Assert.Equal(OrganizationRole.Member, invitation.Role);
        Assert.True(invitation.ExpiresAt > DateTime.UtcNow);

        _mailServiceMock.Verify(
            m => m.SendEmailAsync(
                It.Is<string>(email => email == "newmember@example.com"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task InviteMemberAsync_NonAdmin_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var viewer = await CreateTestUserAsync("viewer@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = viewer.Id,
            Role = OrganizationRole.Viewer,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new InviteMemberRequest
        {
            Email = "newmember@example.com",
            Role = OrganizationRole.Member
        };

        // Act
        var (success, errorMessage) = await _organizationService.InviteMemberAsync(org.Id, viewer.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("admin", errorMessage!);
    }

    [Fact]
    public async Task InviteMemberAsync_ExistingMember_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new InviteMemberRequest
        {
            Email = "member@example.com",
            Role = OrganizationRole.Member
        };

        // Act
        var (success, errorMessage) = await _organizationService.InviteMemberAsync(org.Id, owner.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("already a member", errorMessage!);
    }

    [Fact]
    public async Task InviteMemberAsync_PendingInvitation_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Create existing invitation
        _dbContext.OrganizationInvitations.Add(new OrganizationInvitation
        {
            OrganizationId = org.Id,
            Email = "pending@example.com",
            Role = OrganizationRole.Member,
            TokenHash = "hash",
            InvitedBy = owner.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new InviteMemberRequest
        {
            Email = "pending@example.com",
            Role = OrganizationRole.Member
        };

        // Act
        var (success, errorMessage) = await _organizationService.InviteMemberAsync(org.Id, owner.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("pending invitation", errorMessage!);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ValidToken_AddsMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var newMember = await CreateTestUserAsync("newmember@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Create invitation
        var token = "test-token-12345678901234567890";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token, 12);

        _dbContext.OrganizationInvitations.Add(new OrganizationInvitation
        {
            OrganizationId = org.Id,
            Email = "newmember@example.com",
            Role = OrganizationRole.Member,
            TokenHash = tokenHash,
            InvitedBy = owner.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _organizationService.AcceptInvitationAsync(newMember.Id, token);

        // Assert
        Assert.True(success);

        var member = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == org.Id && m.UserId == newMember.Id);
        Assert.NotNull(member);
        Assert.Equal(OrganizationRole.Member, member.Role);
        Assert.NotNull(member.JoinedAt);
    }

    [Fact]
    public async Task AcceptInvitationAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        var newMember = await CreateTestUserAsync("newmember@example.com");

        // Act
        var (success, errorMessage) = await _organizationService.AcceptInvitationAsync(newMember.Id, "invalid-token");

        // Assert
        Assert.False(success);
        Assert.Contains("invitation", errorMessage!);
    }

    [Fact]
    public async Task RemoveMemberAsync_Admin_RemovesMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        var memberRecord = new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.OrganizationMembers.Add(memberRecord);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _organizationService.RemoveMemberAsync(org.Id, owner.Id, member.Id);

        // Assert
        Assert.True(success);

        var removedMember = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.Id == memberRecord.Id);
        Assert.Null(removedMember);
    }

    [Fact]
    public async Task RemoveMemberAsync_CannotRemoveOwner()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var admin = await CreateTestUserAsync("admin@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = admin.Id,
            Role = OrganizationRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _organizationService.RemoveMemberAsync(org.Id, admin.Id, owner.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("owner", errorMessage!);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_Owner_UpdatesRole()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _organizationService.UpdateMemberRoleAsync(
            org.Id, owner.Id, member.Id, OrganizationRole.Admin);

        // Assert
        Assert.True(success);

        var updatedMember = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == org.Id && m.UserId == member.Id);
        Assert.NotNull(updatedMember);
        Assert.Equal(OrganizationRole.Admin, updatedMember.Role);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_NonOwner_ReturnsError()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var admin = await CreateTestUserAsync("admin@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = admin.Id,
            Role = OrganizationRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _organizationService.UpdateMemberRoleAsync(
            org.Id, admin.Id, member.Id, OrganizationRole.Admin);

        // Assert
        Assert.False(success);
        Assert.Contains("owner", errorMessage!);
    }

    // ============================================================
    // Authorization Tests
    // ============================================================

    [Fact]
    public async Task IsOwnerAsync_Owner_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Act
        var result = await _organizationService.IsOwnerAsync(org.Id, owner.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsOwnerAsync_NonOwner_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        // Act
        var result = await _organizationService.IsOwnerAsync(org.Id, member.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminOrOwnerAsync_Admin_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var admin = await CreateTestUserAsync("admin@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = admin.Id,
            Role = OrganizationRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _organizationService.IsAdminOrOwnerAsync(org.Id, admin.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAdminOrOwnerAsync_Member_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Member,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _organizationService.IsAdminOrOwnerAsync(org.Id, member.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsMemberAsync_Member_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var member = await CreateTestUserAsync("member@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = member.Id,
            Role = OrganizationRole.Viewer,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _organizationService.IsMemberAsync(org.Id, member.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetUserRoleAsync_ReturnsCorrectRole()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var admin = await CreateTestUserAsync("admin@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = admin.Id,
            Role = OrganizationRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var ownerRole = await _organizationService.GetUserRoleAsync(org.Id, owner.Id);
        var adminRole = await _organizationService.GetUserRoleAsync(org.Id, admin.Id);

        // Assert
        Assert.Equal(OrganizationRole.Owner, ownerRole);
        Assert.Equal(OrganizationRole.Admin, adminRole);
    }
}
