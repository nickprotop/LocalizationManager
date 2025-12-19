using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Glossary;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services;

public class GlossaryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GlossaryService _service;
    private readonly Mock<ILogger<GlossaryService>> _mockLogger;

    public GlossaryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _mockLogger = new Mock<ILogger<GlossaryService>>();
        _service = new GlossaryService(_db, _mockLogger.Object);
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

    private async Task<Project> CreateTestProjectAsync(int userId, bool inheritGlossary = true, int? orgId = null)
    {
        var project = new Project
        {
            Slug = "test-project",
            Name = "Test Project",
            UserId = orgId == null ? userId : null,
            OrganizationId = orgId,
            Format = ProjectFormat.Json,
            DefaultLanguage = "en",
            InheritOrganizationGlossary = inheritGlossary,
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

    private async Task<GlossaryTerm> CreateGlossaryTermAsync(
        int? projectId,
        int? orgId,
        int userId,
        string sourceTerm,
        string sourceLanguage = "en",
        Dictionary<string, string>? translations = null,
        bool caseSensitive = false)
    {
        var term = new GlossaryTerm
        {
            ProjectId = projectId,
            OrganizationId = orgId,
            SourceTerm = sourceTerm,
            SourceLanguage = sourceLanguage,
            CaseSensitive = caseSensitive,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GlossaryTerms.Add(term);
        await _db.SaveChangesAsync();

        if (translations != null)
        {
            foreach (var (lang, translation) in translations)
            {
                var glossaryTranslation = new GlossaryTranslation
                {
                    TermId = term.Id,
                    TargetLanguage = lang,
                    TranslatedTerm = translation
                };
                _db.GlossaryTranslations.Add(glossaryTranslation);
            }
            await _db.SaveChangesAsync();
        }

        return term;
    }

    #endregion

    #region Project Glossary Tests

    [Fact]
    public async Task GetProjectGlossaryAsync_ReturnsProjectTerms()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API",
            translations: new Dictionary<string, string> { ["fr"] = "API", ["de"] = "API" });
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Database",
            translations: new Dictionary<string, string> { ["fr"] = "Base de données" });

        // Act
        var result = await _service.GetProjectGlossaryAsync(project.Id);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.ProjectTermsCount);
        Assert.Equal(0, result.InheritedTermsCount);
        Assert.Contains(result.Terms, t => t.SourceTerm == "API");
        Assert.Contains(result.Terms, t => t.SourceTerm == "Database");
    }

    [Fact]
    public async Task GetProjectGlossaryAsync_IncludesInheritedOrgTerms()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        var project = await CreateTestProjectAsync(user.Id, inheritGlossary: true, orgId: org.Id);

        // Project-level term
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Project Term");
        // Org-level term
        await CreateGlossaryTermAsync(null, org.Id, user.Id, "Org Term");

        // Act
        var result = await _service.GetProjectGlossaryAsync(project.Id, includeInherited: true);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.ProjectTermsCount);
        Assert.Equal(1, result.InheritedTermsCount);
    }

    [Fact]
    public async Task GetProjectGlossaryAsync_ExcludesOrgTermsWhenInheritanceDisabled()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        var project = await CreateTestProjectAsync(user.Id, inheritGlossary: false, orgId: org.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Project Term");
        await CreateGlossaryTermAsync(null, org.Id, user.Id, "Org Term");

        // Act
        var result = await _service.GetProjectGlossaryAsync(project.Id, includeInherited: true);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Project Term", result.Terms[0].SourceTerm);
    }

    [Fact]
    public async Task CreateProjectTermAsync_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        var request = new CreateGlossaryTermRequest
        {
            SourceTerm = "Cloud",
            SourceLanguage = "en",
            Description = "Cloud computing term",
            CaseSensitive = false,
            Translations = new Dictionary<string, string>
            {
                ["fr"] = "Cloud",
                ["de"] = "Cloud"
            }
        };

        // Act
        var result = await _service.CreateProjectTermAsync(project.Id, user.Id, request);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Cloud", result.SourceTerm);
        Assert.Equal("en", result.SourceLanguage);
        Assert.Equal("Cloud computing term", result.Description);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.Null(result.OrganizationId);
        Assert.Equal(2, result.Translations.Count);
    }

    [Fact]
    public async Task CreateProjectTermAsync_DuplicateTerm_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Duplicate");

        var request = new CreateGlossaryTermRequest
        {
            SourceTerm = "Duplicate",
            SourceLanguage = "en",
            Translations = new Dictionary<string, string>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateProjectTermAsync(project.Id, user.Id, request));
    }

    [Fact]
    public async Task CreateProjectTermAsync_SameTermDifferentLanguage_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Test", "en");

        var request = new CreateGlossaryTermRequest
        {
            SourceTerm = "Test",
            SourceLanguage = "fr",  // Different source language
            Translations = new Dictionary<string, string>()
        };

        // Act
        var result = await _service.CreateProjectTermAsync(project.Id, user.Id, request);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("fr", result.SourceLanguage);
    }

    #endregion

    #region Organization Glossary Tests

    [Fact]
    public async Task GetOrganizationGlossaryAsync_ReturnsOrgTerms()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        await CreateGlossaryTermAsync(null, org.Id, user.Id, "Org Term 1");
        await CreateGlossaryTermAsync(null, org.Id, user.Id, "Org Term 2");

        // Act
        var result = await _service.GetOrganizationGlossaryAsync(org.Id);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Terms, t => t.SourceTerm == "Org Term 1");
        Assert.Contains(result.Terms, t => t.SourceTerm == "Org Term 2");
    }

    [Fact]
    public async Task CreateOrganizationTermAsync_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        var request = new CreateGlossaryTermRequest
        {
            SourceTerm = "Organization",
            SourceLanguage = "en",
            Translations = new Dictionary<string, string> { ["fr"] = "Organisation" }
        };

        // Act
        var result = await _service.CreateOrganizationTermAsync(org.Id, user.Id, request);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Null(result.ProjectId);
    }

    #endregion

    #region Update and Delete Tests

    [Fact]
    public async Task GetTermAsync_ReturnsTermWithTranslations()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var term = await CreateGlossaryTermAsync(project.Id, null, user.Id, "Test",
            translations: new Dictionary<string, string> { ["fr"] = "Teste", ["de"] = "Test" });

        // Act
        var result = await _service.GetTermAsync(term.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.SourceTerm);
        Assert.Equal(2, result.Translations.Count);
    }

    [Fact]
    public async Task GetTermAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _service.GetTermAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTermAsync_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var term = await CreateGlossaryTermAsync(project.Id, null, user.Id, "Original",
            translations: new Dictionary<string, string> { ["fr"] = "Original" });

        var request = new UpdateGlossaryTermRequest
        {
            SourceTerm = "Updated",
            SourceLanguage = "en",
            Description = "Updated description",
            CaseSensitive = true,
            Translations = new Dictionary<string, string>
            {
                ["fr"] = "Mis à jour",
                ["de"] = "Aktualisiert"
            }
        };

        // Act
        var result = await _service.UpdateTermAsync(term.Id, request);

        // Assert
        Assert.Equal("Updated", result.SourceTerm);
        Assert.Equal("Updated description", result.Description);
        Assert.True(result.CaseSensitive);
        Assert.Equal(2, result.Translations.Count);
        Assert.Equal("Mis à jour", result.Translations["fr"]);
    }

    [Fact]
    public async Task UpdateTermAsync_DuplicateSourceTerm_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Existing");
        var termToUpdate = await CreateGlossaryTermAsync(project.Id, null, user.Id, "ToUpdate");

        var request = new UpdateGlossaryTermRequest
        {
            SourceTerm = "Existing",  // Trying to rename to existing term
            SourceLanguage = "en",
            Translations = new Dictionary<string, string>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateTermAsync(termToUpdate.Id, request));
    }

    [Fact]
    public async Task DeleteTermAsync_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);
        var term = await CreateGlossaryTermAsync(project.Id, null, user.Id, "ToDelete");

        // Act
        await _service.DeleteTermAsync(term.Id);

        // Assert
        var deleted = await _db.GlossaryTerms.FindAsync(term.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteTermAsync_NonExistent_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DeleteTermAsync(99999));
    }

    #endregion

    #region Translation Flow Support Tests

    [Fact]
    public async Task GetEntriesForLanguagePairAsync_ReturnsMatchingEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API", "en",
            translations: new Dictionary<string, string> { ["fr"] = "API" });
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Database", "en",
            translations: new Dictionary<string, string> { ["fr"] = "Base de données", ["de"] = "Datenbank" });

        // Act
        var entries = await _service.GetEntriesForLanguagePairAsync(project.Id, "en", "fr");

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.SourceTerm == "API" && e.TranslatedTerm == "API");
        Assert.Contains(entries, e => e.SourceTerm == "Database" && e.TranslatedTerm == "Base de données");
    }

    [Fact]
    public async Task GetEntriesForLanguagePairAsync_ExcludesTermsWithoutTargetLanguage()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "HasFrench", "en",
            translations: new Dictionary<string, string> { ["fr"] = "A français" });
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "OnlyGerman", "en",
            translations: new Dictionary<string, string> { ["de"] = "Nur Deutsch" });

        // Act
        var entries = await _service.GetEntriesForLanguagePairAsync(project.Id, "en", "fr");

        // Assert
        Assert.Single(entries);
        Assert.Equal("HasFrench", entries[0].SourceTerm);
    }

    [Fact]
    public async Task GetEntriesForLanguagePairAsync_ProjectTermsOverrideOrgTerms()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        var project = await CreateTestProjectAsync(user.Id, inheritGlossary: true, orgId: org.Id);

        // Org term
        await CreateGlossaryTermAsync(null, org.Id, user.Id, "Term", "en",
            translations: new Dictionary<string, string> { ["fr"] = "Org Translation" });
        // Project term with same source
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Term", "en",
            translations: new Dictionary<string, string> { ["fr"] = "Project Translation" });

        // Act
        var entries = await _service.GetEntriesForLanguagePairAsync(project.Id, "en", "fr");

        // Assert
        Assert.Single(entries);
        Assert.Equal("Project Translation", entries[0].TranslatedTerm);
    }

    [Fact]
    public async Task FindMatchingTermsAsync_FindsTermsInText()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API", "en",
            translations: new Dictionary<string, string> { ["fr"] = "API" });
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Database", "en",
            translations: new Dictionary<string, string> { ["fr"] = "Base de données" });

        // Act
        var result = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "The API connects to the Database");

        // Assert
        Assert.True(result.GlossaryApplied);
        Assert.Equal(2, result.TermsMatched);
        Assert.Equal(2, result.MatchedEntries.Count);
    }

    [Fact]
    public async Task FindMatchingTermsAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API", "en",
            translations: new Dictionary<string, string> { ["fr"] = "API" });

        // Act
        var result = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "No matching terms here");

        // Assert
        Assert.False(result.GlossaryApplied);
        Assert.Equal(0, result.TermsMatched);
        Assert.Empty(result.MatchedEntries);
    }

    [Fact]
    public async Task FindMatchingTermsAsync_CaseSensitive_MatchesExactCase()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API", "en",
            translations: new Dictionary<string, string> { ["fr"] = "API" },
            caseSensitive: true);

        // Act
        var resultMatch = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "Use the API here");
        var resultNoMatch = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "Use the api here");  // lowercase

        // Assert
        Assert.True(resultMatch.GlossaryApplied);
        Assert.False(resultNoMatch.GlossaryApplied);
    }

    [Fact]
    public async Task FindMatchingTermsAsync_CaseInsensitive_MatchesAnyCase()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "API", "en",
            translations: new Dictionary<string, string> { ["fr"] = "API" },
            caseSensitive: false);

        // Act
        var resultUpper = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "Use the API here");
        var resultLower = await _service.FindMatchingTermsAsync(
            project.Id, "en", "fr", "Use the api here");

        // Assert
        Assert.True(resultUpper.GlossaryApplied);
        Assert.True(resultLower.GlossaryApplied);
    }

    #endregion

    #region AI Context Tests

    [Fact]
    public void BuildGlossaryContext_CreatesFormattedString()
    {
        // Arrange
        var entries = new List<GlossaryEntryDto>
        {
            new() { SourceTerm = "API", TranslatedTerm = "API", CaseSensitive = false },
            new() { SourceTerm = "Database", TranslatedTerm = "Base de données", CaseSensitive = false }
        };

        // Act
        var context = _service.BuildGlossaryContext(entries);

        // Assert
        Assert.Contains("Use these glossary terms", context);
        Assert.Contains("\"API\" must be translated as \"API\"", context);
        Assert.Contains("\"Database\" must be translated as \"Base de données\"", context);
    }

    [Fact]
    public void BuildGlossaryContext_EmptyList_ReturnsEmptyString()
    {
        // Arrange
        var entries = new List<GlossaryEntryDto>();

        // Act
        var context = _service.BuildGlossaryContext(entries);

        // Assert
        Assert.Equal(string.Empty, context);
    }

    #endregion

    #region Language List Tests

    [Fact]
    public async Task GetProjectGlossaryAsync_ReturnsAllLanguages()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var project = await CreateTestProjectAsync(user.Id);

        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Term1", "en",
            translations: new Dictionary<string, string> { ["fr"] = "T1", ["de"] = "T1" });
        await CreateGlossaryTermAsync(project.Id, null, user.Id, "Term2", "en",
            translations: new Dictionary<string, string> { ["es"] = "T2" });

        // Act
        var result = await _service.GetProjectGlossaryAsync(project.Id);

        // Assert
        Assert.Equal(4, result.Languages.Count); // en, fr, de, es
        Assert.Contains("en", result.Languages);
        Assert.Contains("fr", result.Languages);
        Assert.Contains("de", result.Languages);
        Assert.Contains("es", result.Languages);
    }

    #endregion
}
