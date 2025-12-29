// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Po;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Po;

public class PoPluralMapperTests
{
    #region GetPluralCount Tests

    [Theory]
    [InlineData("en", 2)]
    [InlineData("de", 2)]
    [InlineData("fr", 2)]
    [InlineData("ja", 1)]
    [InlineData("ko", 1)]
    [InlineData("zh", 1)]
    [InlineData("ru", 3)]
    [InlineData("pl", 3)]
    [InlineData("ar", 6)]
    public void GetPluralCount_KnownLanguage_ReturnsCorrectCount(string langCode, int expectedCount)
    {
        // Act
        var result = PoPluralMapper.GetPluralCount(langCode);

        // Assert
        Assert.Equal(expectedCount, result);
    }

    [Fact]
    public void GetPluralCount_UnknownLanguage_ReturnsDefault()
    {
        // Act
        var result = PoPluralMapper.GetPluralCount("xx");

        // Assert
        Assert.Equal(2, result); // Default is 2 (like English)
    }

    [Fact]
    public void GetPluralCount_RegionalVariant_UsesBaseLanguage()
    {
        // Act
        var result = PoPluralMapper.GetPluralCount("en-US");

        // Assert
        Assert.Equal(2, result);
    }

    #endregion

    #region IndexToCategory Tests

    [Theory]
    [InlineData("en", 0, "one")]
    [InlineData("en", 1, "other")]
    [InlineData("fr", 0, "one")]
    [InlineData("fr", 1, "other")]
    [InlineData("ru", 0, "one")]
    [InlineData("ru", 1, "few")]
    [InlineData("ru", 2, "many")]
    [InlineData("ja", 0, "other")]
    public void IndexToCategory_KnownLanguage_ReturnsCorrectCategory(string langCode, int index, string expectedCategory)
    {
        // Act
        var result = PoPluralMapper.IndexToCategory(langCode, index);

        // Assert
        Assert.Equal(expectedCategory, result);
    }

    [Theory]
    [InlineData("ar", 0, "zero")]
    [InlineData("ar", 1, "one")]
    [InlineData("ar", 2, "two")]
    [InlineData("ar", 3, "few")]
    [InlineData("ar", 4, "many")]
    [InlineData("ar", 5, "other")]
    public void IndexToCategory_Arabic_ReturnsSixForms(string langCode, int index, string expectedCategory)
    {
        // Act
        var result = PoPluralMapper.IndexToCategory(langCode, index);

        // Assert
        Assert.Equal(expectedCategory, result);
    }

    #endregion

    #region CategoryToIndex Tests

    [Theory]
    [InlineData("en", "one", 0)]
    [InlineData("en", "other", 1)]
    [InlineData("ru", "one", 0)]
    [InlineData("ru", "few", 1)]
    [InlineData("ru", "many", 2)]
    public void CategoryToIndex_KnownLanguage_ReturnsCorrectIndex(string langCode, string category, int expectedIndex)
    {
        // Act
        var result = PoPluralMapper.CategoryToIndex(langCode, category);

        // Assert
        Assert.Equal(expectedIndex, result);
    }

    [Fact]
    public void CategoryToIndex_UnknownCategory_ReturnsMinusOne()
    {
        // Act
        var result = PoPluralMapper.CategoryToIndex("en", "invalid");

        // Assert
        Assert.Equal(-1, result);
    }

    #endregion

    #region GetCategoriesForLanguage Tests

    [Fact]
    public void GetCategoriesForLanguage_English_ReturnsTwoCategories()
    {
        // Act
        var result = PoPluralMapper.GetCategoriesForLanguage("en");

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal("one", result[0]);
        Assert.Equal("other", result[1]);
    }

    [Fact]
    public void GetCategoriesForLanguage_Russian_ReturnsThreeCategories()
    {
        // Act
        var result = PoPluralMapper.GetCategoriesForLanguage("ru");

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Contains("one", result);
        Assert.Contains("few", result);
        Assert.Contains("many", result);
    }

    [Fact]
    public void GetCategoriesForLanguage_Japanese_ReturnsOneCategory()
    {
        // Act
        var result = PoPluralMapper.GetCategoriesForLanguage("ja");

        // Assert
        Assert.Single(result);
        Assert.Equal("other", result[0]);
    }

    [Fact]
    public void GetCategoriesForLanguage_Arabic_ReturnsSixCategories()
    {
        // Act
        var result = PoPluralMapper.GetCategoriesForLanguage("ar");

        // Assert
        Assert.Equal(6, result.Length);
    }

    [Fact]
    public void GetCategoriesForLanguage_UnknownLanguage_ReturnsEnglishDefault()
    {
        // Act
        var result = PoPluralMapper.GetCategoriesForLanguage("xx");

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal("one", result[0]);
        Assert.Equal("other", result[1]);
    }

    #endregion

    #region Round-Trip Tests

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("ru")]
    [InlineData("ar")]
    [InlineData("ja")]
    public void RoundTrip_IndexToCategory_CategoryToIndex_Consistent(string langCode)
    {
        // Arrange
        var categories = PoPluralMapper.GetCategoriesForLanguage(langCode);

        // Act & Assert
        for (int i = 0; i < categories.Length; i++)
        {
            var category = PoPluralMapper.IndexToCategory(langCode, i);
            var index = PoPluralMapper.CategoryToIndex(langCode, category);
            Assert.Equal(i, index);
        }
    }

    #endregion
}
