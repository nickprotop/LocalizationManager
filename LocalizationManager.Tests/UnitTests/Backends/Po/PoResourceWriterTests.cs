// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Po;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends.Po;

public class PoResourceWriterTests
{
    #region Basic Writing Tests

    [Fact]
    public async Task WriteAsync_ValidResourceFile_CreatesPoFile()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.po");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "en",
                Name = "English",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Hello", Value = "Hello" },
                new() { Key = "Goodbye", Value = "Goodbye" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            Assert.True(File.Exists(tempPath));
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("msgid \"Hello\"", content);
            Assert.Contains("msgstr \"Hello\"", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task WriteAsync_WithComments_IncludesCommentsInOutput()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.po");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "en",
                Name = "English",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Hello", Value = "Hello", Comment = "A greeting" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("# A greeting", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task WriteAsync_WithPlurals_WritesPluralForms()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.po");

        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "en",
                Name = "English",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "%d file",
                    Value = "%d files",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "%d file",
                        ["other"] = "%d files"
                    }
                }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(resourceFile);

            // Assert
            var content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("msgid \"%d file\"", content);
            Assert.Contains("msgid_plural", content);
            Assert.Contains("msgstr[0]", content);
            Assert.Contains("msgstr[1]", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion

    #region Escape Tests

    [Fact]
    public void EscapeString_Newline_ReturnsEscapedNewline()
    {
        // Act
        var result = PoResourceWriter.EscapeString("Hello\nWorld");

        // Assert
        Assert.Equal("Hello\\nWorld", result);
    }

    [Fact]
    public void EscapeString_Tab_ReturnsEscapedTab()
    {
        // Act
        var result = PoResourceWriter.EscapeString("Hello\tWorld");

        // Assert
        Assert.Equal("Hello\\tWorld", result);
    }

    [Fact]
    public void EscapeString_Backslash_ReturnsEscapedBackslash()
    {
        // Act
        var result = PoResourceWriter.EscapeString("C:\\Path");

        // Assert
        Assert.Equal("C:\\\\Path", result);
    }

    [Fact]
    public void EscapeString_Quote_ReturnsEscapedQuote()
    {
        // Act
        var result = PoResourceWriter.EscapeString("Say \"Hello\"");

        // Assert
        Assert.Equal("Say \\\"Hello\\\"", result);
    }

    [Fact]
    public void EscapeString_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = PoResourceWriter.EscapeString("");

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region Serialize Tests

    [Fact]
    public void SerializeToString_ValidResourceFile_ReturnsValidPoFormat()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "en",
                Name = "English",
                IsDefault = false
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Hello", Value = "Hello" }
            }
        };

        // Act
        var result = writer.SerializeToString(resourceFile);

        // Assert
        Assert.Contains("msgid \"\"", result); // Header
        Assert.Contains("msgstr \"\"", result); // Header
        Assert.Contains("Language: en", result);
        Assert.Contains("msgid \"Hello\"", result);
        Assert.Contains("msgstr \"Hello\"", result);
    }

    [Fact]
    public void SerializeToString_IncludesHeader()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "fr",
                Name = "French",
                IsDefault = false
            },
            Entries = new List<ResourceEntry>()
        };

        // Act
        var result = writer.SerializeToString(resourceFile);

        // Assert
        Assert.Contains("MIME-Version: 1.0", result);
        Assert.Contains("Content-Type: text/plain; charset=UTF-8", result);
        Assert.Contains("Language: fr", result);
        Assert.Contains("X-Generator: LRM", result);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task RoundTrip_WriteAndRead_PreservesContent()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var reader = new PoResourceReader();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.po");

        var originalFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "messages",
                Code = "en",
                Name = "English",
                IsDefault = false,
                FilePath = tempPath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Hello", Value = "Hello World" },
                new() { Key = "Goodbye", Value = "Goodbye World", Comment = "A farewell" }
            }
        };

        try
        {
            // Act
            await writer.WriteAsync(originalFile);
            var readFile = await reader.ReadAsync(originalFile.Language);

            // Assert
            Assert.Equal(originalFile.Entries.Count, readFile.Entries.Count);

            var helloEntry = readFile.Entries.First(e => e.Key == "Hello");
            Assert.Equal("Hello World", helloEntry.Value);

            var goodbyeEntry = readFile.Entries.First(e => e.Key == "Goodbye");
            Assert.Equal("Goodbye World", goodbyeEntry.Value);
            Assert.Contains("farewell", goodbyeEntry.Comment);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion

    #region Formatting Preservation Tests

    [Fact]
    public void Write_UnchangedValue_PreservesOriginalFormatting()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };
        var entries = new List<ResourceEntry>
        {
            new ResourceEntry
            {
                Key = "test.key",
                Value = "Line one\nLine two",
                OriginalFormatting = "msgstr \"\"\n\"Line one\\n\"\n\"Line two\""
            }
        };
        var file = new ResourceFile { Language = languageInfo, Entries = entries };

        // Act
        var output = writer.SerializeToString(file);

        // Assert
        Assert.Contains("msgstr \"\"", output);
        Assert.Contains("\"Line one\\n\"", output);
        Assert.Contains("\"Line two\"", output);
    }

    [Fact]
    public void Write_ChangedValue_RegeneratesFormatting()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };
        var entries = new List<ResourceEntry>
        {
            new ResourceEntry
            {
                Key = "test.key",
                Value = "New translation value",
                OriginalFormatting = "msgstr \"\"\n\"Old translation value\""
            }
        };
        var file = new ResourceFile { Language = languageInfo, Entries = entries };

        // Act
        var output = writer.SerializeToString(file);

        // Assert
        Assert.DoesNotContain("Old translation value", output);
        Assert.Contains("New translation value", output);
    }

    [Fact]
    public void Write_NoOriginalFormatting_GeneratesFormatting()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };
        var entries = new List<ResourceEntry>
        {
            new ResourceEntry
            {
                Key = "test.key",
                Value = "Translation value",
                OriginalFormatting = null
            }
        };
        var file = new ResourceFile { Language = languageInfo, Entries = entries };

        // Act
        var output = writer.SerializeToString(file);

        // Assert
        Assert.Contains("msgstr", output);
        Assert.Contains("Translation value", output);
    }

    [Fact]
    public void Write_LongUnchangedValue_PreservesOriginalWrapping()
    {
        // Arrange
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };
        var longValue = "This is a very long translation that was wrapped at specific points";
        var entries = new List<ResourceEntry>
        {
            new ResourceEntry
            {
                Key = "test.key",
                Value = longValue,
                OriginalFormatting = "msgstr \"\"\n\"This is a very long translation that was wrapped \"\n\"at specific points\""
            }
        };
        var file = new ResourceFile { Language = languageInfo, Entries = entries };

        // Act
        var output = writer.SerializeToString(file);

        // Assert
        Assert.Contains("\"This is a very long translation that was wrapped \"", output);
        Assert.Contains("\"at specific points\"", output);
    }

    [Fact]
    public void ReadWriteCycle_UnchangedFile_PreservesFormatting()
    {
        // Arrange - original PO content with specific formatting
        var originalPoContent = @"
msgid ""app.title""
msgstr ""Application Title""

msgid ""app.description""
msgstr """"
""Long description wrapped""
""at specific point""

msgid ""test.key""
msgstr ""Simple value""
";
        var reader = new PoResourceReader();
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act - Read and write back without changes
        var entries = reader.Read(new StringReader(originalPoContent), languageInfo);
        var outputPoContent = writer.SerializeToString(entries);

        // Assert - msgstr lines should match (allowing for header differences)
        var originalMsgStrLines = originalPoContent.Split('\n')
            .Where(l => l.Trim().StartsWith("msgstr") || l.Trim().StartsWith("\""))
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        var outputMsgStrLines = outputPoContent.Split('\n')
            .Where(l => l.Trim().StartsWith("msgstr") || l.Trim().StartsWith("\""))
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .Skip(2) // Skip header msgstr lines
            .ToList();

        // Check that the non-header msgstr formatting is preserved
        Assert.Contains("msgstr \"Application Title\"", outputMsgStrLines);
        Assert.Contains("msgstr \"\"", outputMsgStrLines);
        Assert.Contains("\"Long description wrapped\"", outputMsgStrLines);
        Assert.Contains("\"at specific point\"", outputMsgStrLines);
    }

    [Fact]
    public void ReadWriteCycle_OneValueChanged_PreservesOtherFormatting()
    {
        // Arrange
        var originalPoContent = @"
msgid ""key1""
msgstr """"
""Original multi-line""
""formatting preserved""

msgid ""key2""
msgstr ""This value will change""

msgid ""key3""
msgstr ""Another preserved value""
";
        var reader = new PoResourceReader();
        var writer = new PoResourceWriter();
        var languageInfo = new LanguageInfo { Code = "en", BaseName = "test", Name = "English" };

        // Act - Read, change key2, write back
        var file = reader.Read(new StringReader(originalPoContent), languageInfo);
        var key2Entry = file.Entries.First(e => e.Key == "key2");
        key2Entry.Value = "New changed value";
        var outputPoContent = writer.SerializeToString(file);

        // Assert
        // key1 and key3 should preserve original formatting
        Assert.Contains("\"Original multi-line\"", outputPoContent);
        Assert.Contains("\"formatting preserved\"", outputPoContent);
        Assert.Contains("\"Another preserved value\"", outputPoContent);

        // key2 should have new value with regenerated formatting
        Assert.Contains("New changed value", outputPoContent);
        Assert.DoesNotContain("This value will change", outputPoContent);
    }

    #endregion
}
