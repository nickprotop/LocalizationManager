// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Commands;
using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using Spectre.Console.Cli;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class LanguageCommandsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public LanguageCommandsIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();

        // Create a default test resource file
        CreateDefaultTestResource();
        CreateGreekTestResource();
    }

    private void CreateDefaultTestResource()
    {
        var defaultLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };

        var resourceFile = new ResourceFile
        {
            Language = defaultLang,
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Value1", Comment = "Comment1" },
                new() { Key = "Key2", Value = "Value2", Comment = "Comment2" },
                new() { Key = "Key3", Value = "Value3", Comment = "Comment3" }
            }
        };

        _parser.Write(resourceFile);
    }

    private void CreateGreekTestResource()
    {
        var greekLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "el",
            Name = "Ελληνικά (el)",
            IsDefault = false,
            FilePath = Path.Combine(_testDirectory, "TestResource.el.resx")
        };

        var resourceFile = new ResourceFile
        {
            Language = greekLang,
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Τιμή1", Comment = "Σχόλιο1" },
                new() { Key = "Key2", Value = "Τιμή2", Comment = "Σχόλιο2" },
                new() { Key = "Key3", Value = "Τιμή3", Comment = "Σχόλιο3" }
            }
        };

        _parser.Write(resourceFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void ListLanguagesCommand_DisplaysAllLanguages()
    {
        // Arrange
        var app = new CommandApp<ListLanguagesCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory });

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void AddLanguageCommand_CreatesNewLanguage()
    {
        // Arrange
        var app = new CommandApp<AddLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        // Assert
        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "TestResource.fr.resx")));

        // Verify entries were copied
        var frLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "fr",
            Name = "français (fr)",
            IsDefault = false,
            FilePath = Path.Combine(_testDirectory, "TestResource.fr.resx")
        };
        var frFile = _parser.Parse(frLang);
        Assert.Equal(3, frFile.Entries.Count);
    }

    [Fact]
    public void AddLanguageCommand_EmptyFlag_CreatesEmptyLanguage()
    {
        // Arrange
        var app = new CommandApp<AddLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "de", "--empty", "-y" });

        // Assert
        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_testDirectory, "TestResource.de.resx")));

        // Verify no entries
        var deLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "de",
            Name = "Deutsch (de)",
            IsDefault = false,
            FilePath = Path.Combine(_testDirectory, "TestResource.de.resx")
        };
        var deFile = _parser.Parse(deLang);
        Assert.Empty(deFile.Entries);
    }

    [Fact]
    public void AddLanguageCommand_CopyFromSpecificLanguage_CopiesEntries()
    {
        // Arrange
        var app = new CommandApp<AddLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "fr", "--copy-from", "el", "-y" });

        // Assert
        Assert.Equal(0, result);

        var frLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "fr",
            Name = "français (fr)",
            IsDefault = false,
            FilePath = Path.Combine(_testDirectory, "TestResource.fr.resx")
        };
        var frFile = _parser.Parse(frLang);

        // Should have copied Greek entries
        Assert.Equal(3, frFile.Entries.Count);
        Assert.Equal("Τιμή1", frFile.Entries[0].Value);
    }

    [Fact]
    public void AddLanguageCommand_InvalidCultureCode_ReturnsError()
    {
        // Arrange
        var app = new CommandApp<AddLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "invalid!@#", "-y" });

        // Assert
        Assert.Equal(1, result); // Error code
    }

    [Fact]
    public void AddLanguageCommand_AlreadyExists_ReturnsError()
    {
        // Arrange
        var app = new CommandApp<AddLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act - Try to add Greek which already exists
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "el", "-y" });

        // Assert
        Assert.Equal(1, result); // Error code
    }

    [Fact]
    public void RemoveLanguageCommand_DeletesLanguage()
    {
        // Arrange - Create French first
        var addApp = new CommandApp<AddLanguageCommand>();
        addApp.Configure(config => config.PropagateExceptions());
        addApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        var removeApp = new CommandApp<RemoveLanguageCommand>();
        removeApp.Configure(config => config.PropagateExceptions());

        // Act
        var result = removeApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        // Assert
        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(_testDirectory, "TestResource.fr.resx")));
    }

    [Fact]
    public void RemoveLanguageCommand_CreatesBackup()
    {
        // Arrange - Create French first
        var addApp = new CommandApp<AddLanguageCommand>();
        addApp.Configure(config => config.PropagateExceptions());
        addApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        var removeApp = new CommandApp<RemoveLanguageCommand>();
        removeApp.Configure(config => config.PropagateExceptions());

        // Act
        var result = removeApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        // Assert
        Assert.Equal(0, result);

        // Check backup was created
        var backupDir = Path.Combine(_testDirectory, ".lrm", "backups", "TestResource.fr.resx");
        Assert.True(Directory.Exists(backupDir));
        var backupFiles = Directory.GetFiles(backupDir, "v*.resx");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public void RemoveLanguageCommand_NonExistentLanguage_ReturnsError()
    {
        // Arrange
        var app = new CommandApp<RemoveLanguageCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });

        // Assert
        Assert.Equal(1, result); // Error code
    }

    [Fact]
    public void ListLanguagesCommand_JsonFormat_OutputsJson()
    {
        // Arrange
        var app = new CommandApp<ListLanguagesCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
        });

        // Act
        var result = app.Run(new[] { "--path", _testDirectory, "--format", "json" });

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void FullWorkflow_AddListRemove_WorksCorrectly()
    {
        // Arrange
        var addApp = new CommandApp<AddLanguageCommand>();
        addApp.Configure(config => config.PropagateExceptions());

        var listApp = new CommandApp<ListLanguagesCommand>();
        listApp.Configure(config => config.PropagateExceptions());

        var removeApp = new CommandApp<RemoveLanguageCommand>();
        removeApp.Configure(config => config.PropagateExceptions());

        // Act & Assert - Add French
        var addResult = addApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });
        Assert.Equal(0, addResult);
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        Assert.Equal(3, languages.Count); // Default, Greek, French

        // Act & Assert - List languages
        var listResult = listApp.Run(new[] { "--path", _testDirectory });
        Assert.Equal(0, listResult);

        // Act & Assert - Remove French
        var removeResult = removeApp.Run(new[] { "--path", _testDirectory, "-c", "fr", "-y" });
        Assert.Equal(0, removeResult);
        languages = _discovery.DiscoverLanguages(_testDirectory);
        Assert.Equal(2, languages.Count); // Back to Default and Greek
    }
}
