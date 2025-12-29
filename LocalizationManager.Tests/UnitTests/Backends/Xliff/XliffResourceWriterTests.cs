// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Xliff;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Xliff;

public class XliffResourceWriterTests
{
    #region XLIFF 1.2 Writing Tests

    [Fact]
    public async Task WriteAsync_Xliff12_CreatesValidFile()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "1.2" };
        var writer = new XliffResourceWriter(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Bonjour" },
                new() { Key = "farewell", Value = "Au revoir" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            Assert.True(File.Exists(tempPath));
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("version=\"1.2\"", content);
            Assert.Contains("trans-unit", content);
            Assert.Contains("Bonjour", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task WriteAsync_Xliff12_IncludesComments()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "1.2" };
        var writer = new XliffResourceWriter(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Bonjour", Comment = "A greeting" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("<note>A greeting</note>", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion

    #region XLIFF 2.0 Writing Tests

    [Fact]
    public async Task WriteAsync_Xliff20_CreatesValidFile()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "2.0" };
        var writer = new XliffResourceWriter(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Bonjour" },
                new() { Key = "farewell", Value = "Au revoir" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            Assert.True(File.Exists(tempPath));
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("version=\"2.0\"", content);
            Assert.Contains("<unit", content);
            Assert.Contains("<segment", content);
            Assert.Contains("Bonjour", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task WriteAsync_Xliff20_IncludesNotes()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "2.0" };
        var writer = new XliffResourceWriter(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Bonjour", Comment = "A greeting message" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("<notes>", content);
            Assert.Contains("A greeting message", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion

    #region SerializeToString Tests

    [Fact]
    public void SerializeToString_Xliff12_ReturnsValidXml()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "1.2" };
        var writer = new XliffResourceWriter(config);
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "hello", Value = "Bonjour" }
            }
        };

        // Act
        var result = writer.SerializeToString(resourceFile);

        // Assert
        Assert.Contains("<?xml version=\"1.0\"", result);
        Assert.Contains("xliff", result);
        Assert.Contains("version=\"1.2\"", result);
    }

    [Fact]
    public void SerializeToString_Xliff20_ReturnsValidXml()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "2.0" };
        var writer = new XliffResourceWriter(config);
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "hello", Value = "Bonjour" }
            }
        };

        // Act
        var result = writer.SerializeToString(resourceFile);

        // Assert
        Assert.Contains("<?xml version=\"1.0\"", result);
        Assert.Contains("xliff", result);
        Assert.Contains("version=\"2.0\"", result);
        Assert.Contains("srcLang", result);
        Assert.Contains("trgLang", result);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task RoundTrip_Xliff12_PreservesContent()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "1.2" };
        var writer = new XliffResourceWriter(config);
        var reader = new XliffResourceReader(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var originalFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "fr",
                Name = "French",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Bonjour" },
                new() { Key = "farewell", Value = "Au revoir", Comment = "Goodbye message" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(originalFile);
            var readFile = await reader.ReadAsync(originalFile.Language);

            // Assert
            Assert.Equal(originalFile.Entries.Count, readFile.Entries.Count);

            var greetingEntry = readFile.Entries.First(e => e.Key == "greeting");
            Assert.Equal("Bonjour", greetingEntry.Value);

            var farewellEntry = readFile.Entries.First(e => e.Key == "farewell");
            Assert.Equal("Au revoir", farewellEntry.Value);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task RoundTrip_Xliff20_PreservesContent()
    {
        // Arrange
        var config = new XliffFormatConfiguration { Version = "2.0" };
        var writer = new XliffResourceWriter(config);
        var reader = new XliffResourceReader(config);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xliff");

        var originalFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "resources",
                Code = "de",
                Name = "German",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "greeting", Value = "Hallo" },
                new() { Key = "farewell", Value = "Auf Wiedersehen" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(originalFile);
            var readFile = await reader.ReadAsync(originalFile.Language);

            // Assert
            Assert.Equal(originalFile.Entries.Count, readFile.Entries.Count);

            var greetingEntry = readFile.Entries.First(e => e.Key == "greeting");
            Assert.Equal("Hallo", greetingEntry.Value);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion
}
