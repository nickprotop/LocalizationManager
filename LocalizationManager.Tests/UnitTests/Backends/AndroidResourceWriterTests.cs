// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Android;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class AndroidResourceWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly AndroidResourceWriter _writer;
    private readonly AndroidResourceReader _reader;

    public AndroidResourceWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AndroidWriterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "res", "values"));
        _writer = new AndroidResourceWriter();
        _reader = new AndroidResourceReader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void Write_SimpleStrings_CreatesValidXml()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
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
        Assert.Contains("<string name=\"app_name\">Test App</string>", content);
        Assert.Contains("<string name=\"welcome\">Welcome!</string>", content);
    }

    [Fact]
    public void Write_WithComment_IncludesComment()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
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
        Assert.Contains("Application name", content);
    }

    [Fact]
    public void Write_TranslatableFalse_AddsAttribute()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "api_key", Value = "ABC123", Comment = "[translatable=false]" }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("translatable=\"false\"", content);
    }

    [Fact]
    public void Write_Plurals_CreatesPluralsElement()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
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
        var content = File.ReadAllText(filePath);
        Assert.Contains("<plurals name=\"item_count\">", content);
        Assert.Contains("<item quantity=\"one\">", content);
        Assert.Contains("<item quantity=\"other\">", content);
    }

    [Fact]
    public void Write_StringArray_CreatesStringArrayElement()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var file = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "colors[0]", Value = "Red", Comment = "[string-array:colors]" },
                new() { Key = "colors[1]", Value = "Green", Comment = "[string-array:colors]" },
                new() { Key = "colors[2]", Value = "Blue", Comment = "[string-array:colors]" }
            }
        };

        // Act
        _writer.Write(file);

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("<string-array name=\"colors\">", content);
        Assert.Contains("<item>Red</item>", content);
        Assert.Contains("<item>Green</item>", content);
        Assert.Contains("<item>Blue</item>", content);
    }

    [Fact]
    public void Write_RoundTrip_PreservesData()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        var originalFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "strings",
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
    public async Task CreateLanguageFileAsync_CreatesNewFile()
    {
        // Arrange
        var resPath = Path.Combine(_tempDirectory, "res");

        // Act
        await _writer.CreateLanguageFileAsync("strings", "es", resPath);

        // Assert
        var expectedPath = Path.Combine(resPath, "values-es", "strings.xml");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task DeleteLanguageFileAsync_RemovesFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "res", "values", "strings.xml");
        File.WriteAllText(filePath, "<?xml version=\"1.0\"?><resources></resources>");
        var language = new LanguageInfo
        {
            BaseName = "strings",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = filePath
        };

        // Act
        await _writer.DeleteLanguageFileAsync(language);

        // Assert
        Assert.False(File.Exists(filePath));
    }
}
