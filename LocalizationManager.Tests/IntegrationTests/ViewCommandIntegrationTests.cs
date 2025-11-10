// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class ViewCommandIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public ViewCommandIntegrationTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();

        // Create initial test resource files
        CreateInitialResourceFiles();
    }

    private void CreateInitialResourceFiles()
    {
        // Create default resource file with various keys
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "English (Default)",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Error.NotFound", Value = "Item not found", Comment = "Error message" },
                new() { Key = "Error.Validation", Value = "Validation failed" },
                new() { Key = "Error.Unauthorized", Value = "Access denied" },
                new() { Key = "Success.Save", Value = "Saved successfully" },
                new() { Key = "Success.Delete", Value = "Deleted successfully" },
                new() { Key = "Button.Cancel", Value = "Cancel" },
                new() { Key = "Button.Submit", Value = "Submit" },
                new() { Key = "Label.Name", Value = "Name" },
                new() { Key = "Label.Email", Value = "Email" },
                new() { Key = "Item1", Value = "First item" },
                new() { Key = "Item2", Value = "Second item" },
                new() { Key = "Item3", Value = "Third item" }
            }
        };

        // Create Greek resource file
        var greekFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "el",
                Name = "Ελληνικά (el)",
                IsDefault = false,
                FilePath = Path.Combine(_testDirectory, "TestResource.el.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Error.NotFound", Value = "Δεν βρέθηκε", Comment = "Error message" },
                new() { Key = "Error.Validation", Value = "Αποτυχία επικύρωσης" },
                new() { Key = "Error.Unauthorized", Value = "Άρνηση πρόσβασης" },
                new() { Key = "Success.Save", Value = "Αποθηκεύτηκε επιτυχώς" },
                new() { Key = "Success.Delete", Value = "Διαγράφηκε επιτυχώς" },
                new() { Key = "Button.Cancel", Value = "Ακύρωση" },
                new() { Key = "Button.Submit", Value = "Υποβολή" },
                new() { Key = "Label.Name", Value = "Όνομα" },
                new() { Key = "Label.Email", Value = "Email" },
                new() { Key = "Item1", Value = "Πρώτο στοιχείο" },
                new() { Key = "Item2", Value = "Δεύτερο στοιχείο" },
                new() { Key = "Item3", Value = "Τρίτο στοιχείο" }
            }
        };

        _parser.Write(defaultFile);
        _parser.Write(greekFile);
    }

    [Fact]
    public void ExactMatch_SingleKey_ReturnsOneKey()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var testKey = "Error.NotFound";

        // Act
        var matchedKeys = new List<string>();
        var existingEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == testKey);
        if (existingEntry != null)
        {
            matchedKeys.Add(testKey);
        }

        // Assert
        Assert.Single(matchedKeys);
        Assert.Equal(testKey, matchedKeys[0]);
    }

    [Fact]
    public void RegexMatch_ErrorPattern_ReturnsThreeKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "^Error\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(3, matchedKeys.Count);
        Assert.Contains("Error.NotFound", matchedKeys);
        Assert.Contains("Error.Validation", matchedKeys);
        Assert.Contains("Error.Unauthorized", matchedKeys);
    }

    [Fact]
    public void RegexMatch_SuccessPattern_ReturnsTwoKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "^Success\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(2, matchedKeys.Count);
        Assert.Contains("Success.Save", matchedKeys);
        Assert.Contains("Success.Delete", matchedKeys);
    }

    [Fact]
    public void RegexMatch_ButtonPattern_ReturnsTwoKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "Button\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(2, matchedKeys.Count);
        Assert.Contains("Button.Cancel", matchedKeys);
        Assert.Contains("Button.Submit", matchedKeys);
    }

    [Fact]
    public void RegexMatch_ItemWithNumbersPattern_ReturnsThreeKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "Item[0-9]+";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(3, matchedKeys.Count);
        Assert.Contains("Item1", matchedKeys);
        Assert.Contains("Item2", matchedKeys);
        Assert.Contains("Item3", matchedKeys);
    }

    [Fact]
    public void RegexMatch_AnyPatternContainingError_ReturnsThreeKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = ".*Error.*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(3, matchedKeys.Count);
        Assert.All(matchedKeys, key => Assert.Contains("Error", key));
    }

    [Fact]
    public void RegexMatch_NoMatch_ReturnsEmptyList()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "^NonExistent\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Empty(matchedKeys);
    }

    [Fact]
    public void RegexMatch_InvalidPattern_ThrowsRegexParseException()
    {
        // Arrange
        var pattern = "[0-9"; // Invalid regex - unclosed bracket

        // Act & Assert
        Assert.Throws<RegexParseException>(() =>
        {
            var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        });
    }

    [Fact]
    public void Sorting_MatchedKeys_ReturnsAlphabetically()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "^Error\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .OrderBy(k => k)
            .ToList();

        // Assert
        Assert.Equal(3, matchedKeys.Count);
        Assert.Equal("Error.NotFound", matchedKeys[0]);
        Assert.Equal("Error.Unauthorized", matchedKeys[1]);
        Assert.Equal("Error.Validation", matchedKeys[2]);
    }

    [Fact]
    public void Limit_AppliedToMatches_ReturnsTruncatedList()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = ".*"; // Match everything
        var limit = 5;

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .Take(limit)
            .ToList();

        // Assert
        Assert.Equal(limit, matchedKeys.Count);
    }

    [Fact]
    public void TranslationsExist_ForAllMatchedKeys_InAllLanguages()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "^Button\\..*";

        // Act
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert - Check all languages have these keys
        foreach (var lang in languages)
        {
            var file = _parser.Parse(lang);
            foreach (var key in matchedKeys)
            {
                var entry = file.Entries.FirstOrDefault(e => e.Key == key);
                Assert.NotNull(entry);
                Assert.False(string.IsNullOrEmpty(entry.Value));
            }
        }
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
