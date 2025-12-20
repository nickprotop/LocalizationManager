// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Android;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class AndroidResourceDiscoveryTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _tempDirectory;

    public AndroidResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "AndroidResources");
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AndroidDiscoveryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Discovery Tests

    [Fact]
    public void DiscoverLanguages_FindsAllLanguages()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
        Assert.Equal(3, languages.Count); // values, values-es, values-zh-rCN
    }

    [Fact]
    public void DiscoverLanguages_IdentifiesDefaultLanguage()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.Equal("", defaultLang.Code);
        Assert.Contains("Default", defaultLang.Name);
    }

    [Fact]
    public void DiscoverLanguages_IdentifiesSpanishLanguage()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var spanishLang = languages.FirstOrDefault(l => l.Code == "es");
        Assert.NotNull(spanishLang);
        Assert.Equal("es", spanishLang.Code);
        Assert.False(spanishLang.IsDefault);
        Assert.Contains("values-es", spanishLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_IdentifiesChineseLanguage()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var chineseLang = languages.FirstOrDefault(l => l.Code == "zh-CN");
        Assert.NotNull(chineseLang);
        Assert.Equal("zh-CN", chineseLang.Code);
        Assert.False(chineseLang.IsDefault);
        Assert.Contains("values-zh-rCN", chineseLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_DefaultLanguageFirst()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.True(languages.First().IsDefault);
    }

    [Fact]
    public void DiscoverLanguages_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var discovery = new AndroidResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_tempDirectory);

        // Assert
        Assert.Empty(languages);
    }

    #endregion

    #region Culture Mapper Tests

    [Theory]
    [InlineData("values", "")]
    [InlineData("values-es", "es")]
    [InlineData("values-fr", "fr")]
    [InlineData("values-zh-rCN", "zh-CN")]
    [InlineData("values-zh-rTW", "zh-TW")]
    [InlineData("values-pt-rBR", "pt-BR")]
    [InlineData("values-b+sr+Latn", "sr-Latn")]
    public void FolderToCode_ConvertsCorrectly(string folder, string expectedCode)
    {
        // Act
        var code = AndroidCultureMapper.FolderToCode(folder);

        // Assert
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData("", "values")]
    [InlineData("es", "values-es")]
    [InlineData("fr", "values-fr")]
    [InlineData("zh-CN", "values-zh-rCN")]
    [InlineData("zh-TW", "values-zh-rTW")]
    [InlineData("pt-BR", "values-pt-rBR")]
    [InlineData("sr-Latn", "values-b+sr+Latn")]
    public void CodeToFolder_ConvertsCorrectly(string code, string expectedFolder)
    {
        // Act
        var folder = AndroidCultureMapper.CodeToFolder(code);

        // Assert
        Assert.Equal(expectedFolder, folder);
    }

    [Theory]
    [InlineData("values", true)]
    [InlineData("values-es", true)]
    [InlineData("values-zh-rCN", true)]
    [InlineData("drawable", false)]
    [InlineData("layout", false)]
    [InlineData("", false)]
    public void IsValidResourceFolder_ReturnsCorrectly(string folder, bool expected)
    {
        // Act
        var result = AndroidCultureMapper.IsValidResourceFolder(folder);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
