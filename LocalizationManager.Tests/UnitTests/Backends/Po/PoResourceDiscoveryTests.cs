// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Po;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Po;

public class PoResourceDiscoveryTests
{
    private readonly string _testDataPath;

    public PoResourceDiscoveryTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Po");
    }

    #region DiscoverLanguages Tests

    [Fact]
    public void DiscoverLanguages_FlatStructure_FindsAllPoFiles()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
        Assert.Contains(languages, l => l.Code == "en");
        Assert.Contains(languages, l => l.Code == "fr");
    }

    [Fact]
    public void DiscoverLanguages_FindsPotAsDefault()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages(_testDataPath);

        // Assert
        var defaultLang = languages.FirstOrDefault(l => l.IsDefault);
        Assert.NotNull(defaultLang);
        Assert.True(defaultLang.FilePath?.EndsWith(".pot"));
    }

    [Fact]
    public void DiscoverLanguages_NonExistentPath_ReturnsEmpty()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var languages = discovery.DiscoverLanguages("/nonexistent/path");

        // Assert
        Assert.Empty(languages);
    }

    [Fact]
    public async Task DiscoverLanguagesAsync_FindsLanguages()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var languages = await discovery.DiscoverLanguagesAsync(_testDataPath);

        // Assert
        Assert.NotEmpty(languages);
    }

    #endregion

    #region DetectFolderStructure Tests

    [Fact]
    public void DetectFolderStructure_FlatPoFiles_ReturnsFlat()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var structure = discovery.DetectFolderStructure(_testDataPath);

        // Assert
        Assert.Equal(FolderStructure.Flat, structure);
    }

    [Fact]
    public void DetectFolderStructure_NonExistentPath_ReturnsUnknown()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var structure = discovery.DetectFolderStructure("/nonexistent/path");

        // Assert
        Assert.Equal(FolderStructure.Unknown, structure);
    }

    [Fact]
    public void DetectFolderStructure_GnuStructure_ReturnsGnu()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();
        var tempPath = Path.Combine(Path.GetTempPath(), $"po_gnu_test_{Guid.NewGuid()}");

        try
        {
            // Create GNU structure
            var lcMessagesPath = Path.Combine(tempPath, "locale", "en", "LC_MESSAGES");
            Directory.CreateDirectory(lcMessagesPath);
            File.WriteAllText(Path.Combine(lcMessagesPath, "messages.po"), "msgid \"\"\nmsgstr \"\"");

            // Act
            var structure = discovery.DetectFolderStructure(tempPath);

            // Assert
            Assert.Equal(FolderStructure.Gnu, structure);
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    #endregion

    #region DiscoverConfiguration Tests

    [Fact]
    public void DiscoverConfiguration_ValidPath_DetectsConfiguration()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("flat", config.FolderStructure);
        Assert.NotEmpty(config.Languages);
    }

    [Fact]
    public void DiscoverConfiguration_WithPot_FindsDomain()
    {
        // Arrange
        var discovery = new PoResourceDiscovery();

        // Act
        var config = discovery.DiscoverConfiguration(_testDataPath);

        // Assert
        Assert.Equal("messages", config.Domain);
        Assert.True(config.HasPotTemplate);
    }

    #endregion
}
