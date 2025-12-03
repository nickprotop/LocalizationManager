// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using System.Text.Json;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the init command.
/// </summary>
public class InitCommandIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public InitCommandIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"InitTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region JSON Format Tests

    [Fact]
    public void Init_JsonFormat_CreatesDefaultFile()
    {
        // Arrange
        var jsonDir = Path.Combine(_tempDirectory, "json_default");
        Directory.CreateDirectory(jsonDir);

        // Act - simulate init command behavior
        var backend = new JsonResourceBackend();
        var defaultFile = new Core.Models.ResourceFile
        {
            Language = new Core.Models.LanguageInfo
            {
                BaseName = "strings",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(jsonDir, "strings.json")
            },
            Entries = new List<Core.Models.ResourceEntry>
            {
                new() { Key = "AppTitle", Value = "My Application", Comment = "Application title" },
                new() { Key = "WelcomeMessage", Value = "Welcome!", Comment = "Welcome message" }
            }
        };
        backend.Writer.Write(defaultFile);

        // Assert
        Assert.True(File.Exists(Path.Combine(jsonDir, "strings.json")));
        var content = File.ReadAllText(Path.Combine(jsonDir, "strings.json"));
        Assert.Contains("AppTitle", content);
        Assert.Contains("WelcomeMessage", content);
    }

    [Fact]
    public void Init_JsonFormat_CreatesMultipleLanguages()
    {
        // Arrange
        var jsonDir = Path.Combine(_tempDirectory, "json_multi");
        Directory.CreateDirectory(jsonDir);
        var languages = new[] { "", "fr", "de" };

        // Act - simulate init command behavior
        var backend = new JsonResourceBackend();
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "strings.json" : $"strings.{lang}.json";
            var file = new Core.Models.ResourceFile
            {
                Language = new Core.Models.LanguageInfo
                {
                    BaseName = "strings",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(jsonDir, fileName)
                },
                Entries = new List<Core.Models.ResourceEntry>
                {
                    new() { Key = "AppTitle", Value = string.IsNullOrEmpty(lang) ? "My App" : "" },
                    new() { Key = "WelcomeMessage", Value = string.IsNullOrEmpty(lang) ? "Welcome!" : "" }
                }
            };
            backend.Writer.Write(file);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(jsonDir, "strings.json")));
        Assert.True(File.Exists(Path.Combine(jsonDir, "strings.fr.json")));
        Assert.True(File.Exists(Path.Combine(jsonDir, "strings.de.json")));

        // Verify discovery finds all
        var discovered = backend.Discovery.DiscoverLanguages(jsonDir);
        Assert.Equal(3, discovered.Count);
    }

    [Fact]
    public void Init_JsonFormat_CreatesConfigFile()
    {
        // Arrange
        var jsonDir = Path.Combine(_tempDirectory, "json_config");
        Directory.CreateDirectory(jsonDir);

        // Act - simulate creating lrm.json
        var config = new
        {
            defaultLanguageCode = "en",
            resourceFormat = "json",
            json = new
            {
                baseName = "strings",
                useNestedKeys = false,
                includeMeta = true,
                preserveComments = true
            }
        };
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(jsonDir, "lrm.json"), configJson);

        // Assert
        Assert.True(File.Exists(Path.Combine(jsonDir, "lrm.json")));
        var content = File.ReadAllText(Path.Combine(jsonDir, "lrm.json"));
        Assert.Contains("resourceFormat", content);
        Assert.Contains("json", content);
    }

    #endregion

    #region RESX Format Tests

    [Fact]
    public void Init_ResxFormat_CreatesDefaultFile()
    {
        // Arrange
        var resxDir = Path.Combine(_tempDirectory, "resx_default");
        Directory.CreateDirectory(resxDir);

        // Act - simulate init command behavior
        var backend = new ResxResourceBackend();
        var defaultFile = new Core.Models.ResourceFile
        {
            Language = new Core.Models.LanguageInfo
            {
                BaseName = "strings",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(resxDir, "strings.resx")
            },
            Entries = new List<Core.Models.ResourceEntry>
            {
                new() { Key = "AppTitle", Value = "My Application", Comment = "Application title" },
                new() { Key = "WelcomeMessage", Value = "Welcome!", Comment = "Welcome message" }
            }
        };
        backend.Writer.Write(defaultFile);

        // Assert
        Assert.True(File.Exists(Path.Combine(resxDir, "strings.resx")));
        var content = File.ReadAllText(Path.Combine(resxDir, "strings.resx"));
        Assert.Contains("AppTitle", content);
        Assert.Contains("WelcomeMessage", content);
    }

    [Fact]
    public void Init_ResxFormat_CreatesMultipleLanguages()
    {
        // Arrange
        var resxDir = Path.Combine(_tempDirectory, "resx_multi");
        Directory.CreateDirectory(resxDir);
        var languages = new[] { "", "el", "fr" };

        // Act - simulate init command behavior
        var backend = new ResxResourceBackend();
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "strings.resx" : $"strings.{lang}.resx";
            var file = new Core.Models.ResourceFile
            {
                Language = new Core.Models.LanguageInfo
                {
                    BaseName = "strings",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(resxDir, fileName)
                },
                Entries = new List<Core.Models.ResourceEntry>
                {
                    new() { Key = "AppTitle", Value = string.IsNullOrEmpty(lang) ? "My App" : "" },
                    new() { Key = "WelcomeMessage", Value = string.IsNullOrEmpty(lang) ? "Welcome!" : "" }
                }
            };
            backend.Writer.Write(file);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(resxDir, "strings.resx")));
        Assert.True(File.Exists(Path.Combine(resxDir, "strings.el.resx")));
        Assert.True(File.Exists(Path.Combine(resxDir, "strings.fr.resx")));

        // Verify discovery finds all
        var discovered = backend.Discovery.DiscoverLanguages(resxDir);
        Assert.Equal(3, discovered.Count);
    }

    #endregion

    #region Custom Base Name Tests

    [Fact]
    public void Init_CustomBaseName_UsesCorrectName()
    {
        // Arrange
        var jsonDir = Path.Combine(_tempDirectory, "custom_basename");
        Directory.CreateDirectory(jsonDir);

        // Act
        var backend = new JsonResourceBackend();
        var file = new Core.Models.ResourceFile
        {
            Language = new Core.Models.LanguageInfo
            {
                BaseName = "AppResources",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(jsonDir, "AppResources.json")
            },
            Entries = new List<Core.Models.ResourceEntry>
            {
                new() { Key = "Test", Value = "Test" }
            }
        };
        backend.Writer.Write(file);

        // Assert
        Assert.True(File.Exists(Path.Combine(jsonDir, "AppResources.json")));
    }

    #endregion
}
