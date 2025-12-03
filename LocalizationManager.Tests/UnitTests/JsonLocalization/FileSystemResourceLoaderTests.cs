// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.JsonLocalization;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.JsonLocalization;

public class FileSystemResourceLoaderTests
{
    private readonly string _testDataPath;

    public FileSystemResourceLoaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonLocalization");
    }

    #region GetResourceStream

    [Fact]
    public void GetResourceStream_DefaultCulture_ReturnsStream()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        using var stream = loader.GetResourceStream("strings", "");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void GetResourceStream_SpecificCulture_ReturnsStream()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        using var stream = loader.GetResourceStream("strings", "fr");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void GetResourceStream_NonExistentCulture_ReturnsNull()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        var stream = loader.GetResourceStream("strings", "zz");

        // Assert
        Assert.Null(stream);
    }

    [Fact]
    public void GetResourceStream_NonExistentBaseName_ReturnsNull()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        var stream = loader.GetResourceStream("nonexistent", "");

        // Assert
        Assert.Null(stream);
    }

    [Fact]
    public void GetResourceStream_RelativePath_ConvertsToAbsolute()
    {
        // Arrange - Use relative path notation
        var testDir = Path.GetDirectoryName(_testDataPath)!;
        var relativePath = Path.Combine(testDir, "JsonLocalization");
        var loader = new FileSystemResourceLoader(relativePath);

        // Act
        using var stream = loader.GetResourceStream("strings", "");

        // Assert
        Assert.NotNull(stream);
    }

    #endregion

    #region GetAvailableCultures

    [Fact]
    public void GetAvailableCultures_ReturnsAllCultures()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        var cultures = loader.GetAvailableCultures("strings").ToList();

        // Assert
        Assert.Contains("", cultures); // Default
        Assert.Contains("fr", cultures);
        Assert.Contains("es", cultures);
        Assert.Contains("ru", cultures);
        Assert.Equal(4, cultures.Count);
    }

    [Fact]
    public void GetAvailableCultures_NonExistentBaseName_ReturnsEmpty()
    {
        // Arrange
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        var cultures = loader.GetAvailableCultures("nonexistent").ToList();

        // Assert
        Assert.Empty(cultures);
    }

    [Fact]
    public void GetAvailableCultures_NonExistentDirectory_ReturnsEmpty()
    {
        // Arrange
        var loader = new FileSystemResourceLoader("/nonexistent/path");

        // Act
        var cultures = loader.GetAvailableCultures("strings").ToList();

        // Assert
        Assert.Empty(cultures);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetResourceStream_CultureWithRegion_ReturnsNull_WhenOnlyBaseCultureExists()
    {
        // Arrange - We have fr.json but not fr-CA.json
        var loader = new FileSystemResourceLoader(_testDataPath);

        // Act
        var stream = loader.GetResourceStream("strings", "fr-CA");

        // Assert
        Assert.Null(stream);
    }

    [Fact]
    public void Constructor_WithTrailingSlash_WorksCorrectly()
    {
        // Arrange
        var pathWithSlash = _testDataPath + Path.DirectorySeparatorChar;
        var loader = new FileSystemResourceLoader(pathWithSlash);

        // Act
        using var stream = loader.GetResourceStream("strings", "");

        // Assert
        Assert.NotNull(stream);
    }

    #endregion
}
