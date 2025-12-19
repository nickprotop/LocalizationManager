using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Reviews;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using Translation = LrmCloud.Shared.Entities.Translation;

namespace LrmCloud.Tests.Services;

public class ReviewWorkflowServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReviewWorkflowService _service;
    private readonly Mock<ILogger<ReviewWorkflowService>> _mockLogger;

    public ReviewWorkflowServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _mockLogger = new Mock<ILogger<ReviewWorkflowService>>();
        _service = new ReviewWorkflowService(_db, _mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    #region Helper Methods

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

    private async Task<Project> CreateTestProjectAsync(int userId, bool workflowEnabled = false)
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = userId,
            Format = ProjectFormat.Json,
            DefaultLanguage = "en",
            ReviewWorkflowEnabled = workflowEnabled,
            RequireReviewBeforeExport = false,
            RequireApprovalBeforeExport = false,
            InheritOrganizationReviewers = true,
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
            Name = "Test Organization",
            Slug = "test-org",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    private async Task<Project> CreateOrgProjectAsync(int orgId)
    {
        var project = new Project
        {
            Slug = "org-project",
            Name = "Org Project",
            OrganizationId = orgId,
            Format = ProjectFormat.Json,
            DefaultLanguage = "en",
            ReviewWorkflowEnabled = true,
            InheritOrganizationReviewers = true,
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

    private async Task<LrmCloud.Shared.Entities.Translation> CreateTestTranslationAsync(
        int keyId,
        string languageCode = "en",
        string status = TranslationStatus.Translated)
    {
        var translation = new LrmCloud.Shared.Entities.Translation
        {
            ResourceKeyId = keyId,
            LanguageCode = languageCode,
            Value = "Test value",
            Status = status,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Translations.Add(translation);
        await _db.SaveChangesAsync();
        return translation;
    }

    private async Task AddOrganizationMemberAsync(int orgId, int userId, string role = OrganizationRole.Member)
    {
        var member = new OrganizationMember
        {
            OrganizationId = orgId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        _db.OrganizationMembers.Add(member);
        await _db.SaveChangesAsync();
    }

    #endregion

    #region Workflow Settings Tests

    [Fact]
    public async Task GetWorkflowSettingsAsync_ProjectOwner_ReturnsSettings()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id, workflowEnabled: true);

        // Act
        var settings = await _service.GetWorkflowSettingsAsync(project.Id, user.Id);

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ReviewWorkflowEnabled);
        Assert.Empty(settings.Reviewers);
    }

    [Fact]
    public async Task GetWorkflowSettingsAsync_NonOwner_ReturnsNull()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var other = await CreateTestUserAsync("other@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        // Act
        var settings = await _service.GetWorkflowSettingsAsync(project.Id, other.Id);

        // Assert
        Assert.Null(settings);
    }

    [Fact]
    public async Task UpdateWorkflowSettingsAsync_Owner_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        var request = new UpdateWorkflowSettingsRequest
        {
            ReviewWorkflowEnabled = true,
            RequireReviewBeforeExport = true,
            RequireApprovalBeforeExport = false
        };

        // Act
        var (success, settings, error) = await _service.UpdateWorkflowSettingsAsync(project.Id, user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(settings);
        Assert.Null(error);
        Assert.True(settings.ReviewWorkflowEnabled);
        Assert.True(settings.RequireReviewBeforeExport);
        Assert.False(settings.RequireApprovalBeforeExport);
    }

    [Fact]
    public async Task UpdateWorkflowSettingsAsync_NonOwner_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var other = await CreateTestUserAsync("other@example.com");
        var project = await CreateTestProjectAsync(owner.Id);

        var request = new UpdateWorkflowSettingsRequest
        {
            ReviewWorkflowEnabled = true
        };

        // Act
        var (success, settings, error) = await _service.UpdateWorkflowSettingsAsync(project.Id, other.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(settings);
        Assert.Contains("owner or organization admin", error);
    }

    [Fact]
    public async Task GetWorkflowSettingsAsync_IncludesTranslationStats()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);

        await CreateTestTranslationAsync(key.Id, "en", TranslationStatus.Pending);
        await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Translated);
        await CreateTestTranslationAsync(key.Id, "de", TranslationStatus.Reviewed);
        await CreateTestTranslationAsync(key.Id, "es", TranslationStatus.Approved);

        // Act
        var settings = await _service.GetWorkflowSettingsAsync(project.Id, user.Id);

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.Stats);
        Assert.Equal(1, settings.Stats.PendingCount);
        Assert.Equal(1, settings.Stats.TranslatedCount);
        Assert.Equal(1, settings.Stats.ReviewedCount);
        Assert.Equal(1, settings.Stats.ApprovedCount);
        Assert.Equal(4, settings.Stats.TotalCount);
    }

    #endregion

    #region Reviewer Management Tests

    [Fact]
    public async Task AddProjectReviewerAsync_Owner_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        var request = new AddReviewerRequest
        {
            UserId = reviewer.Id,
            Role = ReviewerRole.Reviewer
        };

        // Act
        var (success, dto, error) = await _service.AddProjectReviewerAsync(project.Id, owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(dto);
        Assert.Null(error);
        Assert.Equal(reviewer.Id, dto.UserId);
        Assert.Equal(ReviewerRole.Reviewer, dto.Role);
    }

    [Fact]
    public async Task AddProjectReviewerAsync_WithLanguageRestriction_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        var request = new AddReviewerRequest
        {
            UserId = reviewer.Id,
            Role = ReviewerRole.Reviewer,
            LanguageCodes = new[] { "fr", "de" }
        };

        // Act
        var (success, dto, error) = await _service.AddProjectReviewerAsync(project.Id, owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(dto);
        Assert.NotNull(dto.LanguageCodes);
        Assert.Contains("fr", dto.LanguageCodes);
        Assert.Contains("de", dto.LanguageCodes);
    }

    [Fact]
    public async Task AddProjectReviewerAsync_DuplicateReviewer_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        var request = new AddReviewerRequest
        {
            UserId = reviewer.Id,
            Role = ReviewerRole.Reviewer
        };

        // Add first time
        await _service.AddProjectReviewerAsync(project.Id, owner.Id, request);

        // Act - try to add again
        var (success, dto, error) = await _service.AddProjectReviewerAsync(project.Id, owner.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(dto);
        Assert.Contains("already a reviewer", error);
    }

    [Fact]
    public async Task RemoveProjectReviewerAsync_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        // Add reviewer
        var addRequest = new AddReviewerRequest { UserId = reviewer.Id, Role = ReviewerRole.Reviewer };
        await _service.AddProjectReviewerAsync(project.Id, owner.Id, addRequest);

        // Act
        var (success, error) = await _service.RemoveProjectReviewerAsync(project.Id, owner.Id, reviewer.Id);

        // Assert
        Assert.True(success);
        Assert.Null(error);

        var reviewers = await _service.GetProjectReviewersAsync(project.Id, owner.Id);
        Assert.Empty(reviewers);
    }

    [Fact]
    public async Task GetProjectReviewersAsync_ReturnsAllReviewers()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer1 = await CreateTestUserAsync("reviewer1@example.com");
        var reviewer2 = await CreateTestUserAsync("reviewer2@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer1.Id, Role = ReviewerRole.Reviewer });
        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer2.Id, Role = ReviewerRole.Approver });

        // Act
        var reviewers = await _service.GetProjectReviewersAsync(project.Id, owner.Id);

        // Assert
        Assert.Equal(2, reviewers.Count);
        Assert.Contains(reviewers, r => r.UserId == reviewer1.Id && r.Role == ReviewerRole.Reviewer);
        Assert.Contains(reviewers, r => r.UserId == reviewer2.Id && r.Role == ReviewerRole.Approver);
    }

    #endregion

    #region Organization Reviewer Tests

    [Fact]
    public async Task AddOrganizationReviewerAsync_OrgOwner_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);

        var request = new AddReviewerRequest
        {
            UserId = reviewer.Id,
            Role = ReviewerRole.Approver
        };

        // Act
        var (success, dto, error) = await _service.AddOrganizationReviewerAsync(org.Id, owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(dto);
        Assert.Equal(ReviewerRole.Approver, dto.Role);
    }

    [Fact]
    public async Task GetWorkflowSettingsAsync_IncludesInheritedOrgReviewers()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var orgReviewer = await CreateTestUserAsync("org-reviewer@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = await CreateOrgProjectAsync(org.Id);

        // Add org-level reviewer
        await _service.AddOrganizationReviewerAsync(org.Id, owner.Id,
            new AddReviewerRequest { UserId = orgReviewer.Id, Role = ReviewerRole.Reviewer });

        // Add org member to owner
        await AddOrganizationMemberAsync(org.Id, owner.Id, OrganizationRole.Owner);

        // Act
        var settings = await _service.GetWorkflowSettingsAsync(project.Id, owner.Id);

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.InheritedReviewers);
        Assert.Single(settings.InheritedReviewers);
        Assert.True(settings.InheritedReviewers[0].IsInherited);
    }

    #endregion

    #region Review Action Tests

    [Fact]
    public async Task ReviewTranslationsAsync_Reviewer_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Translated);

        // Add reviewer
        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer.Id, Role = ReviewerRole.Reviewer });

        var request = new ReviewTranslationsRequest
        {
            TranslationIds = new List<int> { translation.Id }
        };

        // Act
        var (success, response, error) = await _service.ReviewTranslationsAsync(project.Id, reviewer.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(response);
        Assert.Equal(1, response.ProcessedCount);
        Assert.Equal(0, response.SkippedCount);

        // Verify translation status updated
        var updated = await _db.Translations.FindAsync(translation.Id);
        Assert.Equal(TranslationStatus.Reviewed, updated!.Status);
        Assert.Equal(reviewer.Id, updated.ReviewedById);
        Assert.NotNull(updated.ReviewedAt);
    }

    [Fact]
    public async Task ReviewTranslationsAsync_SkipsNonTranslatedStatus()
    {
        // Arrange
        var owner = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var pendingTranslation = await CreateTestTranslationAsync(key.Id, "en", TranslationStatus.Pending);
        var approvedTranslation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Approved);

        var request = new ReviewTranslationsRequest
        {
            TranslationIds = new List<int> { pendingTranslation.Id, approvedTranslation.Id }
        };

        // Act - owner can review
        var (success, response, error) = await _service.ReviewTranslationsAsync(project.Id, owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(response);
        Assert.Equal(0, response.ProcessedCount);
        Assert.Equal(2, response.SkippedCount);
        Assert.Contains("not in 'translated' status", response.SkipReason);
    }

    [Fact]
    public async Task ReviewTranslationsAsync_NonReviewer_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var nonReviewer = await CreateTestUserAsync("nonreviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Translated);

        var request = new ReviewTranslationsRequest
        {
            TranslationIds = new List<int> { translation.Id }
        };

        // Act
        var (success, response, error) = await _service.ReviewTranslationsAsync(project.Id, nonReviewer.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Contains("permission to review", error);
    }

    [Fact]
    public async Task ApproveTranslationsAsync_Approver_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var approver = await CreateTestUserAsync("approver@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Reviewed);

        // Add approver
        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = approver.Id, Role = ReviewerRole.Approver });

        var request = new ApproveTranslationsRequest
        {
            TranslationIds = new List<int> { translation.Id }
        };

        // Act
        var (success, response, error) = await _service.ApproveTranslationsAsync(project.Id, approver.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(response);
        Assert.Equal(1, response.ProcessedCount);

        var updated = await _db.Translations.FindAsync(translation.Id);
        Assert.Equal(TranslationStatus.Approved, updated!.Status);
        Assert.Equal(approver.Id, updated.ApprovedById);
    }

    [Fact]
    public async Task ApproveTranslationsAsync_ReviewerRole_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Reviewed);

        // Add as reviewer only (not approver)
        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer.Id, Role = ReviewerRole.Reviewer });

        var request = new ApproveTranslationsRequest
        {
            TranslationIds = new List<int> { translation.Id }
        };

        // Act
        var (success, response, error) = await _service.ApproveTranslationsAsync(project.Id, reviewer.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("permission to approve", error);
    }

    [Fact]
    public async Task RejectTranslationAsync_Reviewer_Success()
    {
        // Arrange
        var owner = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Reviewed);

        var request = new RejectTranslationRequest
        {
            Comment = "Translation is incorrect"
        };

        // Act - owner can reject
        var (success, error) = await _service.RejectTranslationAsync(project.Id, translation.Id, owner.Id, request);

        // Assert
        Assert.True(success);
        Assert.Null(error);

        var updated = await _db.Translations.FindAsync(translation.Id);
        Assert.Equal(TranslationStatus.Translated, updated!.Status);
        Assert.Equal("Translation is incorrect", updated.RejectionComment);
    }

    [Fact]
    public async Task RejectTranslationAsync_InvalidStatus_Fails()
    {
        // Arrange
        var owner = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);
        var key = await CreateTestResourceKeyAsync(project.Id);
        var translation = await CreateTestTranslationAsync(key.Id, "fr", TranslationStatus.Pending);

        var request = new RejectTranslationRequest { Comment = "Test" };

        // Act
        var (success, error) = await _service.RejectTranslationAsync(project.Id, translation.Id, owner.Id, request);

        // Assert
        Assert.False(success);
        Assert.Contains("only reject reviewed or approved", error);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CanReviewAsync_ProjectOwner_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        // Act
        var canReview = await _service.CanReviewAsync(project.Id, owner.Id);

        // Assert
        Assert.True(canReview);
    }

    [Fact]
    public async Task CanReviewAsync_Reviewer_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer.Id, Role = ReviewerRole.Reviewer });

        // Act
        var canReview = await _service.CanReviewAsync(project.Id, reviewer.Id);

        // Assert
        Assert.True(canReview);
    }

    [Fact]
    public async Task CanReviewAsync_ReviewerWithLanguageRestriction_ChecksLanguage()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest
            {
                UserId = reviewer.Id,
                Role = ReviewerRole.Reviewer,
                LanguageCodes = new[] { "fr", "de" }
            });

        // Act
        var canReviewFr = await _service.CanReviewAsync(project.Id, reviewer.Id, "fr");
        var canReviewEs = await _service.CanReviewAsync(project.Id, reviewer.Id, "es");

        // Assert
        Assert.True(canReviewFr);
        Assert.False(canReviewEs);
    }

    [Fact]
    public async Task CanApproveAsync_Approver_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var approver = await CreateTestUserAsync("approver@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = approver.Id, Role = ReviewerRole.Approver });

        // Act
        var canApprove = await _service.CanApproveAsync(project.Id, approver.Id);

        // Assert
        Assert.True(canApprove);
    }

    [Fact]
    public async Task CanApproveAsync_ReviewerOnly_ReturnsFalse()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var reviewer = await CreateTestUserAsync("reviewer@example.com");
        var project = await CreateTestProjectAsync(owner.Id, workflowEnabled: true);

        await _service.AddProjectReviewerAsync(project.Id, owner.Id,
            new AddReviewerRequest { UserId = reviewer.Id, Role = ReviewerRole.Reviewer });

        // Act
        var canApprove = await _service.CanApproveAsync(project.Id, reviewer.Id);

        // Assert
        Assert.False(canApprove);
    }

    [Fact]
    public async Task CanReviewAsync_InheritedOrgReviewer_ReturnsTrue()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com");
        var orgReviewer = await CreateTestUserAsync("org-reviewer@example.com");
        var org = await CreateTestOrganizationAsync(owner.Id);
        var project = await CreateOrgProjectAsync(org.Id);

        // Add org-level reviewer
        await _service.AddOrganizationReviewerAsync(org.Id, owner.Id,
            new AddReviewerRequest { UserId = orgReviewer.Id, Role = ReviewerRole.Reviewer });

        // Act
        var canReview = await _service.CanReviewAsync(project.Id, orgReviewer.Id);

        // Assert
        Assert.True(canReview);
    }

    #endregion

    #region Status Transition Validation Tests

    [Theory]
    [InlineData("pending", "translated", true)]
    [InlineData("translated", "reviewed", true)]
    [InlineData("reviewed", "approved", true)]
    [InlineData("reviewed", "translated", true)]  // reject
    [InlineData("approved", "translated", true)]  // reject
    [InlineData("pending", "reviewed", false)]    // invalid skip
    [InlineData("pending", "approved", false)]    // invalid skip
    [InlineData("translated", "approved", false)] // invalid skip
    [InlineData("approved", "reviewed", false)]   // invalid reverse
    public void ValidateStatusTransition_ReturnsCorrectResult(
        string currentStatus, string newStatus, bool expectedValid)
    {
        // Act
        var (isValid, error) = _service.ValidateStatusTransition(currentStatus, newStatus);

        // Assert
        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.NotNull(error);
        }
    }

    #endregion
}
