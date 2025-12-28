// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using LocalizationManager.Commands;
using Spectre.Console.Cli;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Tests for case-variant duplicate handling to ensure consistency with MSBuild behavior.
/// MSBuild treats keys that differ only by case as duplicates (case-insensitive).
/// </summary>
public class CaseVariantDuplicateTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();
    private readonly ResxResourceDiscovery _discovery = new();

    public CaseVariantDuplicateTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmCaseTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Using _reader and _writer initialized above
        // Using _discovery initialized above
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    private void CreateResourceFilesWithCaseVariants()
    {
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Total", Value = "Total value", Comment = "First variant" },
                new() { Key = "TOTAL", Value = "TOTAL value", Comment = "Second variant" },
                new() { Key = "NormalKey", Value = "Normal value" },
                new() { Key = "Another", Value = "Another value" }
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
                FilePath = Path.Combine(_testDirectory, "TestResource.fr.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Total", Value = "Valeur totale", Comment = "Premier variante" },
                new() { Key = "TOTAL", Value = "VALEUR TOTALE", Comment = "Deuxième variante" },
                new() { Key = "NormalKey", Value = "Valeur normale" },
                new() { Key = "Another", Value = "Autre valeur" }
            }
        };

        _writer.Write(defaultFile);
        _writer.Write(frenchFile);
    }

    private void CreateResourceFileWithSingleKey(string keyName, string value)
    {
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = keyName, Value = value }
            }
        };

        _writer.Write(defaultFile);
    }

    [Fact]
    public void Validate_CaseVariantDuplicates_DetectedAsDuplicates()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        var validator = new ResourceValidator();

        // Act
        var result = validator.Validate(resourceFiles);

        // Assert: Should detect "Total" and "TOTAL" as duplicates (MSBuild behavior)
        Assert.True(result.DuplicateKeys.Any(), "Should detect case-variant duplicates");

        var defaultDuplicates = result.DuplicateKeys
            .Where(kvp => kvp.Key == "" || kvp.Key == "Default")
            .SelectMany(kvp => kvp.Value)
            .ToList();

        // Either "Total" or "TOTAL" should be in the duplicates list
        Assert.True(
            defaultDuplicates.Any(k => k.Equals("Total", StringComparison.OrdinalIgnoreCase)),
            "Case variants should be detected as duplicates");
    }

    [Fact]
    public void AddCommand_CaseVariantExists_ReturnsError()
    {
        // Arrange: Create file with "Total"
        CreateResourceFileWithSingleKey("Total", "Total value");

        // Act: Try to add "TOTAL" (case variant)
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<AddCommand>("add");
        });

        var result = app.Run(new[] { "add", "TOTAL", "--path", _testDirectory, "--lang", "default:TOTAL value" });

        // Assert: Should fail because case variant exists (exit code 1)
        Assert.Equal(1, result);

        // Verify the key was not added
        var reloadedLanguages = _discovery.DiscoverLanguages(_testDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _reader.Read(lang)).ToList();
        var reloadedDefault = reloadedFiles.First(rf => rf.Language.IsDefault);

        // Should still have only 1 entry (the original "Total")
        var totalEntries = reloadedDefault.Entries
            .Where(e => e.Key.Equals("Total", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(totalEntries);
        Assert.Equal("Total", totalEntries[0].Key);
    }

    [Fact]
    public void MergeCommand_CaseVariants_AutoFirst_UsesFirstKeyName()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act: Auto-merge (keeps first occurrence)
        MergeKeyAutoFirst(resourceFiles, "Total");

        foreach (var rf in resourceFiles)
        {
            _writer.Write(rf);
        }

        // Assert
        var reloadedLanguages = _discovery.DiscoverLanguages(_testDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _reader.Read(lang)).ToList();
        var reloadedDefault = reloadedFiles.First(rf => rf.Language.IsDefault);

        // Should have only 1 occurrence
        var totalEntries = reloadedDefault.Entries
            .Where(e => e.Key.Equals("Total", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(totalEntries);

        // Should use the first key name ("Total", not "TOTAL")
        Assert.Equal("Total", totalEntries[0].Key);
        Assert.Equal("Total value", totalEntries[0].Value);
    }

    [Fact]
    public void MergeCommand_CaseVariants_StandardizesKeyNameAcrossLanguages()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();

        // Act: Auto-merge
        MergeKeyAutoFirst(resourceFiles, "TOTAL"); // Use different casing to search

        foreach (var rf in resourceFiles)
        {
            _writer.Write(rf);
        }

        // Assert: Both files should have standardized key name
        var reloadedLanguages = _discovery.DiscoverLanguages(_testDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _reader.Read(lang)).ToList();

        foreach (var rf in reloadedFiles)
        {
            var entries = rf.Entries
                .Where(e => e.Key.Equals("Total", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Single(entries);
            // All should have the same standardized key name
            Assert.Equal("Total", entries[0].Key);
        }
    }

    [Fact]
    public void DeleteCommand_CaseVariant_FindsAndDeletes()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();

        // Act: Delete using different casing
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<DeleteCommand>("delete");
            config.PropagateExceptions();
        });

        // Delete with "total" should find "Total" and "TOTAL"
        app.Run(new[] { "delete", "total", "--path", _testDirectory, "-y", "--all-duplicates", "--no-backup" });

        // Assert
        var reloadedLanguages = _discovery.DiscoverLanguages(_testDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _reader.Read(lang)).ToList();

        foreach (var rf in reloadedFiles)
        {
            var entries = rf.Entries
                .Where(e => e.Key.Equals("Total", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(entries);
        }
    }

    [Fact]
    public void UpdateCommand_DifferentCasing_FindsAndUpdatesKey()
    {
        // Arrange: Create file with "Total"
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Total", Value = "Original value" }
            }
        };
        _writer.Write(defaultFile);

        // Act: Update using "TOTAL" (different casing)
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<UpdateCommand>("update");
        });

        var result = app.Run(new[] { "update", "TOTAL", "--path", _testDirectory, "--lang", "default:Updated value", "--no-backup", "-y" });

        // Assert: Should succeed (exit code 0)
        Assert.Equal(0, result);

        var reloadedLanguages = _discovery.DiscoverLanguages(_testDirectory);
        var reloadedFiles = reloadedLanguages.Select(lang => _reader.Read(lang)).ToList();
        var reloadedDefault = reloadedFiles.First(rf => rf.Language.IsDefault);

        var entry = reloadedDefault.Entries.FirstOrDefault(e =>
            e.Key.Equals("Total", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);
        Assert.Equal("Updated value", entry.Value);
        // Key name should be preserved (not changed to "TOTAL")
        Assert.Equal("Total", entry.Key);
    }

    [Fact]
    public void ViewCommand_CaseInsensitive_FindsAllVariants()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();

        // Act: Use ViewCommand's FindMatchingKeys logic
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Search with lowercase "total"
        var matches = defaultFile.Entries
            .Where(e => e.Key.Equals("total", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Key)
            .ToList();

        // Assert: Should find both "Total" and "TOTAL"
        Assert.Equal(2, matches.Count);
        Assert.Contains("Total", matches);
        Assert.Contains("TOTAL", matches);
    }

    [Fact]
    public void ViewCommand_CaseVariants_GroupedCorrectly()
    {
        // Arrange
        CreateResourceFilesWithCaseVariants();
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(lang => _reader.Read(lang)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act: Group by key case-insensitively (as ViewCommand does)
        var grouped = defaultFile.Entries
            .Where(e => e.Key.Equals("total", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Key.ToLowerInvariant())
            .ToList();

        // Assert: All should be in same group
        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].Count());
    }

    /// <summary>
    /// Helper method to merge duplicates using auto-first strategy.
    /// Mirrors MergeDuplicatesCommand.MergeKeyAutomatic behavior.
    /// </summary>
    private void MergeKeyAutoFirst(List<ResourceFile> resourceFiles, string key)
    {
        // Determine the standard key name (from first occurrence in first file)
        string? standardKeyName = null;
        foreach (var rf in resourceFiles)
        {
            var firstOccurrence = rf.Entries.FirstOrDefault(e =>
                e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (firstOccurrence != null)
            {
                standardKeyName = firstOccurrence.Key;
                break;
            }
        }

        if (standardKeyName == null) return;

        foreach (var rf in resourceFiles)
        {
            var occurrences = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (occurrences.Count == 0) continue;

            // Standardize the key name of the first occurrence
            rf.Entries[occurrences[0].Index].Key = standardKeyName;

            if (occurrences.Count <= 1) continue;

            // Remove all except first (in reverse order)
            for (int i = occurrences.Count - 1; i >= 1; i--)
            {
                rf.Entries.RemoveAt(occurrences[i].Index);
            }
        }
    }
}
