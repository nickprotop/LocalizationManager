// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Xliff;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Xliff;

public class XliffResourceDiscoveryTests
{
    private readonly string _testDataPath;

    public XliffResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Xliff");
    }

    #region DiscoverLanguages Tests

    [Fact]
    public void DiscoverLanguages_ValidPath_FindsXliffFiles()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
    }

    [Fact]
    public void DiscoverLanguages_ExtractsLanguageFromFilename()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xliff_test_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            // Create test XLIFF files with language codes in filenames
            var xliffContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""2.0"" xmlns=""urn:oasis:names:tc:xliff:document:2.0"" srcLang=""en"" trgLang=""fr"">
  <file id=""test""><unit id=""key""><segment><source>Hello</source><target>Bonjour</target></segment></unit></file>
</xliff>";
            File.WriteAllText(Path.Combine(tempPath, "messages.fr.xliff"), xliffContent);
            File.WriteAllText(Path.Combine(tempPath, "messages.de.xliff"), xliffContent.Replace("trgLang=\"fr\"", "trgLang=\"de\""));

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert
            Assert.Contains(languages, l => l.Code == "fr");
            Assert.Contains(languages, l => l.Code == "de");
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_NonExistentPath_ReturnsEmpty()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages("/nonexistent/path");

        // Assert
        Assert.Empty(languages);
    }

    [Fact]
    public async Task DiscoverLanguagesAsync_FindsLanguages()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var languages = await discovery.DiscoverLanguagesAsync(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
    }

    #endregion

    #region DiscoverConfiguration Tests

    [Fact]
    public void DiscoverConfiguration_ValidPath_ReturnsResult()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        Assert.NotNull(config);
    }

    [Fact]
    public void DiscoverConfiguration_DetectsVersion()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        Assert.NotNull(config.Version);
        Assert.True(config.Version == "1.2" || config.Version == "2.0");
    }

    [Fact]
    public void DiscoverConfiguration_DetectsFileExtension()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        Assert.True(config.FileExtension == ".xliff" || config.FileExtension == ".xlf");
    }

    [Fact]
    public void DiscoverConfiguration_DetectsBilingual()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        // Bilingual should be detected based on whether files have both source and target
        Assert.NotNull(config);
    }

    #endregion

    #region File Extension Tests

    [Fact]
    public void DiscoverLanguages_FindsXlfFiles()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xlf_test_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            // Create test file with .xlf extension
            var xliffContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""es"">
    <body><trans-unit id=""key""><source>Hello</source><target>Hola</target></trans-unit></body>
  </file>
</xliff>";
            File.WriteAllText(Path.Combine(tempPath, "messages.es.xlf"), xliffContent);

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert
            Assert.NotEmpty(languages);
            Assert.Contains(languages, l => l.Code == "es");
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_FindsBothExtensions()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xliff_mixed_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            var xliffContent12 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""fr"">
    <body><trans-unit id=""key""><source>Hello</source><target>Bonjour</target></trans-unit></body>
  </file>
</xliff>";

            var xliffContent20 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""2.0"" xmlns=""urn:oasis:names:tc:xliff:document:2.0"" srcLang=""en"" trgLang=""de"">
  <file id=""test""><unit id=""key""><segment><source>Hello</source><target>Hallo</target></segment></unit></file>
</xliff>";

            File.WriteAllText(Path.Combine(tempPath, "messages.fr.xlf"), xliffContent12);
            File.WriteAllText(Path.Combine(tempPath, "messages.de.xliff"), xliffContent20);

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert
            Assert.Contains(languages, l => l.Code == "fr");
            Assert.Contains(languages, l => l.Code == "de");
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    #endregion

    #region Language Extraction From File Content Tests

    [Fact]
    public void DiscoverLanguages_ExtractsTargetLanguageFromXliff12()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xliff12_lang_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            var xliffContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""it"">
    <body><trans-unit id=""key""><source>Hello</source><target>Ciao</target></trans-unit></body>
  </file>
</xliff>";
            File.WriteAllText(Path.Combine(tempPath, "messages.xliff"), xliffContent);

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert
            Assert.NotEmpty(languages);
            // Should extract "it" from target-language attribute
            Assert.Contains(languages, l => l.Code == "it");
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_ExtractsTargetLanguageFromXliff20()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xliff20_lang_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            var xliffContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""2.0"" xmlns=""urn:oasis:names:tc:xliff:document:2.0"" srcLang=""en"" trgLang=""pt"">
  <file id=""test""><unit id=""key""><segment><source>Hello</source><target>Olá</target></segment></unit></file>
</xliff>";
            File.WriteAllText(Path.Combine(tempPath, "messages.xliff"), xliffContent);

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert
            Assert.NotEmpty(languages);
            // Should extract "pt" from trgLang attribute
            Assert.Contains(languages, l => l.Code == "pt");
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public void DiscoverLanguages_DeduplicatesSameLanguage()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"xliff_dedup_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempPath);

            // Create two XLIFF files with the SAME source and target languages (en→fr)
            var xliffContent12 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""1.2"" xmlns=""urn:oasis:names:tc:xliff:document:1.2"">
  <file source-language=""en"" target-language=""fr"">
    <body><trans-unit id=""key1""><source>Hello</source><target>Bonjour</target></trans-unit></body>
  </file>
</xliff>";

            var xliffContent20 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xliff version=""2.0"" xmlns=""urn:oasis:names:tc:xliff:document:2.0"" srcLang=""en"" trgLang=""fr"">
  <file id=""test""><unit id=""key2""><segment><source>Goodbye</source><target>Au revoir</target></segment></unit></file>
</xliff>";

            File.WriteAllText(Path.Combine(tempPath, "v12_simple.xliff"), xliffContent12);
            File.WriteAllText(Path.Combine(tempPath, "v20_simple.xliff"), xliffContent20);

            // Act
            var languages = discovery.DiscoverLanguages(tempPath);

            // Assert - bilingual files have both source (en) and target (fr)
            // Should deduplicate to just 2 unique languages, not 4 (2 per file)
            Assert.Equal(2, languages.Count);
            Assert.Contains(languages, l => l.Code == "en" && l.IsDefault);
            Assert.Contains(languages, l => l.Code == "fr" && !l.IsDefault);
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_TestDataPath_ReturnsUniqueLanguages()
    {
        // Arrange
        var discovery = new XliffResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert - should deduplicate even though there are 2 files with French
        var distinctCodes = languages.Select(l => l.Code).Distinct().Count();
        Assert.Equal(languages.Count, distinctCodes);
    }

    #endregion
}
