using LocalizationManager.Core.Configuration;
using System.Text.Json;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Configuration;

public class ConfigurationManagerCloudTests : IDisposable
{
    private readonly string _testDirectory;

    public ConfigurationManagerCloudTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm_config_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region LoadRemotesConfigurationAsync Tests

    [Fact]
    public async Task LoadRemotesConfigurationAsync_NoFile_ReturnsDefaultConfiguration()
    {
        // Act
        var result = await ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Remote);
        Assert.True(result.Enabled); // Default value
    }

    [Fact]
    public async Task LoadRemotesConfigurationAsync_ValidFile_ReturnsConfiguration()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var remotesPath = Path.Combine(lrmDir, "remotes.json");
        var config = new RemotesConfiguration
        {
            Remote = "https://lrm-cloud.com/org/project",
            Enabled = false
        };
        await File.WriteAllTextAsync(remotesPath, JsonSerializer.Serialize(config));

        // Act
        var result = await ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal("https://lrm-cloud.com/org/project", result.Remote);
        Assert.False(result.Enabled);
    }

    [Fact]
    public async Task LoadRemotesConfigurationAsync_InvalidJson_ThrowsConfigurationException()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var remotesPath = Path.Combine(lrmDir, "remotes.json");
        await File.WriteAllTextAsync(remotesPath, "{ invalid json }");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(
            () => ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory));
        Assert.Contains("remotes.json", ex.Message);
    }

    [Fact]
    public async Task LoadRemotesConfigurationAsync_EmptyFile_ReturnsDefaultConfiguration()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var remotesPath = Path.Combine(lrmDir, "remotes.json");
        await File.WriteAllTextAsync(remotesPath, "null");

        // Act
        var result = await ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory);

        // Assert - Returns default when deserialization returns null
        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoadRemotesConfigurationAsync_CamelCaseProperties_ParsesCorrectly()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var remotesPath = Path.Combine(lrmDir, "remotes.json");
        await File.WriteAllTextAsync(remotesPath, "{\"remote\": \"https://test.com/o/p\", \"enabled\": true}");

        // Act
        var result = await ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal("https://test.com/o/p", result.Remote);
        Assert.True(result.Enabled);
    }

    #endregion

    #region SaveRemotesConfigurationAsync Tests

    [Fact]
    public async Task SaveRemotesConfigurationAsync_CreatesLrmDirectory()
    {
        // Arrange
        var config = new RemotesConfiguration
        {
            Remote = "https://lrm-cloud.com/org/project",
            Enabled = true
        };

        // Act
        await ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, config);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".lrm")));
    }

    [Fact]
    public async Task SaveRemotesConfigurationAsync_WritesValidJson()
    {
        // Arrange
        var config = new RemotesConfiguration
        {
            Remote = "https://lrm-cloud.com/org/project",
            Enabled = false
        };

        // Act
        await ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, config);

        // Assert
        var path = Path.Combine(_testDirectory, ".lrm", "remotes.json");
        Assert.True(File.Exists(path));

        var content = await File.ReadAllTextAsync(path);
        var loaded = JsonSerializer.Deserialize<RemotesConfiguration>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(loaded);
        Assert.Equal("https://lrm-cloud.com/org/project", loaded!.Remote);
        Assert.False(loaded.Enabled);
    }

    [Fact]
    public async Task SaveRemotesConfigurationAsync_OverwritesExisting()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var remotesPath = Path.Combine(lrmDir, "remotes.json");
        await File.WriteAllTextAsync(remotesPath, "{\"remote\": \"old\"}");

        var newConfig = new RemotesConfiguration
        {
            Remote = "https://new-url.com/o/p"
        };

        // Act
        await ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, newConfig);

        // Assert
        var content = await File.ReadAllTextAsync(remotesPath);
        Assert.Contains("new-url.com", content);
        Assert.DoesNotContain("old", content);
    }

    [Fact]
    public async Task SaveRemotesConfigurationAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, null!));
    }

    [Fact]
    public async Task SaveRemotesConfigurationAsync_UsesCamelCase()
    {
        // Arrange
        var config = new RemotesConfiguration
        {
            Remote = "https://test.com/o/p",
            Enabled = true
        };

        // Act
        await ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, config);

        // Assert
        var path = Path.Combine(_testDirectory, ".lrm", "remotes.json");
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\"remote\"", content);
        Assert.Contains("\"enabled\"", content);
        Assert.DoesNotContain("\"Remote\"", content);
        Assert.DoesNotContain("\"Enabled\"", content);
    }

    #endregion

    #region EnsureGitIgnoreAsync Tests

    [Fact]
    public async Task EnsureGitIgnoreAsync_NoGitIgnore_CreatesWithLrmEntry()
    {
        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        Assert.True(File.Exists(gitIgnorePath));
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        Assert.Contains(".lrm/", content);
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_ExistingWithoutLrm_AppendsEntry()
    {
        // Arrange
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "node_modules/\nbin/\n");

        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        Assert.Contains("node_modules/", content);
        Assert.Contains("bin/", content);
        Assert.Contains(".lrm/", content);
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_AlreadyHasLrmSlash_DoesNotDuplicate()
    {
        // Arrange
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, ".lrm/\nnode_modules/\n");

        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        var count = content.Split(".lrm/").Length - 1;
        Assert.Equal(1, count); // Should appear exactly once
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_AlreadyHasLrmNoSlash_DoesNotDuplicate()
    {
        // Arrange
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, ".lrm\nnode_modules/\n");

        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        // Should not add another .lrm entry since .lrm already exists
        Assert.DoesNotContain(".lrm/\n.lrm", content);
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_EmptyGitIgnore_AddsEntry()
    {
        // Arrange
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "");

        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        Assert.Contains(".lrm/", content);
    }

    [Fact]
    public async Task EnsureGitIgnoreAsync_WhitespaceAroundEntry_StillDetects()
    {
        // Arrange
        var gitIgnorePath = Path.Combine(_testDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "  .lrm/  \nnode_modules/\n");

        // Act
        await ConfigurationManager.EnsureGitIgnoreAsync(_testDirectory);

        // Assert
        var content = await File.ReadAllTextAsync(gitIgnorePath);
        var lrmCount = content.Split(new[] { ".lrm" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(1, lrmCount);
    }

    #endregion

    #region LoadTeamConfigurationAsync Tests

    [Fact]
    public async Task LoadTeamConfigurationAsync_NoFile_ReturnsDefaultConfiguration()
    {
        // Act
        var result = await ConfigurationManager.LoadTeamConfigurationAsync(_testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ResourceFormat);
        Assert.Null(result.DefaultLanguageCode);
    }

    [Fact]
    public async Task LoadTeamConfigurationAsync_ValidFile_ReturnsConfiguration()
    {
        // Arrange
        var configPath = Path.Combine(_testDirectory, "lrm.json");
        var config = new ConfigurationModel
        {
            ResourceFormat = "resx",
            DefaultLanguageCode = "en"
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config));

        // Act
        var result = await ConfigurationManager.LoadTeamConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal("resx", result.ResourceFormat);
        Assert.Equal("en", result.DefaultLanguageCode);
    }

    [Fact]
    public async Task LoadTeamConfigurationAsync_InvalidJson_ThrowsConfigurationException()
    {
        // Arrange
        var configPath = Path.Combine(_testDirectory, "lrm.json");
        await File.WriteAllTextAsync(configPath, "{ not valid json");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(
            () => ConfigurationManager.LoadTeamConfigurationAsync(_testDirectory));
        Assert.Contains("lrm.json", ex.Message);
    }

    #endregion

    #region LoadPersonalConfigurationAsync Tests

    [Fact]
    public async Task LoadPersonalConfigurationAsync_NoFile_ReturnsNull()
    {
        // Act
        var result = await ConfigurationManager.LoadPersonalConfigurationAsync(_testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadPersonalConfigurationAsync_ValidFile_ReturnsConfiguration()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var configPath = Path.Combine(lrmDir, "config.json");
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = "fr"
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config));

        // Act
        var result = await ConfigurationManager.LoadPersonalConfigurationAsync(_testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fr", result!.DefaultLanguageCode);
    }

    [Fact]
    public async Task LoadPersonalConfigurationAsync_InvalidJson_ThrowsConfigurationException()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var configPath = Path.Combine(lrmDir, "config.json");
        await File.WriteAllTextAsync(configPath, "invalid");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(
            () => ConfigurationManager.LoadPersonalConfigurationAsync(_testDirectory));
        Assert.Contains("config.json", ex.Message);
    }

    #endregion

    #region LoadConfigurationAsync Tests (Merged)

    [Fact]
    public async Task LoadConfigurationAsync_TeamOnly_ReturnsTeamConfig()
    {
        // Arrange
        var teamConfig = new ConfigurationModel
        {
            ResourceFormat = "json",
            DefaultLanguageCode = "en"
        };
        await File.WriteAllTextAsync(
            Path.Combine(_testDirectory, "lrm.json"),
            JsonSerializer.Serialize(teamConfig));

        // Act
        var result = await ConfigurationManager.LoadConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal("json", result.ResourceFormat);
        Assert.Equal("en", result.DefaultLanguageCode);
    }

    [Fact]
    public async Task LoadConfigurationAsync_PersonalOverridesTeam()
    {
        // Arrange
        var teamConfig = new ConfigurationModel
        {
            ResourceFormat = "json",
            DefaultLanguageCode = "en"
        };
        await File.WriteAllTextAsync(
            Path.Combine(_testDirectory, "lrm.json"),
            JsonSerializer.Serialize(teamConfig));

        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var personalConfig = new ConfigurationModel
        {
            DefaultLanguageCode = "de" // Override default language
        };
        await File.WriteAllTextAsync(
            Path.Combine(lrmDir, "config.json"),
            JsonSerializer.Serialize(personalConfig));

        // Act
        var result = await ConfigurationManager.LoadConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal("json", result.ResourceFormat); // From team
        Assert.Equal("de", result.DefaultLanguageCode); // Overridden by personal
    }

    [Fact]
    public async Task LoadConfigurationAsync_NullProjectDirectory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ConfigurationManager.LoadConfigurationAsync(null!));
    }

    [Fact]
    public async Task LoadConfigurationAsync_EmptyProjectDirectory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ConfigurationManager.LoadConfigurationAsync(""));
    }

    #endregion

    #region SaveTeamConfigurationAsync Tests

    [Fact]
    public async Task SaveTeamConfigurationAsync_WritesValidFile()
    {
        // Arrange
        var config = new ConfigurationModel
        {
            ResourceFormat = "resx",
            DefaultLanguageCode = "en"
        };

        // Act
        await ConfigurationManager.SaveTeamConfigurationAsync(_testDirectory, config);

        // Assert
        var path = Path.Combine(_testDirectory, "lrm.json");
        Assert.True(File.Exists(path));

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("resx", content);
        Assert.Contains("en", content);
    }

    [Fact]
    public async Task SaveTeamConfigurationAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ConfigurationManager.SaveTeamConfigurationAsync(_testDirectory, null!));
    }

    #endregion

    #region SavePersonalConfigurationAsync Tests

    [Fact]
    public async Task SavePersonalConfigurationAsync_CreatesLrmDirectory()
    {
        // Arrange
        var config = new ConfigurationModel { DefaultLanguageCode = "fr" };

        // Act
        await ConfigurationManager.SavePersonalConfigurationAsync(_testDirectory, config);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".lrm")));
    }

    [Fact]
    public async Task SavePersonalConfigurationAsync_WritesValidFile()
    {
        // Arrange
        var config = new ConfigurationModel { DefaultLanguageCode = "fr" };

        // Act
        await ConfigurationManager.SavePersonalConfigurationAsync(_testDirectory, config);

        // Assert
        var path = Path.Combine(_testDirectory, ".lrm", "config.json");
        Assert.True(File.Exists(path));

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("fr", content);
    }

    [Fact]
    public async Task SavePersonalConfigurationAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ConfigurationManager.SavePersonalConfigurationAsync(_testDirectory, null!));
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task RemotesConfiguration_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new RemotesConfiguration
        {
            Remote = "https://lrm-cloud.com/@user/project",
            Enabled = false
        };

        // Act
        await ConfigurationManager.SaveRemotesConfigurationAsync(_testDirectory, original);
        var loaded = await ConfigurationManager.LoadRemotesConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal(original.Remote, loaded.Remote);
        Assert.Equal(original.Enabled, loaded.Enabled);
    }

    [Fact]
    public async Task TeamConfiguration_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new ConfigurationModel
        {
            ResourceFormat = "json",
            DefaultLanguageCode = "de",
            Translation = new TranslationConfiguration
            {
                DefaultProvider = "deepl",
                MaxRetries = 5
            }
        };

        // Act
        await ConfigurationManager.SaveTeamConfigurationAsync(_testDirectory, original);
        var loaded = await ConfigurationManager.LoadTeamConfigurationAsync(_testDirectory);

        // Assert
        Assert.Equal(original.ResourceFormat, loaded.ResourceFormat);
        Assert.Equal(original.DefaultLanguageCode, loaded.DefaultLanguageCode);
        Assert.NotNull(loaded.Translation);
        Assert.Equal("deepl", loaded.Translation!.DefaultProvider);
        Assert.Equal(5, loaded.Translation.MaxRetries);
    }

    #endregion
}
