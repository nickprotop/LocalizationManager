// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Android;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class AndroidResourceReaderTests
{
    private readonly string _testDataPath;
    private readonly AndroidResourceReader _reader;
    private readonly AndroidResourceDiscovery _discovery;

    public AndroidResourceReaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "AndroidResources");
        _reader = new AndroidResourceReader();
        _discovery = new AndroidResourceDiscovery();
    }

    [Fact]
    public void Read_DefaultLanguage_ReadsAllEntries()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        Assert.NotNull(file);
        Assert.NotEmpty(file.Entries);
    }

    [Fact]
    public void Read_ParsesSimpleStrings()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        var appName = file.Entries.FirstOrDefault(e => e.Key == "app_name");
        Assert.NotNull(appName);
        Assert.Equal("Test App", appName.Value);
    }

    [Fact]
    public void Read_ParsesComments()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        var appName = file.Entries.FirstOrDefault(e => e.Key == "app_name");
        Assert.NotNull(appName);
        Assert.Contains("Application name", appName.Comment ?? "");
    }

    [Fact]
    public void Read_ParsesTranslatableFalse()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        var apiKey = file.Entries.FirstOrDefault(e => e.Key == "api_key");
        Assert.NotNull(apiKey);
        Assert.Contains("[translatable=false]", apiKey.Comment ?? "");
    }

    [Fact]
    public void Read_ParsesPlurals()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        var itemCount = file.Entries.FirstOrDefault(e => e.Key == "item_count");
        Assert.NotNull(itemCount);
        Assert.True(itemCount.IsPlural);
        Assert.NotNull(itemCount.PluralForms);
        Assert.True(itemCount.PluralForms.ContainsKey("one"));
        Assert.True(itemCount.PluralForms.ContainsKey("other"));
        Assert.Equal("%d item", itemCount.PluralForms["one"]);
        Assert.Equal("%d items", itemCount.PluralForms["other"]);
    }

    [Fact]
    public void Read_ParsesStringArrays()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var defaultLang = languages.First(l => l.IsDefault);

        // Act
        var file = _reader.Read(defaultLang);

        // Assert
        var colorEntries = file.Entries.Where(e => e.Key.StartsWith("colors[")).ToList();
        Assert.Equal(3, colorEntries.Count);
        Assert.Equal("Red", colorEntries.First(e => e.Key == "colors[0]").Value);
        Assert.Equal("Green", colorEntries.First(e => e.Key == "colors[1]").Value);
        Assert.Equal("Blue", colorEntries.First(e => e.Key == "colors[2]").Value);
    }

    [Fact]
    public void Read_SpanishLanguage_ReadsTranslations()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var spanishLang = languages.First(l => l.Code == "es");

        // Act
        var file = _reader.Read(spanishLang);

        // Assert
        var appName = file.Entries.FirstOrDefault(e => e.Key == "app_name");
        Assert.NotNull(appName);
        Assert.Equal("Aplicación de Prueba", appName.Value);
    }

    [Fact]
    public void Read_ChineseLanguage_ReadsTranslations()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var chineseLang = languages.First(l => l.Code == "zh-CN");

        // Act
        var file = _reader.Read(chineseLang);

        // Assert
        var appName = file.Entries.FirstOrDefault(e => e.Key == "app_name");
        Assert.NotNull(appName);
        Assert.Equal("测试应用", appName.Value);
    }
}
