// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.JsonLocalization;
using Microsoft.Extensions.Localization;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class JsonStringLocalizerTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly JsonLocalizer _jsonLocalizer;
    private readonly JsonStringLocalizer _stringLocalizer;

    public JsonStringLocalizerTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonLocalization");
        _jsonLocalizer = new JsonLocalizer(_testDataPath, "strings");
        _stringLocalizer = new JsonStringLocalizer(_jsonLocalizer);
    }

    public void Dispose()
    {
        _jsonLocalizer.Dispose();
    }

    #region Indexer Access

    [Fact]
    public void Indexer_ExistingKey_ReturnsLocalizedString()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        // Act
        LocalizedString result = _stringLocalizer["appTitle"];

        // Assert
        Assert.Equal("appTitle", result.Name);
        Assert.Equal("Test Application", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public void Indexer_NonExistingKey_ReturnsKeyAsValue()
    {
        // Act
        LocalizedString result = _stringLocalizer["nonExistent"];

        // Assert
        Assert.Equal("nonExistent", result.Name);
        Assert.Equal("nonExistent", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    [Fact]
    public void Indexer_WithArguments_ReturnsFormattedString()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        // Act
        LocalizedString result = _stringLocalizer["greeting", "World"];

        // Assert
        Assert.Equal("greeting", result.Name);
        Assert.Equal("Hello, World!", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public void Indexer_WithMultipleArguments_ReturnsFormattedString()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        // Act
        LocalizedString result = _stringLocalizer["multiParam", "John", 5];

        // Assert
        Assert.Equal("User John has 5 items", result.Value);
    }

    #endregion

    #region Culture Support

    [Fact]
    public void Indexer_UsesCurrentUICulture()
    {
        // Arrange
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr");

            // Act
            LocalizedString result = _stringLocalizer["appTitle"];

            // Assert
            Assert.Equal("Application de test", result.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    [Fact]
    public void Indexer_CultureChangeAffectsSubsequentCalls()
    {
        // Arrange
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            // First call in English
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var englishResult = _stringLocalizer["appTitle"];

            // Change to French
            CultureInfo.CurrentUICulture = new CultureInfo("fr");
            var frenchResult = _stringLocalizer["appTitle"];

            // Assert
            Assert.Equal("Test Application", englishResult.Value);
            Assert.Equal("Application de test", frenchResult.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    #endregion

    #region GetAllStrings

    [Fact]
    public void GetAllStrings_WithoutParentCultures_ReturnsCurrentCultureStrings()
    {
        // Arrange
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            _jsonLocalizer.Culture = CultureInfo.InvariantCulture;

            // Act
            var strings = _stringLocalizer.GetAllStrings(includeParentCultures: false).ToList();

            // Assert
            Assert.NotEmpty(strings);
            Assert.Contains(strings, s => s.Name == "appTitle");
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    [Fact]
    public void GetAllStrings_WithParentCultures_IncludesParentStrings()
    {
        // Arrange
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            _jsonLocalizer.Culture = new CultureInfo("es");

            // Act
            var strings = _stringLocalizer.GetAllStrings(includeParentCultures: true).ToList();

            // Assert
            Assert.NotEmpty(strings);
            // Spanish has appTitle
            Assert.Contains(strings, s => s.Name == "appTitle");
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    #endregion

    #region Null/Invalid Input

    [Fact]
    public void Constructor_NullLocalizer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JsonStringLocalizer(null!));
    }

    #endregion
}
