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
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
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
}
