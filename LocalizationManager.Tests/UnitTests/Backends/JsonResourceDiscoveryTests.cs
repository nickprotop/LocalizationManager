// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class JsonResourceDiscoveryTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _i18nextTestDataPath;
    private readonly string _tempDirectory;

    public JsonResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "JsonResources");
        _i18nextTestDataPath = Path.Combine(_testDataPath, "I18next");
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"JsonDiscoveryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Standard Format Discovery Tests

    [Fact]
    public void DiscoverLanguages_StandardFormat_FindsAllLanguages()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
        Assert.Equal(3, languages.Count); // TestResource.json, TestResource.el.json, TestResource.fr.json
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_IdentifiesDefaultLanguage()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.Equal("", defaultLang.Code);
        Assert.Contains("Default", defaultLang.Name);
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_IdentifiesGreekLanguage()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var greekLang = languages.FirstOrDefault(l => l.Code == "el");
        Assert.NotNull(greekLang);
        Assert.Equal("el", greekLang.Code);
        Assert.False(greekLang.IsDefault);
        Assert.Contains("TestResource.el.json", greekLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_SetsCorrectBaseName()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        foreach (var lang in languages)
        {
            Assert.Equal("TestResource", lang.BaseName);
        }
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_SetsCorrectFilePaths()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        foreach (var lang in languages)
        {
            Assert.NotNull(lang.FilePath);
            Assert.True(File.Exists(lang.FilePath), $"File path {lang.FilePath} does not exist");
            Assert.EndsWith(".json", lang.FilePath);
        }
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_SkipsLrmConfigFile()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert - should not include lrm.json
        Assert.DoesNotContain(languages, l => l.FilePath.EndsWith("lrm.json", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region i18next Format Discovery Tests

    [Fact]
    public void DiscoverLanguages_I18nextFormat_FindsAllLanguages()
    {
        // Arrange
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var discovery = new JsonResourceDiscovery(config);

        // Act
        var languages = discovery.DiscoverLanguages(_i18nextTestDataPath);

        // Assert
        Assert.Equal(2, languages.Count); // en.json, fr.json
    }

    [Fact]
    public void DiscoverLanguages_I18nextFormat_AutoDetectsDefaultByEnglishCode()
    {
        // Arrange - no config default, no _meta.isDefault, same key count
        // Should fall back to "English code" detection
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var discovery = new JsonResourceDiscovery(config);

        // Act
        var languages = discovery.DiscoverLanguages(_i18nextTestDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("en.json", defaultLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_I18nextFormat_NonDefaultHasCultureCode()
    {
        // Arrange
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var discovery = new JsonResourceDiscovery(config);

        // Act
        var languages = discovery.DiscoverLanguages(_i18nextTestDataPath);

        // Assert
        var frenchLang = languages.FirstOrDefault(l => !l.IsDefault && l.FilePath.EndsWith("fr.json"));
        Assert.NotNull(frenchLang);
        Assert.Equal("fr", frenchLang.Code);
    }

    [Fact]
    public void DiscoverLanguages_I18nextFormat_DefaultHasEmptyCode()
    {
        // Arrange
        var config = new JsonFormatConfiguration { I18nextCompatible = true };
        var discovery = new JsonResourceDiscovery(config);

        // Act
        var languages = discovery.DiscoverLanguages(_i18nextTestDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.Equal("", defaultLang.Code);
    }

    #endregion

    #region Auto-detection Priority Tests

    [Fact]
    public void DiscoverLanguages_I18next_ConfigDefaultTakesPriority()
    {
        // Arrange - create temp files where French would otherwise win by key count
        var tempI18nextDir = Path.Combine(_tempDirectory, "i18next_config");
        Directory.CreateDirectory(tempI18nextDir);

        // en.json has fewer keys
        File.WriteAllText(Path.Combine(tempI18nextDir, "en.json"), @"{""one"": ""One""}");
        // fr.json has more keys
        File.WriteAllText(Path.Combine(tempI18nextDir, "fr.json"), @"{""one"": ""Un"", ""two"": ""Deux"", ""three"": ""Trois""}");
        // lrm.json says French is default
        File.WriteAllText(Path.Combine(tempI18nextDir, "lrm.json"), @"{""defaultLanguageCode"": ""fr"", ""json"": {""i18nextCompatible"": true}}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempI18nextDir);

        // Assert - French should be default because config says so
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("fr.json", defaultLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_I18next_MetaIsDefaultTakesPriority()
    {
        // Arrange - create temp files where _meta.isDefault marks French as default
        var tempI18nextDir = Path.Combine(_tempDirectory, "i18next_meta");
        Directory.CreateDirectory(tempI18nextDir);

        // en.json - no isDefault
        File.WriteAllText(Path.Combine(tempI18nextDir, "en.json"), @"{""key"": ""Value""}");
        // fr.json - has isDefault: true
        File.WriteAllText(Path.Combine(tempI18nextDir, "fr.json"), @"{""_meta"": {""isDefault"": true}, ""key"": ""Valeur""}");
        // lrm.json - no defaultLanguageCode specified
        File.WriteAllText(Path.Combine(tempI18nextDir, "lrm.json"), @"{""json"": {""i18nextCompatible"": true}}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempI18nextDir);

        // Assert - French should be default because _meta.isDefault says so
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("fr.json", defaultLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_I18next_MostKeysTakesPriority()
    {
        // Arrange - create temp files where German has most keys (no English to fall back to)
        var tempI18nextDir = Path.Combine(_tempDirectory, "i18next_keys");
        Directory.CreateDirectory(tempI18nextDir);

        // de.json has most keys
        File.WriteAllText(Path.Combine(tempI18nextDir, "de.json"), @"{""a"": ""A"", ""b"": ""B"", ""c"": ""C"", ""d"": ""D""}");
        // fr.json has fewer keys
        File.WriteAllText(Path.Combine(tempI18nextDir, "fr.json"), @"{""a"": ""A"", ""b"": ""B""}");
        // lrm.json - no defaultLanguageCode
        File.WriteAllText(Path.Combine(tempI18nextDir, "lrm.json"), @"{""json"": {""i18nextCompatible"": true}}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempI18nextDir);

        // Assert - German should be default because it has most keys
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("de.json", defaultLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_I18next_EnglishFallbackWhenTied()
    {
        // Arrange - same key count, English should win
        var tempI18nextDir = Path.Combine(_tempDirectory, "i18next_english");
        Directory.CreateDirectory(tempI18nextDir);

        // Same number of keys
        File.WriteAllText(Path.Combine(tempI18nextDir, "en.json"), @"{""a"": ""A"", ""b"": ""B""}");
        File.WriteAllText(Path.Combine(tempI18nextDir, "fr.json"), @"{""a"": ""A"", ""b"": ""B""}");
        // lrm.json - no defaultLanguageCode
        File.WriteAllText(Path.Combine(tempI18nextDir, "lrm.json"), @"{""json"": {""i18nextCompatible"": true}}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempI18nextDir);

        // Assert - English should win by fallback
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("en.json", defaultLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_I18next_AlphabeticalFallback()
    {
        // Arrange - no English, same key count -> alphabetical wins
        var tempI18nextDir = Path.Combine(_tempDirectory, "i18next_alpha");
        Directory.CreateDirectory(tempI18nextDir);

        // Same number of keys, no English
        File.WriteAllText(Path.Combine(tempI18nextDir, "fr.json"), @"{""a"": ""A""}");
        File.WriteAllText(Path.Combine(tempI18nextDir, "de.json"), @"{""a"": ""A""}");
        // lrm.json - no defaultLanguageCode
        File.WriteAllText(Path.Combine(tempI18nextDir, "lrm.json"), @"{""json"": {""i18nextCompatible"": true}}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempI18nextDir);

        // Assert - de.json should win alphabetically
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.EndsWith("de.json", defaultLang.FilePath);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DiscoverLanguages_NonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var languages = discovery.DiscoverLanguages(nonExistentPath);

        // Assert
        Assert.Empty(languages);
    }

    [Fact]
    public void DiscoverLanguages_DirectoryWithNoJsonFiles_ReturnsEmptyList()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();
        var emptyDir = Path.Combine(_tempDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var languages = discovery.DiscoverLanguages(emptyDir);

        // Assert
        Assert.Empty(languages);
    }

    [Fact]
    public void DiscoverLanguages_StandardFormat_HandlesComplexCultureCodes()
    {
        // Arrange
        var tempDir = Path.Combine(_tempDirectory, "complex_cultures");
        Directory.CreateDirectory(tempDir);

        File.WriteAllText(Path.Combine(tempDir, "strings.json"), @"{""key"": ""Value""}");
        File.WriteAllText(Path.Combine(tempDir, "strings.zh-Hans.json"), @"{""key"": ""值""}");
        File.WriteAllText(Path.Combine(tempDir, "strings.pt-BR.json"), @"{""key"": ""Valor""}");

        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(tempDir);

        // Assert
        Assert.Equal(3, languages.Count);
        Assert.Contains(languages, l => l.Code == "zh-Hans");
        Assert.Contains(languages, l => l.Code == "pt-BR");
    }

    [Fact]
    public void DiscoverLanguages_SortsDefaultFirst()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert - default should be first
        Assert.True(languages[0].IsDefault);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task DiscoverLanguagesAsync_ReturnsLanguages()
    {
        // Arrange
        var discovery = new JsonResourceDiscovery();

        // Act
        var languages = await discovery.DiscoverLanguagesAsync(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
    }

    #endregion
}
