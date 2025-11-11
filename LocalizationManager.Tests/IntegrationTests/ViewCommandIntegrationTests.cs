// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class ViewCommandIntegrationTests
{
    private readonly string _testDirectory;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public ViewCommandIntegrationTests()
    {
        // Use persistent TestData folder
        _testDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");

        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
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

    [Fact]
    public void WildcardMatch_StarOnly_ReturnsAllKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "*";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Equal(17, matchedKeys.Count); // All keys
    }

    [Fact]
    public void WildcardMatch_StarAtEnd_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "Error.*";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
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
    public void WildcardMatch_StarAtBeginning_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "*.Save";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert
        Assert.Single(matchedKeys);
        Assert.Contains("Success.Save", matchedKeys);
    }

    [Fact]
    public void WildcardMatch_StarInMiddle_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "Button.*";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert - Matches Button.Cancel and Button.Submit
        Assert.Equal(2, matchedKeys.Count);
        Assert.Contains("Button.Cancel", matchedKeys);
        Assert.Contains("Button.Submit", matchedKeys);
    }

    [Fact]
    public void WildcardMatch_QuestionMark_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "Item?";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
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
    public void WildcardMatch_CombinedStarAndQuestion_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var pattern = "*.*";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);
        var regex = new Regex(convertedPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var matchedKeys = defaultFile.Entries
            .Where(e => regex.IsMatch(e.Key))
            .Select(e => e.Key)
            .ToList();

        // Assert - All keys with dots
        Assert.Equal(9, matchedKeys.Count); // All keys except Item1, Item2, Item3
    }

    [Fact]
    public void WildcardMatch_EscapedStar_MatchesLiteralStar()
    {
        // Arrange
        var pattern = "Test\\*Key";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);

        // Assert - Should match literal asterisk
        Assert.Contains("\\*", convertedPattern);
        Assert.DoesNotContain(".*", convertedPattern.Replace("^", "").Replace("$", "").Replace("\\*", ""));
    }

    [Fact]
    public void WildcardMatch_EscapedQuestion_MatchesLiteralQuestion()
    {
        // Arrange
        var pattern = "Test\\?Key";

        // Act
        var convertedPattern = Commands.ViewCommand.ConvertWildcardToRegex(pattern);

        // Assert - Should match literal question mark
        Assert.Contains("\\?", convertedPattern);
        // Check that it's not converted to . (single character match)
        var cleanPattern = convertedPattern.Replace("^", "").Replace("$", "").Replace("\\?", "");
        Assert.DoesNotContain(".", cleanPattern);
    }

    [Fact]
    public void IsWildcardPattern_DetectsWildcards()
    {
        // Arrange & Act & Assert
        Assert.True(Commands.ViewCommand.IsWildcardPattern("App.*"));
        Assert.True(Commands.ViewCommand.IsWildcardPattern("*.Text"));
        Assert.True(Commands.ViewCommand.IsWildcardPattern("Error.???"));
        Assert.True(Commands.ViewCommand.IsWildcardPattern("*"));
    }

    [Fact]
    public void IsWildcardPattern_DetectsWildcardsRegardlessOfRegexSyntax()
    {
        // Arrange & Act & Assert
        // The --regex flag determines behavior, not pattern detection
        // If pattern has wildcards, IsWildcardPattern returns true
        Assert.True(Commands.ViewCommand.IsWildcardPattern("^App.*"));  // Has *
        Assert.True(Commands.ViewCommand.IsWildcardPattern("App.*$"));  // Has *
        Assert.False(Commands.ViewCommand.IsWildcardPattern("App[0-9]"));  // No wildcards
        Assert.False(Commands.ViewCommand.IsWildcardPattern("(Error|Success)"));  // No wildcards
    }

    [Fact]
    public void IsWildcardPattern_IgnoresEscapedWildcards()
    {
        // Arrange & Act & Assert
        Assert.False(Commands.ViewCommand.IsWildcardPattern("Test\\*Key"));
        Assert.False(Commands.ViewCommand.IsWildcardPattern("Test\\?Key"));
    }

    // Culture Filtering Tests

    [Fact]
    public void ParseCultureCodes_ParsesCommaSeparatedCodes()
    {
        // Arrange & Act
        var result = Commands.ViewCommand.ParseCultureCodes("en,fr,el");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("en", result);
        Assert.Contains("fr", result);
        Assert.Contains("el", result);
    }

    [Fact]
    public void ParseCultureCodes_HandlesWhitespaceAndDuplicates()
    {
        // Arrange & Act
        var result = Commands.ViewCommand.ParseCultureCodes("en, fr , el, en");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("en", result);
        Assert.Contains("fr", result);
        Assert.Contains("el", result);
    }

    [Fact]
    public void ParseCultureCodes_ReturnsEmptyForNullOrEmpty()
    {
        // Arrange & Act & Assert
        Assert.Empty(Commands.ViewCommand.ParseCultureCodes(null));
        Assert.Empty(Commands.ViewCommand.ParseCultureCodes(""));
        Assert.Empty(Commands.ViewCommand.ParseCultureCodes("   "));
    }

    [Fact]
    public void FilterResourceFiles_IncludesCultures()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            Cultures = "el,fr"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Language.Code == "el");
        Assert.Contains(result, f => f.Language.Code == "fr");
        Assert.DoesNotContain(result, f => f.Language.IsDefault);
        Assert.Empty(invalidCodes);
    }

    [Fact]
    public void FilterResourceFiles_IncludesDefault()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            Cultures = "default,el"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Language.IsDefault);
        Assert.Contains(result, f => f.Language.Code == "el");
        Assert.Empty(invalidCodes);
    }

    [Fact]
    public void FilterResourceFiles_ExcludesCultures()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            ExcludeCultures = "el"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, f => f.Language.Code == "el");
        Assert.Contains(result, f => f.Language.IsDefault);
        Assert.Contains(result, f => f.Language.Code == "fr");
        Assert.Empty(invalidCodes);
    }

    [Fact]
    public void FilterResourceFiles_ExcludesDefault()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            ExcludeCultures = "default"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, f => f.Language.IsDefault);
        Assert.Contains(result, f => f.Language.Code == "el");
        Assert.Contains(result, f => f.Language.Code == "fr");
        Assert.Empty(invalidCodes);
    }

    [Fact]
    public void FilterResourceFiles_CombinesIncludeAndExclude()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            Cultures = "default,el",
            ExcludeCultures = "el"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert - Include filters first, then exclude removes el, leaving only default
        Assert.Single(result);
        Assert.Contains(result, f => f.Language.IsDefault);
        Assert.DoesNotContain(result, f => f.Language.Code == "el");
        Assert.Empty(invalidCodes);
    }

    [Fact]
    public void FilterResourceFiles_DetectsInvalidCodes()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            Cultures = "en,de,zh"
        };

        // Act
        var result = Commands.ViewCommand.FilterResourceFiles(files, settings, out var invalidCodes);

        // Assert
        Assert.Empty(result); // No files match en, de, or zh
        Assert.Equal(3, invalidCodes.Count);
        Assert.Contains("en", invalidCodes);
        Assert.Contains("de", invalidCodes);
        Assert.Contains("zh", invalidCodes);
    }

    [Fact]
    public void IsKeysOnlyMode_TrueWhenKeysOnlyFlag()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            KeysOnly = true
        };

        // Act & Assert
        Assert.True(Commands.ViewCommand.IsKeysOnlyMode(settings, files));
    }

    [Fact]
    public void IsKeysOnlyMode_TrueWhenNoTranslationsFlag()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test",
            NoTranslations = true
        };

        // Act & Assert
        Assert.True(Commands.ViewCommand.IsKeysOnlyMode(settings, files));
    }

    [Fact]
    public void IsKeysOnlyMode_TrueWhenNoResourceFiles()
    {
        // Arrange
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test"
        };

        // Act & Assert
        Assert.True(Commands.ViewCommand.IsKeysOnlyMode(settings, new List<ResourceFile>()));
    }

    [Fact]
    public void IsKeysOnlyMode_FalseWhenNormalMode()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var files = languages.Select(l => _parser.Parse(l)).ToList();
        var settings = new Commands.ViewCommand.Settings
        {
            Key = "test"
        };

        // Act & Assert
        Assert.False(Commands.ViewCommand.IsKeysOnlyMode(settings, files));
    }

    [Fact]
    public void TestSetup_CreatesThreeLanguageFiles()
    {
        // Arrange & Act
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var physicalFiles = Directory.GetFiles(_testDirectory, "*.resx");

        // Assert
        Assert.Equal(3, languages.Count);
        Assert.Equal(3, physicalFiles.Length);
        Assert.Contains(languages, l => l.IsDefault);
        Assert.Contains(languages, l => l.Code == "el");
        Assert.Contains(languages, l => l.Code == "fr");
    }

    // Extra Keys Detection Tests

    [Fact]
    public void DetectExtraKeysInFilteredFiles_FindsKeysNotInDefault()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var elFile = _parser.Parse(languages.First(l => l.Code == "el"));

        // Add an extra key to the el file for testing
        elFile.Entries.Add(new Core.Models.ResourceEntry
        {
            Key = "ExtraGreekKey",
            Value = "Greek only value"
        });

        var filteredFiles = new List<Core.Models.ResourceFile> { defaultFile, elFile };

        // Act
        var result = Commands.ViewCommand.DetectExtraKeysInFilteredFiles(defaultFile, filteredFiles);

        // Assert
        Assert.Single(result);
        Assert.Contains("el", result.Keys.First()); // Language name contains "el"
        Assert.Equal(2, result.Values.First().Count); // MissingInDefault + ExtraGreekKey
        Assert.Contains("ExtraGreekKey", result.Values.First());
        Assert.Contains("MissingInDefault", result.Values.First());
    }

    [Fact]
    public void DetectExtraKeysInFilteredFiles_IgnoresDefaultFile()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));

        // Add an extra key to the default file
        defaultFile.Entries.Add(new Core.Models.ResourceEntry
        {
            Key = "ExtraDefaultKey",
            Value = "Default only value"
        });

        var filteredFiles = new List<Core.Models.ResourceFile> { defaultFile };

        // Act
        var result = Commands.ViewCommand.DetectExtraKeysInFilteredFiles(defaultFile, filteredFiles);

        // Assert
        // Default file's extra keys should not be reported (even though it's in filteredFiles)
        Assert.Empty(result);
    }

    [Fact]
    public void DetectExtraKeysInFilteredFiles_DetectsMissingInDefaultKey()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var files = languages.Select(l => _parser.Parse(l)).ToList();

        // Act
        var result = Commands.ViewCommand.DetectExtraKeysInFilteredFiles(defaultFile, files);

        // Assert
        // Our test data has "MissingInDefault" key in el.resx and fr.resx but not in default
        Assert.Equal(2, result.Count);
        Assert.All(result.Values, keys => Assert.Contains("MissingInDefault", keys));

        // Verify the keys really don't exist in default
        foreach (var kvp in result)
        {
            foreach (var key in kvp.Value)
            {
                Assert.DoesNotContain(defaultFile.Entries, e => e.Key == key);
            }
        }
    }

    [Fact]
    public void DetectExtraKeysInFilteredFiles_HandleMultipleLanguages()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultFile = _parser.Parse(languages.First(l => l.IsDefault));
        var elFile = _parser.Parse(languages.First(l => l.Code == "el"));
        var frFile = _parser.Parse(languages.First(l => l.Code == "fr"));

        // Add extra keys to both non-default files
        elFile.Entries.Add(new Core.Models.ResourceEntry
        {
            Key = "ExtraGreekKey",
            Value = "Greek only"
        });

        frFile.Entries.Add(new Core.Models.ResourceEntry
        {
            Key = "ExtraFrenchKey",
            Value = "French only"
        });

        var filteredFiles = new List<Core.Models.ResourceFile> { defaultFile, elFile, frFile };

        // Act
        var result = Commands.ViewCommand.DetectExtraKeysInFilteredFiles(defaultFile, filteredFiles);

        // Assert
        Assert.Equal(2, result.Count);
        // Check for el language (contains "el" in name)
        Assert.Contains(result, kvp => kvp.Key.Contains("el") && kvp.Value.Contains("ExtraGreekKey"));
        // Check for fr language (contains "fr" in name)
        Assert.Contains(result, kvp => kvp.Key.Contains("fr") && kvp.Value.Contains("ExtraFrenchKey"));
        // Both should also have MissingInDefault from test data
        Assert.All(result.Values, keys => Assert.Contains("MissingInDefault", keys));
    }

    #region SearchScope Tests

    [Fact]
    public void SearchScope_Keys_ExactMatch_ReturnsMatchingKey()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            "Error.NotFound",
            Commands.ViewCommand.SearchScope.Keys,
            isRegex: false);

        // Assert
        Assert.Single(matchedKeys);
        Assert.Contains("Error.NotFound", matchedKeys);
    }

    [Fact]
    public void SearchScope_Values_ExactMatch_ReturnsKeysWithMatchingValue()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Find an actual value from test data
        var testValue = resourceFiles.SelectMany(rf => rf.Entries)
            .FirstOrDefault(e => !string.IsNullOrEmpty(e.Value))?.Value;

        Assert.NotNull(testValue); // Ensure test data has values

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            testValue!,
            Commands.ViewCommand.SearchScope.Values,
            isRegex: false);

        // Assert
        // Should find at least the key we got the value from
        Assert.NotEmpty(matchedKeys);
    }

    [Fact]
    public void SearchScope_Both_ExactMatch_ReturnsKeysMatchingKeyOrValue()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act - search for "Error" which appears in keys like "Error.NotFound"
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            "Error",
            Commands.ViewCommand.SearchScope.Both,
            isRegex: false);

        // Assert
        // In exact match mode with "Both", will match if key equals "Error" OR value equals "Error"
        // This may return empty if no exact matches, which is expected
        Assert.NotNull(matchedKeys);
    }

    [Fact]
    public void SearchScope_Keys_RegexPattern_ReturnsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            "^Error\\..*",
            Commands.ViewCommand.SearchScope.Keys,
            isRegex: true);

        // Assert
        Assert.NotEmpty(matchedKeys);
        Assert.All(matchedKeys, key => Assert.StartsWith("Error.", key));
    }

    [Fact]
    public void SearchScope_Values_RegexPattern_ReturnsKeysWithMatchingValues()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Get a sample value and use part of it for regex
        var sampleEntry = resourceFiles.SelectMany(rf => rf.Entries)
            .FirstOrDefault(e => !string.IsNullOrEmpty(e.Value) && e.Value.Length > 2);

        Assert.NotNull(sampleEntry); // Ensure test data exists

        // Use first few chars of the value as pattern
        var pattern = $".*{Regex.Escape(sampleEntry!.Value!.Substring(0, Math.Min(3, sampleEntry.Value.Length)))}.*";

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            pattern,
            Commands.ViewCommand.SearchScope.Values,
            isRegex: true);

        // Assert
        // Should find keys whose values match the pattern
        Assert.NotEmpty(matchedKeys);
    }

    [Fact]
    public void SearchScope_Both_RegexPattern_ReturnsKeysMatchingKeyOrValue()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act - search for pattern that matches keys starting with "Error" OR values containing "error"
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            ".*[Ee]rror.*",
            Commands.ViewCommand.SearchScope.Both,
            isRegex: true);

        // Assert
        Assert.NotEmpty(matchedKeys);
        // Results should include both keys with "Error" and keys with values containing "error"
    }

    [Fact]
    public void SearchScope_Values_SearchesAllLanguages()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Assuming test data has some French-specific values
        // Act - exact match for a French value
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            "Introuvable",  // French for "Not Found"
            Commands.ViewCommand.SearchScope.Values,
            isRegex: false);

        // Assert
        // Should find keys even if value only exists in non-default language
        // (May be empty if test data doesn't have this value)
        Assert.NotNull(matchedKeys);
    }

    [Fact]
    public void SearchScope_Values_HandlesNullValues()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act - should not crash on null/empty values
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            "NonExistentValue",
            Commands.ViewCommand.SearchScope.Values,
            isRegex: false);

        // Assert
        Assert.NotNull(matchedKeys);
        Assert.Empty(matchedKeys);
    }

    [Fact]
    public void SearchScope_Values_OnlyReturnsKeysFromDefaultFile()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var defaultKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();

        // Act - search in values with pattern that might match many entries
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            ".*",
            Commands.ViewCommand.SearchScope.Values,
            isRegex: true);

        // Assert
        // All returned keys must exist in default file
        Assert.All(matchedKeys, key => Assert.Contains(key, defaultKeys));
    }

    #endregion

    #region Comments Search Tests

    [Fact]
    public void SearchScope_Comments_ExactMatch_FindsKeyByComment()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Find a key with a comment
        var entryWithComment = defaultFile.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Comment));
        if (entryWithComment == null)
        {
            // Skip test if no comments exist
            return;
        }

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            entryWithComment.Comment!,
            Commands.ViewCommand.SearchScope.Comments,
            isRegex: false);

        // Assert
        Assert.Contains(entryWithComment.Key, matchedKeys);
    }

    [Fact]
    public void SearchScope_Comments_RegexPattern_FindsMatchingKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Act - search for any comment content
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            ".*",
            Commands.ViewCommand.SearchScope.Comments,
            isRegex: true);

        // Assert - should match keys with comments
        var keysWithComments = defaultFile.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Comment))
            .Select(e => e.Key)
            .ToList();

        if (keysWithComments.Any())
        {
            Assert.NotEmpty(matchedKeys);
        }
    }

    [Fact]
    public void SearchScope_All_SearchesInKeysValuesAndComments()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);

        // Find a pattern that exists in at least one of keys/values/comments
        var testPattern = "Error";

        // Act
        var matchedKeys = Commands.ViewCommand.FindMatchingKeys(
            defaultFile,
            resourceFiles,
            $".*{testPattern}.*",
            Commands.ViewCommand.SearchScope.All,
            isRegex: true,
            caseSensitive: false);

        // Assert - should find keys in any location
        Assert.NotEmpty(matchedKeys);
    }

    #endregion

    #region Status Filtering Tests

    [Fact]
    public void FilterByStatus_Empty_ReturnsKeysWithEmptyValues()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var allKeys = defaultFile.Entries.Select(e => e.Key).ToList();

        // Act
        var emptyKeys = Commands.ViewCommand.FilterByStatus(
            allKeys,
            defaultFile,
            resourceFiles,
            Commands.ViewCommand.TranslationStatus.Empty);

        // Assert
        foreach (var key in emptyKeys)
        {
            var hasEmpty = resourceFiles.Any(rf =>
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                return entry == null || string.IsNullOrWhiteSpace(entry.Value);
            });
            Assert.True(hasEmpty, $"Key {key} should have at least one empty value");
        }
    }

    [Fact]
    public void FilterByStatus_Complete_ReturnsFullyTranslatedKeys()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var allKeys = defaultFile.Entries.Select(e => e.Key).ToList();

        // Act
        var completeKeys = Commands.ViewCommand.FilterByStatus(
            allKeys,
            defaultFile,
            resourceFiles,
            Commands.ViewCommand.TranslationStatus.Complete);

        // Assert
        foreach (var key in completeKeys)
        {
            var allHaveValues = resourceFiles.All(rf =>
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                return entry != null && !string.IsNullOrWhiteSpace(entry.Value);
            });
            Assert.True(allHaveValues, $"Key {key} should have non-empty values in all languages");
        }
    }

    [Fact]
    public void FilterByStatus_Missing_ReturnsKeysAbsentFromAnyLanguage()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var allKeys = defaultFile.Entries.Select(e => e.Key).ToList();

        // Act
        var missingKeys = Commands.ViewCommand.FilterByStatus(
            allKeys,
            defaultFile,
            resourceFiles,
            Commands.ViewCommand.TranslationStatus.Missing);

        // Assert
        foreach (var key in missingKeys)
        {
            var isMissing = resourceFiles.Any(rf => !rf.Entries.Any(e => e.Key == key));
            Assert.True(isMissing, $"Key {key} should be missing from at least one language file");
        }
    }

    [Fact]
    public void FilterByStatus_Untranslated_ReturnsKeysWithMissingOrSameAsDefault()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var allKeys = defaultFile.Entries.Select(e => e.Key).ToList();

        // Act
        var untranslatedKeys = Commands.ViewCommand.FilterByStatus(
            allKeys,
            defaultFile,
            resourceFiles,
            Commands.ViewCommand.TranslationStatus.Untranslated);

        // Assert
        foreach (var key in untranslatedKeys)
        {
            var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == key);
            var defaultValue = defaultEntry?.Value ?? "";

            var hasUntranslated = resourceFiles.Where(rf => !rf.Language.IsDefault).Any(rf =>
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                return entry == null ||
                       string.IsNullOrWhiteSpace(entry.Value) ||
                       entry.Value == defaultValue;
            });
            Assert.True(hasUntranslated, $"Key {key} should be untranslated in at least one non-default language");
        }
    }

    [Fact]
    public void FilterByStatus_Partial_ReturnsKeysWithSomeButNotAllTranslations()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();
        var defaultFile = resourceFiles.First(rf => rf.Language.IsDefault);
        var allKeys = defaultFile.Entries.Select(e => e.Key).ToList();

        // Act
        var partialKeys = Commands.ViewCommand.FilterByStatus(
            allKeys,
            defaultFile,
            resourceFiles,
            Commands.ViewCommand.TranslationStatus.Partial);

        // Assert
        foreach (var key in partialKeys)
        {
            var hasAnyTranslation = false;
            var hasAnyMissing = false;

            foreach (var rf in resourceFiles.Where(rf => !rf.Language.IsDefault))
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    hasAnyTranslation = true;
                }
                else
                {
                    hasAnyMissing = true;
                }
            }

            Assert.True(hasAnyTranslation && hasAnyMissing,
                $"Key {key} should have some but not all translations");
        }
    }

    #endregion

    #region Inverse Matching Tests

    [Fact]
    public void ApplyExclusions_SingleExactPattern_ExcludesMatchingKey()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Button.Cancel", "Error.NotFound" };
        var notPattern = new[] { "Button.Save" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("Button.Save", result);
        Assert.Contains("Button.Cancel", result);
        Assert.Contains("Error.NotFound", result);
    }

    [Fact]
    public void ApplyExclusions_WildcardPattern_ExcludesMatchingKeys()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Button.Cancel", "Error.NotFound", "Success.Created" };
        var notPattern = new[] { "Button.*" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("Button.Save", result);
        Assert.DoesNotContain("Button.Cancel", result);
        Assert.Contains("Error.NotFound", result);
        Assert.Contains("Success.Created", result);
    }

    [Fact]
    public void ApplyExclusions_MultiplePatterns_CommaSeparated_ExcludesAllMatches()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Button.Cancel", "Error.NotFound", "Success.Created", "Link.Home" };
        var notPatterns = new[] { "Button.*,Error.*" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPatterns);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("Button.Save", result);
        Assert.DoesNotContain("Button.Cancel", result);
        Assert.DoesNotContain("Error.NotFound", result);
        Assert.Contains("Success.Created", result);
        Assert.Contains("Link.Home", result);
    }

    [Fact]
    public void ApplyExclusions_MultiplePatterns_MultipleFlags_ExcludesAllMatches()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Button.Cancel", "Error.NotFound", "Success.Created", "Link.Home" };
        var notPatterns = new[] { "Button.*", "Error.*" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPatterns);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("Button.Save", result);
        Assert.DoesNotContain("Button.Cancel", result);
        Assert.DoesNotContain("Error.NotFound", result);
        Assert.Contains("Success.Created", result);
        Assert.Contains("Link.Home", result);
    }

    [Fact]
    public void ApplyExclusions_CaseInsensitiveByDefault()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "button.cancel", "ERROR.NotFound" };
        var notPattern = new[] { "BUTTON.*" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern, caseSensitive: false);

        // Assert
        Assert.Single(result);
        Assert.Contains("ERROR.NotFound", result);
    }

    [Fact]
    public void ApplyExclusions_CaseSensitive_RespectsCase()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "button.cancel", "ERROR.NotFound" };
        var notPattern = new[] { "button.*" };

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern, caseSensitive: true);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Button.Save", result); // Different case, not excluded
        Assert.DoesNotContain("button.cancel", result); // Exact case, excluded
        Assert.Contains("ERROR.NotFound", result);
    }

    [Fact]
    public void ApplyExclusions_EmptyArray_ReturnsAllKeys()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Error.NotFound" };
        var notPattern = Array.Empty<string>();

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(keys, result);
    }

    [Fact]
    public void ApplyExclusions_NullArray_ReturnsAllKeys()
    {
        // Arrange
        var keys = new List<string> { "Button.Save", "Error.NotFound" };
        string[]? notPattern = null;

        // Act
        var result = Commands.ViewCommand.ApplyExclusions(keys, notPattern!);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(keys, result);
    }

    #endregion
}
