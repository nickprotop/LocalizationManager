using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.DTOs.TranslationMemory;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services;

public class TranslationMemoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TranslationMemoryService _service;
    private readonly Mock<ILogger<TranslationMemoryService>> _mockLogger;

    public TranslationMemoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _mockLogger = new Mock<ILogger<TranslationMemoryService>>();
        _service = new TranslationMemoryService(_db, _mockLogger.Object);
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

    private async Task<TranslationMemory> CreateTmEntryAsync(
        int userId,
        string sourceText,
        string translatedText,
        string sourceLang = "en",
        string targetLang = "fr",
        int? orgId = null)
    {
        var entry = new TranslationMemory
        {
            UserId = userId,
            OrganizationId = orgId,
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            SourceText = sourceText,
            TranslatedText = translatedText,
            SourceHash = ComputeHash(NormalizeText(sourceText)),
            UseCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.TranslationMemories.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private static string NormalizeText(string text)
    {
        var normalized = string.Join(" ", text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries));
        return normalized.ToLowerInvariant();
    }

    private static string ComputeHash(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion

    #region Lookup Tests

    [Fact]
    public async Task LookupAsync_ExactMatch_ReturnsMatch()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello world", "Bonjour le monde");

        var request = new TmLookupRequest
        {
            SourceText = "Hello world",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(100, response.Matches[0].MatchPercent);
        Assert.Equal("Hello world", response.Matches[0].SourceText);
        Assert.Equal("Bonjour le monde", response.Matches[0].TranslatedText);
    }

    [Fact]
    public async Task LookupAsync_ExactMatch_CaseInsensitive()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello World", "Bonjour le monde");

        var request = new TmLookupRequest
        {
            SourceText = "hello world",  // Different case
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(100, response.Matches[0].MatchPercent);
    }

    [Fact]
    public async Task LookupAsync_FuzzyMatch_ReturnsMatches()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "The quick brown fox", "Le renard brun rapide");

        var request = new TmLookupRequest
        {
            SourceText = "The quick brown dog",  // Similar but not exact
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.True(response.Matches[0].MatchPercent >= 70);
        Assert.True(response.Matches[0].MatchPercent < 100);
    }

    [Fact]
    public async Task LookupAsync_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello world", "Bonjour le monde");

        var request = new TmLookupRequest
        {
            SourceText = "Completely different text",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Empty(response.Matches);
    }

    [Fact]
    public async Task LookupAsync_DifferentLanguagePair_ReturnsEmpty()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello world", "Bonjour le monde", "en", "fr");

        var request = new TmLookupRequest
        {
            SourceText = "Hello world",
            SourceLanguage = "en",
            TargetLanguage = "de",  // Different target language
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Empty(response.Matches);
    }

    [Fact]
    public async Task LookupAsync_IncludesOrganizationEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        await CreateTmEntryAsync(user.Id, "Personal entry", "Entrée personnelle", "en", "fr");
        await CreateTmEntryAsync(user.Id, "Org entry", "Entrée organisation", "en", "fr", org.Id);

        var request = new TmLookupRequest
        {
            SourceText = "Org entry",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            OrganizationId = org.Id,
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal("Org entry", response.Matches[0].SourceText);
    }

    [Fact]
    public async Task LookupAsync_RespectsMaxResults()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Test one", "Test un");
        await CreateTmEntryAsync(user.Id, "Test two", "Test deux");
        await CreateTmEntryAsync(user.Id, "Test three", "Test trois");

        var request = new TmLookupRequest
        {
            SourceText = "Test",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 50,
            MaxResults = 2
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.True(response.Matches.Count <= 2);
    }

    #endregion

    #region Store Tests

    [Fact]
    public async Task StoreAsync_NewEntry_CreatesEntry()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new TmStoreRequest
        {
            SourceText = "New translation",
            TranslatedText = "Nouvelle traduction",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            Context = "test context"
        };

        // Act
        var result = await _service.StoreAsync(user.Id, request);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("New translation", result.SourceText);
        Assert.Equal("Nouvelle traduction", result.TranslatedText);
        Assert.Equal(1, result.UseCount);
        Assert.Equal("test context", result.Context);
    }

    [Fact]
    public async Task StoreAsync_ExistingEntry_UpdatesEntry()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var existing = await CreateTmEntryAsync(user.Id, "Hello", "Bonjour");
        var originalUseCount = existing.UseCount;

        var request = new TmStoreRequest
        {
            SourceText = "Hello",
            TranslatedText = "Salut",  // Updated translation
            SourceLanguage = "en",
            TargetLanguage = "fr"
        };

        // Act
        var result = await _service.StoreAsync(user.Id, request);

        // Assert
        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("Salut", result.TranslatedText);
        Assert.Equal(originalUseCount + 1, result.UseCount);
    }

    [Fact]
    public async Task StoreAsync_WithOrganization_StoresOrgEntry()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);

        var request = new TmStoreRequest
        {
            SourceText = "Org text",
            TranslatedText = "Texte org",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            OrganizationId = org.Id
        };

        // Act
        var result = await _service.StoreAsync(user.Id, request);

        // Assert
        Assert.Equal(org.Id, result.OrganizationId);
    }

    [Fact]
    public async Task StoreBatchAsync_StoresMultipleEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var requests = new List<TmStoreRequest>
        {
            new() { SourceText = "One", TranslatedText = "Un", SourceLanguage = "en", TargetLanguage = "fr" },
            new() { SourceText = "Two", TranslatedText = "Deux", SourceLanguage = "en", TargetLanguage = "fr" },
            new() { SourceText = "Three", TranslatedText = "Trois", SourceLanguage = "en", TargetLanguage = "fr" }
        };

        // Act
        await _service.StoreBatchAsync(user.Id, requests);

        // Assert
        var entries = await _db.TranslationMemories.Where(tm => tm.UserId == user.Id).ToListAsync();
        Assert.Equal(3, entries.Count);
    }

    #endregion

    #region Use Count Tests

    [Fact]
    public async Task IncrementUseCountAsync_IncrementsCount()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var entry = await CreateTmEntryAsync(user.Id, "Test", "Test");
        var originalCount = entry.UseCount;

        // Act
        await _service.IncrementUseCountAsync(entry.Id);

        // Assert
        var updated = await _db.TranslationMemories.FindAsync(entry.Id);
        Assert.Equal(originalCount + 1, updated!.UseCount);
    }

    [Fact]
    public async Task IncrementUseCountAsync_NonExistentEntry_DoesNothing()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act - should not throw
        await _service.IncrementUseCountAsync(nonExistentId);

        // Assert - no exception thrown
        Assert.True(true);
    }

    #endregion

    #region Stats Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Entry 1", "Entrée 1", "en", "fr");
        await CreateTmEntryAsync(user.Id, "Entry 2", "Entrée 2", "en", "fr");
        await CreateTmEntryAsync(user.Id, "Entry 3", "Eintrag 3", "en", "de");

        // Act
        var stats = await _service.GetStatsAsync(user.Id);

        // Assert
        Assert.Equal(3, stats.TotalEntries);
        Assert.Equal(3, stats.TotalUseCount); // Each entry has UseCount = 1
        Assert.Equal(2, stats.LanguagePairs.Count);
        Assert.Contains(stats.LanguagePairs, lp => lp.SourceLanguage == "en" && lp.TargetLanguage == "fr" && lp.EntryCount == 2);
        Assert.Contains(stats.LanguagePairs, lp => lp.SourceLanguage == "en" && lp.TargetLanguage == "de" && lp.EntryCount == 1);
    }

    [Fact]
    public async Task GetStatsAsync_IncludesOrganizationEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var org = await CreateTestOrganizationAsync(user.Id);
        await CreateTmEntryAsync(user.Id, "Personal", "Personnel");
        await CreateTmEntryAsync(user.Id, "Org entry", "Entrée org", "en", "fr", org.Id);

        // Act
        var stats = await _service.GetStatsAsync(user.Id, org.Id);

        // Assert
        Assert.Equal(2, stats.TotalEntries);
    }

    [Fact]
    public async Task GetStatsAsync_EmptyTm_ReturnsZeros()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var stats = await _service.GetStatsAsync(user.Id);

        // Assert
        Assert.Equal(0, stats.TotalEntries);
        Assert.Equal(0, stats.TotalUseCount);
        Assert.Empty(stats.LanguagePairs);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_OwnEntry_Success()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var entry = await CreateTmEntryAsync(user.Id, "To delete", "À supprimer");

        // Act
        var result = await _service.DeleteAsync(user.Id, entry.Id);

        // Assert
        Assert.True(result);
        var deleted = await _db.TranslationMemories.FindAsync(entry.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersEntry_ReturnsFalse()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("user1@example.com");
        var user2 = await CreateTestUserAsync("user2@example.com");
        var entry = await CreateTmEntryAsync(user1.Id, "User1 entry", "Entrée user1");

        // Act
        var result = await _service.DeleteAsync(user2.Id, entry.Id);

        // Assert
        Assert.False(result);
        var stillExists = await _db.TranslationMemories.FindAsync(entry.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentEntry_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var result = await _service.DeleteAsync(user.Id, 99999);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task ClearAsync_ClearsAllUserEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Entry 1", "Entrée 1");
        await CreateTmEntryAsync(user.Id, "Entry 2", "Entrée 2");
        await CreateTmEntryAsync(user.Id, "Entry 3", "Entrée 3");

        // Act
        var deletedCount = await _service.ClearAsync(user.Id);

        // Assert
        Assert.Equal(3, deletedCount);
        var remaining = await _db.TranslationMemories.Where(tm => tm.UserId == user.Id).CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ClearAsync_WithLanguageFilter_ClearsOnlyMatchingEntries()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "French 1", "Français 1", "en", "fr");
        await CreateTmEntryAsync(user.Id, "French 2", "Français 2", "en", "fr");
        await CreateTmEntryAsync(user.Id, "German 1", "Deutsch 1", "en", "de");

        // Act
        var deletedCount = await _service.ClearAsync(user.Id, sourceLanguage: "en", targetLanguage: "fr");

        // Assert
        Assert.Equal(2, deletedCount);
        var remaining = await _db.TranslationMemories.Where(tm => tm.UserId == user.Id).ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("de", remaining[0].TargetLanguage);
    }

    [Fact]
    public async Task ClearAsync_DoesNotAffectOtherUsers()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("user1@example.com");
        var user2 = await CreateTestUserAsync("user2@example.com");
        await CreateTmEntryAsync(user1.Id, "User1 entry", "Entrée user1");
        await CreateTmEntryAsync(user2.Id, "User2 entry", "Entrée user2");

        // Act
        await _service.ClearAsync(user1.Id);

        // Assert
        var user1Entries = await _db.TranslationMemories.Where(tm => tm.UserId == user1.Id).CountAsync();
        var user2Entries = await _db.TranslationMemories.Where(tm => tm.UserId == user2.Id).CountAsync();
        Assert.Equal(0, user1Entries);
        Assert.Equal(1, user2Entries);
    }

    #endregion

    #region Whitespace Normalization Tests

    [Fact]
    public async Task LookupAsync_NormalizesWhitespace()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello   world", "Bonjour le monde");

        var request = new TmLookupRequest
        {
            SourceText = "Hello world",  // Single space
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(100, response.Matches[0].MatchPercent);
    }

    [Fact]
    public async Task LookupAsync_NormalizesNewlines()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await CreateTmEntryAsync(user.Id, "Hello\nworld", "Bonjour le monde");

        var request = new TmLookupRequest
        {
            SourceText = "Hello world",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            MinMatchPercent = 70,
            MaxResults = 5
        };

        // Act
        var response = await _service.LookupAsync(user.Id, request);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(100, response.Matches[0].MatchPercent);
    }

    #endregion
}
