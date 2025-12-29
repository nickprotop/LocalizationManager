// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Xliff;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Xliff;

public class XliffResourceReaderTests
{
    private readonly string _testDataPath;

    public XliffResourceReaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Xliff");
    }

    #region XLIFF 1.2 Tests

    [Fact]
    public void Read_Xliff12File_ReturnsResourceFile()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v12_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(languageInfo, result.Language);
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public void Read_Xliff12File_ParsesEntriesCorrectly()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v12_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var greetingEntry = result.Entries.FirstOrDefault(e => e.Key == "greeting");
        Assert.NotNull(greetingEntry);
        Assert.Equal("Bonjour", greetingEntry.Value);

        var welcomeEntry = result.Entries.FirstOrDefault(e => e.Key == "welcome");
        Assert.NotNull(welcomeEntry);
        Assert.Equal("Bienvenue dans l'application", welcomeEntry.Value);
    }

    [Fact]
    public void Read_Xliff12File_ParsesNotesAsComments()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v12_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var greetingEntry = result.Entries.FirstOrDefault(e => e.Key == "greeting");
        Assert.NotNull(greetingEntry);
        Assert.NotNull(greetingEntry.Comment);
        Assert.Contains("greeting", greetingEntry.Comment);
    }

    #endregion

    #region XLIFF 2.0 Tests

    [Fact]
    public void Read_Xliff20File_ReturnsResourceFile()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v20_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public void Read_Xliff20File_ParsesEntriesCorrectly()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v20_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var greetingEntry = result.Entries.FirstOrDefault(e => e.Key == "greeting");
        Assert.NotNull(greetingEntry);
        Assert.Equal("Bonjour", greetingEntry.Value);
    }

    [Fact]
    public void Read_Xliff20File_ParsesNotesCorrectly()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v20_simple.xliff")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var greetingEntry = result.Entries.FirstOrDefault(e => e.Key == "greeting");
        Assert.NotNull(greetingEntry);
        Assert.NotNull(greetingEntry.Comment);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task ReadAsync_Xliff12File_ReturnsResourceFile()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v12_simple.xliff")
        };

        // Act
        var result = await reader.ReadAsync(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public async Task ReadAsync_Xliff20File_ReturnsResourceFile()
    {
        // Arrange
        var reader = new XliffResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "resources",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "v20_simple.xliff")
        };

        // Act
        var result = await reader.ReadAsync(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
    }

    #endregion
}
