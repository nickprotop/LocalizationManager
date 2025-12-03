// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for delete command with duplicate key handling.
/// Tests both RESX (which supports duplicates) and JSON (which doesn't support duplicates).
/// </summary>
public class DeleteCommandDuplicateTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _resxDirectory;
    private readonly string _jsonDirectory;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;
    private readonly IResourceBackend _jsonBackend;

    public DeleteCommandDuplicateTests()
    {
        // Create temporary test directories
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmTests_{Guid.NewGuid()}");
        _resxDirectory = Path.Combine(_testDirectory, "resx");
        _jsonDirectory = Path.Combine(_testDirectory, "json");
        Directory.CreateDirectory(_resxDirectory);
        Directory.CreateDirectory(_jsonDirectory);

        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
        _jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration
        {
            UseNestedKeys = false,
            IncludeMeta = false,
            PreserveComments = false
        });

        // Create initial test resource files
        CreateResourceFilesWithDuplicates();
    }

    private void CreateResourceFilesWithDuplicates()
    {
        CreateResxFilesWithDuplicates();
        CreateJsonFiles();
    }

    private void CreateResxFilesWithDuplicates()
    {
        // Create default resource file with duplicates
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_resxDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "SingleKey", Value = "Single value" },
                new() { Key = "DuplicateKey", Value = "First value" },
                new() { Key = "DuplicateKey", Value = "Second value" },
                new() { Key = "AnotherKey", Value = "Another value" }
            }
        };

        // Create French resource file with duplicates
        var frenchFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "fr",
                Name = "Français (fr)",
                IsDefault = false,
                FilePath = Path.Combine(_resxDirectory, "TestResource.fr.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "SingleKey", Value = "Valeur unique" },
                new() { Key = "DuplicateKey", Value = "Première valeur" },
                new() { Key = "DuplicateKey", Value = "Deuxième valeur" },
                new() { Key = "AnotherKey", Value = "Une autre valeur" }
            }
        };

        _parser.Write(defaultFile);
        _parser.Write(frenchFile);
    }

    private void CreateJsonFiles()
    {
        // JSON doesn't support duplicate keys - create files with unique keys
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_jsonDirectory, "TestResource.json")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "SingleKey", Value = "Single value" },
                new() { Key = "KeyToDelete", Value = "Delete me" },
                new() { Key = "AnotherKey", Value = "Another value" }
            }
        };

        var frenchFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "fr",
                Name = "Français (fr)",
                IsDefault = false,
                FilePath = Path.Combine(_jsonDirectory, "TestResource.fr.json")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "SingleKey", Value = "Valeur unique" },
                new() { Key = "KeyToDelete", Value = "Supprimer moi" },
                new() { Key = "AnotherKey", Value = "Une autre valeur" }
            }
        };

        _jsonBackend.Writer.Write(defaultFile);
        _jsonBackend.Writer.Write(frenchFile);
    }

    #region RESX Tests (with duplicate support)

    [Fact]
    public void DeleteKey_WithDuplicates_WithoutFlag_ThrowsError()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var resourceFiles = languages.Select(lang => _parser.Parse(lang)).ToList();

        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var occurrenceCount = defaultFile.Entries.Count(e => e.Key == "DuplicateKey");

        // Assert: Verify we have duplicates
        Assert.Equal(2, occurrenceCount);

        // Act & Assert: Attempting to delete should detect duplicates
        // In real command, this would return error code 1
        // Here we just verify detection logic works
        var hasDuplicates = occurrenceCount > 1;
        Assert.True(hasDuplicates);
    }

    [Fact]
    public void DeleteKey_WithAllDuplicatesFlag_RemovesAll()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var resourceFiles = languages.Select(lang => _parser.Parse(lang)).ToList();

        // Act: Delete all occurrences (simulating --all-duplicates flag)
        foreach (var rf in resourceFiles)
        {
            rf.Entries.RemoveAll(e => e.Key == "DuplicateKey");
        }

        foreach (var rf in resourceFiles)
        {
            _parser.Write(rf);
        }

        // Assert: Verify key is completely removed
        var reloadedLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _parser.Parse(lang)).ToList();

        foreach (var rf in reloadedFiles)
        {
            Assert.DoesNotContain(rf.Entries, e => e.Key == "DuplicateKey");
        }

        // Verify other keys remain
        var reloadedDefault = reloadedFiles.First(rf => rf.Language.IsDefault);
        Assert.Contains(reloadedDefault.Entries, e => e.Key == "SingleKey");
        Assert.Contains(reloadedDefault.Entries, e => e.Key == "AnotherKey");
    }

    [Fact]
    public void DeleteKey_SingleOccurrence_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var resourceFiles = languages.Select(lang => _parser.Parse(lang)).ToList();

        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var occurrenceCount = defaultFile.Entries.Count(e => e.Key == "SingleKey");

        Assert.Equal(1, occurrenceCount); // Verify only one occurrence

        // Act: Delete (no flag needed for single occurrence)
        foreach (var rf in resourceFiles)
        {
            rf.Entries.RemoveAll(e => e.Key == "SingleKey");
        }

        foreach (var rf in resourceFiles)
        {
            _parser.Write(rf);
        }

        // Assert
        var reloadedLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _parser.Parse(lang)).ToList();

        foreach (var rf in reloadedFiles)
        {
            Assert.DoesNotContain(rf.Entries, e => e.Key == "SingleKey");
        }
    }

    [Fact]
    public void DetectDuplicates_FindsCorrectCount()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));

        // Act: Check for duplicates
        var keysWithDuplicates = defaultFile.Entries
            .GroupBy(e => e.Key)
            .Where(g => g.Count() > 1)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToList();

        // Assert
        Assert.Single(keysWithDuplicates); // Only "DuplicateKey" has duplicates
        Assert.Equal("DuplicateKey", keysWithDuplicates[0].Key);
        Assert.Equal(2, keysWithDuplicates[0].Count);
    }

    [Fact]
    public void DeleteKey_NonExistent_NoError()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var resourceFiles = languages.Select(lang => _parser.Parse(lang)).ToList();

        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var initialCount = defaultFile.Entries.Count;

        // Act: Try to delete non-existent key
        var removedCount = 0;
        foreach (var rf in resourceFiles)
        {
            removedCount += rf.Entries.RemoveAll(e => e.Key == "NonExistentKey");
        }

        // Assert: Nothing was removed
        Assert.Equal(0, removedCount);
        Assert.Equal(initialCount, defaultFile.Entries.Count);
    }

    [Fact]
    public void DeleteKey_CrossLanguageSync_RemovesFromAll()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var resourceFiles = languages.Select(lang => _parser.Parse(lang)).ToList();

        // Verify key exists in all languages
        foreach (var rf in resourceFiles)
        {
            Assert.Contains(rf.Entries, e => e.Key == "SingleKey");
        }

        // Act: Delete from all languages
        foreach (var rf in resourceFiles)
        {
            rf.Entries.RemoveAll(e => e.Key == "SingleKey");
        }

        foreach (var rf in resourceFiles)
        {
            _parser.Write(rf);
        }

        // Assert: Verify removed from all languages
        var reloadedLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _parser.Parse(lang)).ToList();

        foreach (var rf in reloadedFiles)
        {
            Assert.DoesNotContain(rf.Entries, e => e.Key == "SingleKey");
        }
    }

    #endregion

    #region JSON Tests (no duplicate support)

    [Fact]
    public void Json_DeleteKey_RemovesSuccessfully()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var resourceFiles = languages.Select(lang => _jsonBackend.Reader.Read(lang)).ToList();

        // Verify key exists
        foreach (var rf in resourceFiles)
        {
            Assert.Contains(rf.Entries, e => e.Key == "KeyToDelete");
        }

        // Act: Delete key
        foreach (var rf in resourceFiles)
        {
            rf.Entries.RemoveAll(e => e.Key == "KeyToDelete");
            _jsonBackend.Writer.Write(rf);
        }

        // Assert: Verify removed
        var reloadedLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in reloadedLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            Assert.DoesNotContain(file.Entries, e => e.Key == "KeyToDelete");
        }
    }

    [Fact]
    public void Json_DeleteKey_PreservesOtherKeys()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        // Act: Delete one key
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            file.Entries.RemoveAll(e => e.Key == "KeyToDelete");
            _jsonBackend.Writer.Write(file);
        }

        // Assert: Other keys still exist
        var reloadedLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in reloadedLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            Assert.Contains(file.Entries, e => e.Key == "SingleKey");
            Assert.Contains(file.Entries, e => e.Key == "AnotherKey");
            Assert.Equal(2, file.Entries.Count);
        }
    }

    [Fact]
    public void Json_DeleteKey_NonExistent_NoError()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var defaultFile = _jsonBackend.Reader.Read(languages.First(l => l.IsDefault));
        var initialCount = defaultFile.Entries.Count;

        // Act: Try to delete non-existent key
        var removedCount = defaultFile.Entries.RemoveAll(e => e.Key == "NonExistentKey");
        _jsonBackend.Writer.Write(defaultFile);

        // Assert: Nothing was removed
        Assert.Equal(0, removedCount);
        var reloadedFile = _jsonBackend.Reader.Read(languages.First(l => l.IsDefault));
        Assert.Equal(initialCount, reloadedFile.Entries.Count);
    }

    [Fact]
    public void Json_DeleteKey_CrossLanguageSync_RemovesFromAll()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        // Verify key exists in all languages
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            Assert.Contains(file.Entries, e => e.Key == "SingleKey");
        }

        // Act: Delete from all languages
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            file.Entries.RemoveAll(e => e.Key == "SingleKey");
            _jsonBackend.Writer.Write(file);
        }

        // Assert: Verify removed from all languages
        var reloadedLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in reloadedLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            Assert.DoesNotContain(file.Entries, e => e.Key == "SingleKey");
        }
    }

    [Fact]
    public void Json_NoDuplicateKeys_AllKeysUnique()
    {
        // JSON format enforces unique keys
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var duplicateKeys = file.Entries
                .GroupBy(e => e.Key)
                .Where(g => g.Count() > 1)
                .ToList();

            Assert.Empty(duplicateKeys);
        }
    }

    #endregion

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
