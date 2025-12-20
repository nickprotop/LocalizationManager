// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.iOS;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class IosResourceDiscoveryTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _tempDirectory;

    public IosResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "IosResources");
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IosDiscoveryTests_{Guid.NewGuid()}");
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
        var discovery = new IosResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
        Assert.Equal(3, languages.Count); // en.lproj, es.lproj, zh-Hans.lproj
    }

    [Fact]
    public void DiscoverLanguages_IdentifiesDefaultLanguage()
    {
        // Arrange
        var discovery = new IosResourceDiscovery();

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
        var discovery = new IosResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var spanishLang = languages.FirstOrDefault(l => l.Code == "es");
        Assert.NotNull(spanishLang);
        Assert.Equal("es", spanishLang.Code);
        Assert.False(spanishLang.IsDefault);
        Assert.Contains("es.lproj", spanishLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_IdentifiesChineseLanguage()
    {
        // Arrange
        var discovery = new IosResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var chineseLang = languages.FirstOrDefault(l => l.Code == "zh-Hans");
        Assert.NotNull(chineseLang);
        Assert.Equal("zh-Hans", chineseLang.Code);
        Assert.False(chineseLang.IsDefault);
        Assert.Contains("zh-Hans.lproj", chineseLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_DefaultLanguageFirst()
    {
        // Arrange
        var discovery = new IosResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.True(languages.First().IsDefault);
    }

    [Fact]
    public void DiscoverLanguages_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var discovery = new IosResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_tempDirectory);

        // Assert
        Assert.Empty(languages);
    }

    #endregion

    #region Culture Mapper Tests

    [Theory]
    [InlineData("en.lproj", "en")]
    [InlineData("es.lproj", "es")]
    [InlineData("fr.lproj", "fr")]
    [InlineData("zh-Hans.lproj", "zh-Hans")]
    [InlineData("zh-Hant.lproj", "zh-Hant")]
    [InlineData("pt-BR.lproj", "pt-BR")]
    [InlineData("Base.lproj", "")]
    public void LprojToCode_ConvertsCorrectly(string folder, string expectedCode)
    {
        // Act
        var code = IosCultureMapper.LprojToCode(folder);

        // Assert
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData("en", "en.lproj")]
    [InlineData("es", "es.lproj")]
    [InlineData("fr", "fr.lproj")]
    [InlineData("zh-Hans", "zh-Hans.lproj")]
    [InlineData("pt-BR", "pt-BR.lproj")]
    public void CodeToLproj_ConvertsCorrectly(string code, string expectedFolder)
    {
        // Act
        var folder = IosCultureMapper.CodeToLproj(code);

        // Assert
        Assert.Equal(expectedFolder, folder);
    }

    [Fact]
    public void CodeToLproj_EmptyCode_UseBase_ReturnsBaseLproj()
    {
        // Act
        var folder = IosCultureMapper.CodeToLproj("", useBase: true);

        // Assert
        Assert.Equal("Base.lproj", folder);
    }

    [Theory]
    [InlineData("en.lproj", true)]
    [InlineData("es.lproj", true)]
    [InlineData("Base.lproj", true)]
    [InlineData("zh-Hans.lproj", true)]
    [InlineData("en", false)]
    [InlineData("", false)]
    [InlineData(".lproj", false)]
    public void IsValidLprojFolder_ReturnsCorrectly(string folder, bool expected)
    {
        // Act
        var result = IosCultureMapper.IsValidLprojFolder(folder);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Base.lproj", true)]
    [InlineData("base.lproj", true)]
    [InlineData("BASE.LPROJ", true)]
    [InlineData("en.lproj", false)]
    public void IsBaseLproj_ReturnsCorrectly(string folder, bool expected)
    {
        // Act
        var result = IosCultureMapper.IsBaseLproj(folder);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
