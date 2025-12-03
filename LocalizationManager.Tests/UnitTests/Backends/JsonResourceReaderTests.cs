// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Exceptions;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class JsonResourceReaderTests
{
    private readonly string _testDataPath;
    private readonly string _i18nextTestDataPath;

    public JsonResourceReaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonResources");
        _i18nextTestDataPath = Path.Combine(_testDataPath, "I18next");
    }

    #region Standard Format Tests

    [Fact]
    public void Read_ValidJsonFile_ReturnsResourceFile()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(languageInfo, result.Language);
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public void Read_ValidJsonFile_ParsesNestedKeysCorrectly()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var errorNotFound = result.Entries.FirstOrDefault(e => e.Key == "Errors.NotFound");
        Assert.NotNull(errorNotFound);
        Assert.Equal("Item not found", errorNotFound.Value);

        var buttonsOk = result.Entries.FirstOrDefault(e => e.Key == "Buttons.OK");
        Assert.NotNull(buttonsOk);
        Assert.Equal("OK", buttonsOk.Value);
    }

    [Fact]
    public void Read_ValidJsonFile_ParsesCommentsCorrectly()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var saveEntry = result.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.NotNull(saveEntry);
        Assert.Equal("Save All", saveEntry.Value);
        Assert.Equal("Save button label", saveEntry.Comment);
    }

    [Fact]
    public void Read_ValidJsonFile_ParsesEmptyValues()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var emptyEntry = result.Entries.FirstOrDefault(e => e.Key == "EmptyValue");
        Assert.NotNull(emptyEntry);
        Assert.Equal(string.Empty, emptyEntry.Value);
    }

    [Fact]
    public void Read_ValidJsonFile_ParsesPlaceholders()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var requiredEntry = result.Entries.FirstOrDefault(e => e.Key == "Validation.Required");
        Assert.NotNull(requiredEntry);
        Assert.Contains("{0}", requiredEntry.Value);
    }

    [Fact]
    public void Read_NonExistentFile_ThrowsResourceNotFoundException()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "NonExistent",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "NonExistent.json")
        };

        // Act & Assert
        Assert.Throws<ResourceNotFoundException>(() => reader.Read(languageInfo));
    }

    [Fact]
    public void Read_InvalidJsonSyntax_ThrowsResourceParseException()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var tempFile = Path.Combine(Path.GetTempPath(), "InvalidJson.json");
        File.WriteAllText(tempFile, "{ invalid json }");

        var languageInfo = new LanguageInfo
        {
            BaseName = "InvalidJson",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = tempFile
        };

        try
        {
            // Act & Assert
            Assert.Throws<ResourceParseException>(() => reader.Read(languageInfo));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Read_GreekLanguageFile_ReturnsCorrectValues()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "el",
            Name = "Greek",
            IsDefault = false,
            FilePath = Path.Combine(_testDataPath, "TestResource.el.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var saveEntry = result.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.NotNull(saveEntry);
        Assert.Equal("Αποθήκευση Όλων", saveEntry.Value);

        var cancelEntry = result.Entries.FirstOrDefault(e => e.Key == "Cancel");
        Assert.NotNull(cancelEntry);
        Assert.Equal("Ακύρωση", cancelEntry.Value);
    }

    [Fact]
    public void Read_SkipsMetaProperties()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert - no entries with keys starting with _
        Assert.DoesNotContain(result.Entries, e => e.Key.StartsWith("_"));
    }

    #endregion

    #region i18next Format Tests

    [Fact]
    public void Read_I18nextFormat_ParsesNestedKeys()
    {
        // Arrange
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var reader = new JsonResourceReader(config);
        var languageInfo = new LanguageInfo
        {
            BaseName = "strings",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_i18nextTestDataPath, "en.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var saveEntry = result.Entries.FirstOrDefault(e => e.Key == "common.save");
        Assert.NotNull(saveEntry);
        Assert.Equal("Save", saveEntry.Value);
    }

    [Fact]
    public void Read_I18nextFormat_ParsesI18nextPlaceholders()
    {
        // Arrange
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var reader = new JsonResourceReader(config);
        var languageInfo = new LanguageInfo
        {
            BaseName = "strings",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_i18nextTestDataPath, "en.json")
        };

        // Act
        var result = reader.Read(languageInfo);

        // Assert
        var welcomeEntry = result.Entries.FirstOrDefault(e => e.Key == "welcome");
        Assert.NotNull(welcomeEntry);
        Assert.Contains("{{name}}", welcomeEntry.Value);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task ReadAsync_ValidJsonFile_ReturnsResourceFile()
    {
        // Arrange
        var reader = new JsonResourceReader();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.json")
        };

        // Act
        var result = await reader.ReadAsync(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
    }

    #endregion
}
