// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Data;
using LocalizationManager.Core.Models;
using LocalizationManager.UI.Filters;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class ResourceFilterServiceTests
{
    private readonly ResourceFilterService _filterService;

    public ResourceFilterServiceTests()
    {
        _filterService = new ResourceFilterService();
    }

    private DataTable CreateTestDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Key", typeof(string));
        dt.Columns.Add("English", typeof(string));
        dt.Columns.Add("French", typeof(string));
        dt.Columns.Add("_Visible", typeof(bool));
        dt.Columns.Add("_HasExtraKey", typeof(bool));

        dt.Rows.Add("Save", "Save", "Enregistrer", true, false);
        dt.Rows.Add("Cancel", "Cancel", "Annuler", true, false);
        dt.Rows.Add("Error.NotFound", "Not Found", "Non trouvé", true, false);
        dt.Rows.Add("Error.Invalid", "Invalid", "Invalide", true, false);

        return dt;
    }

    [Fact]
    public void FilterRows_EmptySearchText_ReturnsAllRows()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria { SearchText = "" };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void FilterRows_SubstringMode_CaseInsensitive_MatchesCorrectRows()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "error",
            Mode = FilterMode.Substring,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Equal(2, result.Count); // Error.NotFound and Error.Invalid
    }

    [Fact]
    public void FilterRows_SubstringMode_CaseSensitive_MatchesCorrectRows()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "Error",
            Mode = FilterMode.Substring,
            CaseSensitive = true,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Equal(2, result.Count); // Error.NotFound and Error.Invalid
    }

    [Fact]
    public void FilterRows_WildcardMode_MatchesPattern()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "Error.*",
            Mode = FilterMode.Wildcard,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Equal(2, result.Count); // Error.NotFound and Error.Invalid
    }

    [Fact]
    public void FilterRows_RegexMode_MatchesPattern()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "^Error\\.",
            Mode = FilterMode.Regex,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Equal(2, result.Count); // Error.NotFound and Error.Invalid
    }

    [Fact]
    public void FilterRows_KeysAndValues_SearchesInBothColumns()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "Annuler",
            Mode = FilterMode.Substring,
            CaseSensitive = false,
            Scope = SearchScope.KeysAndValues
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Single(result); // Only "Cancel" row
    }

    [Fact]
    public void FilterRows_KeysOnly_DoesNotSearchInValues()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "Annuler",
            Mode = FilterMode.Substring,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Empty(result); // Should not find in values
    }

    [Fact]
    public void FilterRows_InvalidRegex_ReturnsEmptyList()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "[invalid(regex",
            Mode = FilterMode.Regex,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.FilterRows(table, criteria);

        // Assert
        Assert.Empty(result); // Invalid regex should return no results
    }

    [Fact]
    public void IsWildcardPattern_WithAsterisk_ReturnsTrue()
    {
        // Arrange
        var pattern = "Error*";

        // Act
        var result = ResourceFilterService.IsWildcardPattern(pattern);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsWildcardPattern_WithQuestionMark_ReturnsTrue()
    {
        // Arrange
        var pattern = "Error?";

        // Act
        var result = ResourceFilterService.IsWildcardPattern(pattern);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsWildcardPattern_WithEscapedWildcard_ReturnsFalse()
    {
        // Arrange
        var pattern = @"Error\*";

        // Act
        var result = ResourceFilterService.IsWildcardPattern(pattern);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsWildcardPattern_NoWildcards_ReturnsFalse()
    {
        // Arrange
        var pattern = "Error.NotFound";

        // Act
        var result = ResourceFilterService.IsWildcardPattern(pattern);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ConvertWildcardToRegex_SimpleAsterisk_ConvertsCorrectly()
    {
        // Arrange
        var pattern = "Error*";

        // Act
        var result = ResourceFilterService.ConvertWildcardToRegex(pattern);

        // Assert
        Assert.Equal("^Error.*$", result);
    }

    [Fact]
    public void ConvertWildcardToRegex_SimpleQuestionMark_ConvertsCorrectly()
    {
        // Arrange
        var pattern = "Error?";

        // Act
        var result = ResourceFilterService.ConvertWildcardToRegex(pattern);

        // Assert
        Assert.Equal("^Error.$", result);
    }

    [Fact]
    public void ConvertWildcardToRegex_EscapedWildcard_ConvertsToLiteral()
    {
        // Arrange
        var pattern = @"Error\*";

        // Act
        var result = ResourceFilterService.ConvertWildcardToRegex(pattern);

        // Assert
        Assert.Contains(@"\*", result);
    }

    [Fact]
    public void ConvertWildcardToRegex_SpecialRegexCharacters_EscapesProperly()
    {
        // Arrange
        var pattern = "Error.NotFound*";

        // Act
        var result = ResourceFilterService.ConvertWildcardToRegex(pattern);

        // Assert
        Assert.Contains(@"\.", result); // Dot should be escaped
    }

    [Fact]
    public void ParseCultureCodes_CommaSeparated_ReturnsNormalizedList()
    {
        // Arrange
        var input = "en, FR, de-DE, es";

        // Act
        var result = ResourceFilterService.ParseCultureCodes(input);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains("en", result);
        Assert.Contains("fr", result);
        Assert.Contains("de-de", result);
        Assert.Contains("es", result);
    }

    [Fact]
    public void ParseCultureCodes_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var input = "";

        // Act
        var result = ResourceFilterService.ParseCultureCodes(input);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCultureCodes_WithDuplicates_RemovesDuplicates()
    {
        // Arrange
        var input = "en, EN, en, fr";

        // Act
        var result = ResourceFilterService.ParseCultureCodes(input);

        // Assert
        Assert.Equal(2, result.Count); // Only en and fr
    }

    [Fact]
    public void DetectExtraKeysInFilteredFiles_WithExtraKeys_ReturnsCorrectKeys()
    {
        // Arrange
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo { Code = "", Name = "Default", IsDefault = true, BaseName = "Test", FilePath = "" },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save" },
                new() { Key = "Cancel", Value = "Cancel" }
            }
        };

        var frenchFile = new ResourceFile
        {
            Language = new LanguageInfo { Code = "fr", Name = "French", IsDefault = false, BaseName = "Test", FilePath = "" },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Enregistrer" },
                new() { Key = "Cancel", Value = "Annuler" },
                new() { Key = "ExtraKey", Value = "Extra" } // Extra key not in default
            }
        };

        var resourceFiles = new List<ResourceFile> { defaultFile, frenchFile };

        // Act
        var result = ResourceFilterService.DetectExtraKeysInFilteredFiles(defaultFile, resourceFiles);

        // Assert
        Assert.Single(result); // Only French has extra keys
        Assert.Contains("French", result.Keys);
        Assert.Single(result["French"]); // One extra key
        Assert.Contains("ExtraKey", result["French"]);
    }

    [Fact]
    public void DetectExtraKeysInFilteredFiles_NoExtraKeys_ReturnsEmptyDictionary()
    {
        // Arrange
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo { Code = "", Name = "Default", IsDefault = true, BaseName = "Test", FilePath = "" },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save" },
                new() { Key = "Cancel", Value = "Cancel" }
            }
        };

        var frenchFile = new ResourceFile
        {
            Language = new LanguageInfo { Code = "fr", Name = "French", IsDefault = false, BaseName = "Test", FilePath = "" },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Enregistrer" },
                new() { Key = "Cancel", Value = "Annuler" }
            }
        };

        var resourceFiles = new List<ResourceFile> { defaultFile, frenchFile };

        // Act
        var result = ResourceFilterService.DetectExtraKeysInFilteredFiles(defaultFile, resourceFiles);

        // Assert
        Assert.Empty(result); // No extra keys
    }

    [Fact]
    public void MatchesFilter_WithWarningMarker_StillMatchesKey()
    {
        // Arrange
        var dt = new DataTable();
        dt.Columns.Add("Key", typeof(string));
        dt.Columns.Add("English", typeof(string));
        dt.Columns.Add("_Visible", typeof(bool));
        dt.Columns.Add("_HasExtraKey", typeof(bool));

        var row = dt.NewRow();
        row["Key"] = "⚠ ExtraKey"; // Key with warning marker
        row["English"] = "Extra";
        row["_Visible"] = true;
        row["_HasExtraKey"] = true;
        dt.Rows.Add(row);

        var criteria = new FilterCriteria
        {
            SearchText = "ExtraKey",
            Mode = FilterMode.Substring,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // Act
        var result = _filterService.MatchesFilter(row, criteria);

        // Assert
        Assert.True(result); // Should strip warning marker and match
    }

    [Fact]
    public void ClearCache_ClearsRegexCache()
    {
        // Arrange
        var table = CreateTestDataTable();
        var criteria = new FilterCriteria
        {
            SearchText = "Error*",
            Mode = FilterMode.Wildcard,
            CaseSensitive = false,
            Scope = SearchScope.KeysOnly
        };

        // First call to populate cache
        _filterService.FilterRows(table, criteria);

        // Act
        _filterService.ClearCache();

        // Assert
        // Second call should work without errors (cache was cleared)
        var result = _filterService.FilterRows(table, criteria);
        Assert.Equal(2, result.Count);
    }
}
