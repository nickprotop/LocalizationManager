// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.JsonLocalization.Core;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class PluralResolverTests
{
    #region English

    [Theory]
    [InlineData(0, "other")]
    [InlineData(1, "one")]
    [InlineData(2, "other")]
    [InlineData(5, "other")]
    [InlineData(10, "other")]
    [InlineData(100, "other")]
    public void GetPluralForm_English_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("en");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region French

    [Theory]
    [InlineData(0, "one")]   // French: 0 is singular
    [InlineData(1, "one")]
    [InlineData(2, "other")]
    [InlineData(5, "other")]
    public void GetPluralForm_French_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("fr");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Russian (Slavic)

    [Theory]
    [InlineData(0, "many")]
    [InlineData(1, "one")]
    [InlineData(2, "few")]
    [InlineData(3, "few")]
    [InlineData(4, "few")]
    [InlineData(5, "many")]
    [InlineData(10, "many")]
    [InlineData(11, "many")]
    [InlineData(12, "many")]
    [InlineData(14, "many")]
    [InlineData(20, "many")]
    [InlineData(21, "one")]
    [InlineData(22, "few")]
    [InlineData(25, "many")]
    [InlineData(100, "many")]
    [InlineData(101, "one")]
    [InlineData(102, "few")]
    public void GetPluralForm_Russian_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("ru");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Polish

    [Theory]
    [InlineData(0, "many")]
    [InlineData(1, "one")]
    [InlineData(2, "few")]
    [InlineData(3, "few")]
    [InlineData(4, "few")]
    [InlineData(5, "many")]
    [InlineData(10, "many")]
    [InlineData(12, "many")]
    [InlineData(22, "few")]
    [InlineData(25, "many")]
    public void GetPluralForm_Polish_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("pl");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Czech

    [Theory]
    [InlineData(0, "other")]
    [InlineData(1, "one")]
    [InlineData(2, "few")]
    [InlineData(3, "few")]
    [InlineData(4, "few")]
    [InlineData(5, "other")]
    [InlineData(10, "other")]
    public void GetPluralForm_Czech_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("cs");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Arabic

    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "few")]
    [InlineData(10, "few")]
    [InlineData(11, "many")]
    [InlineData(99, "many")]
    [InlineData(100, "other")]
    public void GetPluralForm_Arabic_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("ar");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Welsh

    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "few")]
    [InlineData(6, "many")]
    [InlineData(4, "other")]
    [InlineData(5, "other")]
    [InlineData(7, "other")]
    public void GetPluralForm_Welsh_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("cy");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Irish

    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "few")]
    [InlineData(6, "few")]
    [InlineData(7, "many")]
    [InlineData(10, "many")]
    [InlineData(11, "other")]
    public void GetPluralForm_Irish_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("ga");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Asian Languages (No Plural)

    [Theory]
    [InlineData("zh")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("vi")]
    [InlineData("th")]
    public void GetPluralForm_AsianLanguages_ReturnsOther(string languageCode)
    {
        // Arrange
        var culture = new CultureInfo(languageCode);

        // Act & Assert - All counts should return "other"
        Assert.Equal("other", PluralResolver.GetPluralForm(0, culture));
        Assert.Equal("other", PluralResolver.GetPluralForm(1, culture));
        Assert.Equal("other", PluralResolver.GetPluralForm(5, culture));
    }

    #endregion

    #region Default/Unknown Languages

    [Fact]
    public void GetPluralForm_UnknownLanguage_UsesDefaultRule()
    {
        // Arrange
        var culture = new CultureInfo("xx"); // Unknown language

        // Act & Assert - Should use simple one/other rule
        Assert.Equal("one", PluralResolver.GetPluralForm(1, culture));
        Assert.Equal("other", PluralResolver.GetPluralForm(0, culture));
        Assert.Equal("other", PluralResolver.GetPluralForm(2, culture));
    }

    [Fact]
    public void GetPluralForm_NullCulture_UsesCurrentCulture()
    {
        // Act - Should not throw, uses current culture
        var result = PluralResolver.GetPluralForm(1, null);

        // Assert - Should return something (depends on current culture)
        Assert.NotNull(result);
    }

    #endregion

    #region Latvian

    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(11, "other")]
    [InlineData(21, "one")]
    [InlineData(31, "one")]
    [InlineData(2, "other")]
    public void GetPluralForm_Latvian_ReturnsCorrectForm(int count, string expected)
    {
        // Arrange
        var culture = new CultureInfo("lv");

        // Act
        var result = PluralResolver.GetPluralForm(count, culture);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
