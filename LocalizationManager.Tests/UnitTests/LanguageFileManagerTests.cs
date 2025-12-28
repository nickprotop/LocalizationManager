// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class LanguageFileManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LanguageFileManager _manager;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();

    public LanguageFileManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _manager = new LanguageFileManager();

        // Create a default test resource file
        CreateDefaultTestResource();
    }

    private void CreateDefaultTestResource()
    {
        var defaultLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };

        var resourceFile = new ResourceFile
        {
            Language = defaultLang,
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Value1", Comment = "Comment1" },
                new() { Key = "Key2", Value = "Value2", Comment = "Comment2" }
            }
        };

        _writer.Write(resourceFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void CreateLanguageFile_ValidCulture_CreatesFile()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "fr";

        // Act
        var result = _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fr", result.Language.Code);
        Assert.Equal("French", result.Language.Name);
        Assert.False(result.Language.IsDefault);
        Assert.True(File.Exists(result.Language.FilePath));
    }

    [Fact]
    public void CreateLanguageFile_WithSourceFile_CopiesEntries()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "el";
        var defaultLang = new LanguageInfo
        {
            BaseName = baseName,
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };
        var sourceFile = _reader.Read(defaultLang);

        // Act
        var result = _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory, sourceFile, copyEntries: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Key1", result.Entries[0].Key);
        Assert.Equal("Value1", result.Entries[0].Value);
    }

    [Fact]
    public void CreateLanguageFile_EmptyFile_CreatesEmptyEntries()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "de";
        var defaultLang = new LanguageInfo
        {
            BaseName = baseName,
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };
        var sourceFile = _reader.Read(defaultLang);

        // Act
        var result = _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory, sourceFile, copyEntries: false);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void CreateLanguageFile_InvalidCulture_ThrowsArgumentException()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "invalid!@#";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory));
    }

    [Fact]
    public void CreateLanguageFile_AlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "fr";
        _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory));
    }

    [Fact]
    public void DeleteLanguageFile_ExistingFile_DeletesSuccessfully()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "fr";
        var createdFile = _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory);
        var filePath = createdFile.Language.FilePath;

        // Act
        _manager.DeleteLanguageFile(createdFile.Language);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void DeleteLanguageFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "fr",
            Name = "French",
            IsDefault = false,
            FilePath = Path.Combine(_testDirectory, "NonExistent.fr.resx")
        };

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            _manager.DeleteLanguageFile(languageInfo));
    }

    [Fact]
    public void DeleteLanguageFile_DefaultLanguage_ThrowsInvalidOperationException()
    {
        // Arrange
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _manager.DeleteLanguageFile(languageInfo));
        Assert.Contains("default language", ex.Message.ToLower());
    }

    [Fact]
    public void IsValidCultureCode_ValidCode_ReturnsTrue()
    {
        // Arrange & Act
        var result = _manager.IsValidCultureCode("fr", out var culture);

        // Assert
        Assert.True(result);
        Assert.NotNull(culture);
        Assert.Equal("French", culture.DisplayName);
    }

    [Fact]
    public void IsValidCultureCode_ValidRegionalCode_ReturnsTrue()
    {
        // Arrange & Act
        var result = _manager.IsValidCultureCode("fr-FR", out var culture);

        // Assert
        Assert.True(result);
        Assert.NotNull(culture);
        Assert.Contains("French", culture.DisplayName);
    }

    [Fact]
    public void IsValidCultureCode_InvalidCode_ReturnsFalse()
    {
        // Arrange & Act
        var result = _manager.IsValidCultureCode("invalid!@#", out var culture);

        // Assert
        Assert.False(result);
        Assert.Null(culture);
    }

    [Fact]
    public void LanguageFileExists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "fr";
        _manager.CreateLanguageFile(baseName, cultureCode, _testDirectory);

        // Act
        var result = _manager.LanguageFileExists(baseName, cultureCode, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LanguageFileExists_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var baseName = "TestResource";
        var cultureCode = "de";

        // Act
        var result = _manager.LanguageFileExists(baseName, cultureCode, _testDirectory);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCultureDisplayName_ValidCode_ReturnsDisplayName()
    {
        // Arrange & Act
        var result = _manager.GetCultureDisplayName("fr");

        // Assert
        Assert.Equal("French", result);
    }

    [Fact]
    public void GetCultureDisplayName_InvalidCode_ReturnsCode()
    {
        // Arrange & Act
        var result = _manager.GetCultureDisplayName("invalid!@#");

        // Assert
        Assert.Equal("invalid!@#", result);
    }
}
