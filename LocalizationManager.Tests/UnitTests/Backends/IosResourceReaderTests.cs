// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.iOS;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class IosResourceReaderTests
{
    private readonly string _testDataPath;
    private readonly IosResourceReader _reader;
    private readonly IosResourceDiscovery _discovery;

    public IosResourceReaderTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "IosResources");
        _reader = new IosResourceReader();
        _discovery = new IosResourceDiscovery();
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
    public void Read_ParsesPluralsFromStringsdict()
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
        var chineseLang = languages.First(l => l.Code == "zh-Hans");

        // Act
        var file = _reader.Read(chineseLang);

        // Assert
        var appName = file.Entries.FirstOrDefault(e => e.Key == "app_name");
        Assert.NotNull(appName);
        Assert.Equal("测试应用", appName.Value);
    }

    [Fact]
    public void Read_SpanishPlurals_ReadsCorrectly()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDataPath);
        var spanishLang = languages.First(l => l.Code == "es");

        // Act
        var file = _reader.Read(spanishLang);

        // Assert
        var itemCount = file.Entries.FirstOrDefault(e => e.Key == "item_count");
        Assert.NotNull(itemCount);
        Assert.True(itemCount.IsPlural);
        Assert.NotNull(itemCount.PluralForms);
        Assert.Equal("%d artículo", itemCount.PluralForms["one"]);
        Assert.Equal("%d artículos", itemCount.PluralForms["other"]);
    }
}
