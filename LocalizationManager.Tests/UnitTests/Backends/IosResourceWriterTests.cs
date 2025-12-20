// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.iOS;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class IosResourceWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IosResourceWriter _writer;
    private readonly IosResourceReader _reader;

    public IosResourceWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IosWriterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "en.lproj"));
        _writer = new IosResourceWriter();
        _reader = new IosResourceReader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void Write_SimpleStrings_CreatesValidStringsFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "Localizable",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "app_name", Value = "Test App" },
                new() { Key = "welcome", Value = "Welcome!" }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.Contains("\"app_name\" = \"Test App\";", content);
        Assert.Contains("\"welcome\" = \"Welcome!\";", content);
    }

    [Fact]
    public void Write_WithComment_IncludesComment()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "Localizable",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "app_name", Value = "Test App", Comment = "Application name" }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("/* Application name */", content);
    }

    [Fact]
    public void Write_Plurals_CreatesStringsdictFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var stringsdictPath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.stringsdict");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "Localizable",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "item_count",
                    Value = "%d items",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "%d item",
                        ["other"] = "%d items"
                    }
                }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        Assert.True(File.Exists(stringsdictPath));
        var content = File.ReadAllText(stringsdictPath);
        Assert.Contains("<key>item_count</key>", content);
        Assert.Contains("<key>one</key>", content);
        Assert.Contains("<key>other</key>", content);
        Assert.Contains("%d item", content);
        Assert.Contains("%d items", content);
    }

    [Fact]
    public void Write_MixedContent_CreatesBothFiles()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var stringsdictPath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.stringsdict");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "Localizable",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "app_name", Value = "Test App" },
                new()
                {
                    Key = "item_count",
                    Value = "%d items",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "%d item",
                        ["other"] = "%d items"
                    }
                }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(stringsdictPath));

        var stringsContent = File.ReadAllText(filePath);
        Assert.Contains("\"app_name\" = \"Test App\";", stringsContent);

        var stringsdictContent = File.ReadAllText(stringsdictPath);
        Assert.Contains("<key>item_count</key>", stringsdictContent);
    }

    [Fact]
    public void Write_RoundTrip_PreservesData()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var originalFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "Localizable",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "app_name", Value = "Test App", Comment = "App name" },
                new()
                {
                    Key = "items",
                    Value = "%d items",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "%d item",
                        ["other"] = "%d items"
                    }
                }
            }
        };

        // Act
        _writer.Write(originalFile);
        var readFile = _reader.Read(originalFile.Language);

        // Assert
        Assert.Equal(2, readFile.Entries.Count);
        Assert.Equal("Test App", readFile.Entries.First(e => e.Key == "app_name").Value);
        var plural = readFile.Entries.First(e => e.Key == "items");
        Assert.True(plural.IsPlural);
        Assert.Equal("%d item", plural.PluralForms!["one"]);
    }

    [Fact]
    public async Task CreateLanguageFileAsync_CreatesNewLprojFolder()
    {
        // Act
        await _writer.CreateLanguageFileAsync("Localizable", "es", _tempDirectory);

        // Assert
        var expectedPath = Path.Combine(_tempDirectory, "es.lproj", "Localizable.strings");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task DeleteLanguageFileAsync_RemovesBothFiles()
    {
        // Arrange
        var stringsPath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.strings");
        var stringsdictPath = Path.Combine(_tempDirectory, "en.lproj", "Localizable.stringsdict");
        File.WriteAllText(stringsPath, "\"test\" = \"Test\";");
        File.WriteAllText(stringsdictPath, "<?xml version=\"1.0\"?><plist></plist>");
        var language = new LanguageInfo
        {
            BaseName = "Localizable",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = stringsPath
        };

        // Act
        await _writer.DeleteLanguageFileAsync(language);

        // Assert
        Assert.False(File.Exists(stringsPath));
        Assert.False(File.Exists(stringsdictPath));
    }
}
