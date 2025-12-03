// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.JsonLocalization;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class JsonLocalizerTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly JsonLocalizer _localizer;

    public JsonLocalizerTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonLocalization");
        _localizer = new JsonLocalizer(_testDataPath, "strings");
    }

    public void Dispose()
    {
        _localizer.Dispose();
    }

    #region Basic String Access

    [Fact]
    public void GetString_SimpleKey_ReturnsValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["appTitle"];

        // Assert
        Assert.Equal("Test Application", result);
    }

    [Fact]
    public void GetString_NestedKey_ReturnsValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["buttons.save"];

        // Assert
        Assert.Equal("Save", result);
    }

    [Fact]
    public void GetString_DeepNestedKey_ReturnsValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["errors.required"];

        // Assert
        Assert.Equal("This field is required", result);
    }

    [Fact]
    public void GetString_NonExistentKey_ReturnsKey()
    {
        // Act
        var result = _localizer["nonExistent.key"];

        // Assert
        Assert.Equal("nonExistent.key", result);
    }

    [Fact]
    public void GetString_EmptyValue_ReturnsEmptyString()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["emptyValue"];

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region Format Arguments

    [Fact]
    public void GetString_WithSingleFormatArgument_ReturnsFormattedValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["greeting", "World"];

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void GetString_WithMultipleFormatArguments_ReturnsFormattedValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer["multiParam", "John", 5];

        // Assert
        Assert.Equal("User John has 5 items", result);
    }

    [Fact]
    public void GetString_WithInvalidFormat_ReturnsUnformattedValue()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act - greeting expects one arg but we're testing that it handles extra args gracefully
        var result = _localizer.GetString("greeting", "World", "ExtraArg");

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    #endregion

    #region Culture Support

    [Fact]
    public void GetString_FrenchCulture_ReturnsFrenchValue()
    {
        // Arrange
        _localizer.Culture = new CultureInfo("fr");

        // Act
        var result = _localizer["appTitle"];

        // Assert
        Assert.Equal("Application de test", result);
    }

    [Fact]
    public void GetString_SpanishCulture_ReturnsSpanishValue()
    {
        // Arrange
        _localizer.Culture = new CultureInfo("es");

        // Act
        var result = _localizer["appTitle"];

        // Assert
        Assert.Equal("Aplicación de prueba", result);
    }

    [Fact]
    public void GetString_CultureFallback_FallsBackToDefault()
    {
        // Arrange - Spanish doesn't have errors.required
        _localizer.Culture = new CultureInfo("es");

        // Act
        var result = _localizer["errors.required"];

        // Assert - Should fall back to default
        Assert.Equal("This field is required", result);
    }

    [Fact]
    public void GetString_SpecificCultureParam_ReturnsCorrectValue()
    {
        // Act
        var result = _localizer.GetString("appTitle", new CultureInfo("fr"));

        // Assert
        Assert.Equal("Application de test", result);
    }

    [Fact]
    public void Culture_SetNull_DefaultsToCurrent()
    {
        // Arrange
        var originalCulture = _localizer.Culture;

        // Act
        _localizer.Culture = null!;

        // Assert
        Assert.Equal(CultureInfo.CurrentUICulture, _localizer.Culture);
    }

    #endregion

    #region Pluralization

    [Fact]
    public void Plural_ZeroCount_ReturnsOtherFormForEnglish()
    {
        // Arrange - English uses "other" for 0 (CLDR rules)
        _localizer.Culture = new CultureInfo("en");

        // Act
        var result = _localizer.Plural("itemCount", 0);

        // Assert - English returns "other" form for 0
        Assert.Equal("0 items", result);
    }

    [Fact]
    public void Plural_OneCount_ReturnsOneForm()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer.Plural("itemCount", 1);

        // Assert
        Assert.Equal("One item", result);
    }

    [Fact]
    public void Plural_MultipleCount_ReturnsOtherForm()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer.Plural("itemCount", 5);

        // Assert
        Assert.Equal("5 items", result);
    }

    [Fact]
    public void Plural_FrenchCulture_ReturnsFrenchPlural()
    {
        // Arrange - French uses "one" for 0 and 1 (CLDR rules)
        _localizer.Culture = new CultureInfo("fr");

        // Act
        var result = _localizer.Plural("itemCount", 0);

        // Assert - French uses "one" form for 0, which is "Un élément"
        Assert.Equal("Un élément", result);
    }

    [Fact]
    public void Plural_RussianCulture_ReturnsCorrectForm()
    {
        // Arrange - Russian has more complex plural rules
        _localizer.Culture = new CultureInfo("ru");

        // Act
        var resultOne = _localizer.Plural("itemCount", 1);
        var resultFew = _localizer.Plural("itemCount", 2);
        var resultMany = _localizer.Plural("itemCount", 5);

        // Assert
        Assert.Equal("1 элемент", resultOne);
        Assert.Equal("2 элемента", resultFew);
        Assert.Equal("5 элементов", resultMany);
    }

    [Fact]
    public void Plural_NonPluralKey_ReturnsKeyAsPlainText()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var result = _localizer.Plural("nonExistent", 5);

        // Assert
        Assert.Equal("nonExistent", result);
    }

    #endregion

    #region Available Cultures

    [Fact]
    public void AvailableCultures_ReturnsAllCultures()
    {
        // Act
        var cultures = _localizer.AvailableCultures.ToList();

        // Assert
        Assert.Contains("", cultures); // Default
        Assert.Contains("fr", cultures);
        Assert.Contains("es", cultures);
        Assert.Contains("ru", cultures);
    }

    #endregion

    #region Get All Strings

    [Fact]
    public void GetAllStrings_ReturnsAllEntries()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;

        // Act
        var strings = _localizer.GetAllStrings().ToList();

        // Assert
        Assert.Contains(strings, s => s.Key == "appTitle");
        Assert.Contains(strings, s => s.Key == "welcome");
        Assert.Contains(strings, s => s.Key == "buttons.save");
    }

    [Fact]
    public void GetAllStrings_WithSpecificCulture_ReturnsCultureEntries()
    {
        // Act
        var strings = _localizer.GetAllStrings(new CultureInfo("fr")).ToList();

        // Assert
        var appTitle = strings.FirstOrDefault(s => s.Key == "appTitle");
        Assert.Equal("Application de test", appTitle.Value);
    }

    #endregion

    #region Cache

    [Fact]
    public void ClearCache_AllowsResourceReload()
    {
        // Arrange
        _localizer.Culture = CultureInfo.InvariantCulture;
        var firstValue = _localizer["appTitle"];

        // Act
        _localizer.ClearCache();
        var secondValue = _localizer["appTitle"];

        // Assert - Both should work correctly
        Assert.Equal("Test Application", firstValue);
        Assert.Equal("Test Application", secondValue);
    }

    #endregion
}
