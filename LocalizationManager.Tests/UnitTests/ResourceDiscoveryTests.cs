// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class ResourceDiscoveryTests
{
    private readonly string _testDataPath;
    private readonly IResourceDiscovery _discovery = new ResxResourceDiscovery();

    public ResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "FlatResx");
    }

    [Fact]
    public void DiscoverLanguages_ValidDirectory_FindsLanguages()
    {
        // Act
        var languages = _discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
        Assert.Equal(3, languages.Count); // TestResource.resx, TestResource.el.resx, and TestResource.fr.resx
    }

    [Fact]
    public void DiscoverLanguages_ValidDirectory_IdentifiesDefaultLanguage()
    {
        // Act
        var languages = _discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.Equal("", defaultLang.Code);
        Assert.Contains("Default", defaultLang.Name);
    }

    [Fact]
    public void DiscoverLanguages_ValidDirectory_IdentifiesGreekLanguage()
    {
        // Act
        var languages = _discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var greekLang = languages.FirstOrDefault(l => l.Code == "el");
        Assert.NotNull(greekLang);
        Assert.Equal("el", greekLang.Code);
        Assert.False(greekLang.IsDefault);
        Assert.Contains("TestResource.el.resx", greekLang.FilePath);
    }

    [Fact]
    public void DiscoverLanguages_EmptyDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => _discovery.DiscoverLanguages(nonExistentPath));
    }

    [Fact]
    public void DiscoverLanguages_DirectoryWithNoResxFiles_ReturnsEmptyList()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var languages = _discovery.DiscoverLanguages(tempDir);

            // Assert
            Assert.Empty(languages);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void DiscoverLanguages_ValidDirectory_SetsCorrectFilePaths()
    {
        // Act
        var languages = _discovery.DiscoverLanguages(_testDataPath);

        // Assert
        foreach (var lang in languages)
        {
            Assert.NotNull(lang.FilePath);
            Assert.True(File.Exists(lang.FilePath), $"File path {lang.FilePath} does not exist");
            Assert.EndsWith(".resx", lang.FilePath);
        }
    }

    [Fact]
    public void DiscoverResourceGroups_MultiBaseDirectory_ReturnsOneGroupPerBaseName()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

        var directory = _discovery.DiscoverResourceGroups(path);

        Assert.Equal(2, directory.Groups.Count);
        Assert.Contains(directory.Groups, g => g.BaseName == "CustomerResources");
        Assert.Contains(directory.Groups, g => g.BaseName == "GlassResources");
    }

    [Fact]
    public void DiscoverResourceGroups_MultiBaseDirectory_ReturnsOneCultureCodePerLanguage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

        var directory = _discovery.DiscoverResourceGroups(path);

        // Two cultures: invariant ("") and Italian ("it")
        Assert.Equal(2, directory.CultureCodes.Count);
        Assert.Contains("", directory.CultureCodes);
        Assert.Contains("it", directory.CultureCodes);
    }

    [Fact]
    public void DiscoverResourceGroups_MultiBaseDirectory_EachGroupHasAllCultures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

        var directory = _discovery.DiscoverResourceGroups(path);

        foreach (var group in directory.Groups)
        {
            Assert.Equal(2, group.Files.Count);
            Assert.Contains(group.Files, f => f.IsDefault);
            Assert.Contains(group.Files, f => f.Code == "it");
        }
    }

    [Fact]
    public void DiscoverLanguages_FilesInSubfolders_AreDiscovered()
    {
        // Bug #4: .resx files nested under subfolders of the resource path were
        // not discovered (TopDirectoryOnly). They must now be found recursively.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nested = Path.Combine(tempDir, "Components", "Account", "Pages");
        Directory.CreateDirectory(nested);

        try
        {
            File.WriteAllText(Path.Combine(nested, "Login.resx"), EmptyResx);
            File.WriteAllText(Path.Combine(nested, "Login.it.resx"), EmptyResx);

            var languages = _discovery.DiscoverLanguages(tempDir);

            Assert.Equal(2, languages.Count);
            Assert.Contains(languages, l => l.IsDefault && l.FilePath.EndsWith("Login.resx"));
            Assert.Contains(languages, l => l.Code == "it");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DiscoverResourceGroups_DistinctlyNamedGroupsInSubfolders_AreDiscovered()
    {
        // The reported scenario: distinctly-named resource groups living in different
        // subfolders must each be discovered with their default + culture files.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folderA = Path.Combine(tempDir, "Customers", "Pages");
        var folderB = Path.Combine(tempDir, "Account");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        try
        {
            File.WriteAllText(Path.Combine(folderA, "CustomerResources.resx"), EmptyResx);
            File.WriteAllText(Path.Combine(folderA, "CustomerResources.it.resx"), EmptyResx);
            File.WriteAllText(Path.Combine(folderB, "Login.resx"), EmptyResx);
            File.WriteAllText(Path.Combine(folderB, "Login.it.resx"), EmptyResx);

            var directory = _discovery.DiscoverResourceGroups(tempDir);

            Assert.Equal(2, directory.Groups.Count);
            Assert.Contains(directory.Groups, g => g.BaseName == "CustomerResources");
            Assert.Contains(directory.Groups, g => g.BaseName == "Login");
            foreach (var group in directory.Groups)
            {
                Assert.Equal(2, group.Files.Count);
                Assert.Contains(group.Files, f => f.IsDefault);
                Assert.Contains(group.Files, f => f.Code == "it");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_WithDefaultLanguageCode_LabelsSuffixlessFileWithConfiguredCode()
    {
        // Bug #1: the suffix-less default file must carry the configured default
        // language code (e.g. "it") instead of an empty code, so every client
        // shows it as the real language rather than guessing "English".
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "CustomerResources.resx"), EmptyResx);
            File.WriteAllText(Path.Combine(tempDir, "CustomerResources.it.resx"), EmptyResx);

            var discovery = new ResxResourceDiscovery("it");
            var languages = discovery.DiscoverLanguages(tempDir);

            var defaultLang = languages.Single(l => l.IsDefault);
            Assert.Equal("it", defaultLang.Code);
            Assert.EndsWith("CustomerResources.resx", defaultLang.FilePath);

            // The suffixed .it file should not be a second, duplicate "it" default.
            Assert.Single(languages, l => l.IsDefault);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DiscoverLanguages_WithoutDefaultLanguageCode_KeepsEmptyDefaultCode()
    {
        // Backward compatibility: with no configured default, the suffix-less file
        // keeps Code = "" as before.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "CustomerResources.resx"), EmptyResx);

            var discovery = new ResxResourceDiscovery();
            var languages = discovery.DiscoverLanguages(tempDir);

            var defaultLang = languages.Single(l => l.IsDefault);
            Assert.Equal("", defaultLang.Code);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private const string EmptyResx =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n  <resheader name=\"version\"><value>2.0</value></resheader>\n  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n</root>\n";
}
