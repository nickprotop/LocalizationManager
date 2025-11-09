// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class ResourceFileParserTests
{
    private readonly string _testDataPath;

    public ResourceFileParserTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    [Fact]
    public void Parse_ValidResxFile_ReturnsResourceFile()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "en",
            Name = "English (Default)",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.resx")
        };

        // Act
        var result = parser.Parse(languageInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(languageInfo, result.Language);
        Assert.NotEmpty(result.Entries);
        Assert.Equal(4, result.Entries.Count); // Save, Cancel, Welcome, EmptyValue
    }

    [Fact]
    public void Parse_ValidResxFile_ParsesEntriesCorrectly()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "en",
            Name = "English (Default)",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.resx")
        };

        // Act
        var result = parser.Parse(languageInfo);

        // Assert
        var saveEntry = result.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.NotNull(saveEntry);
        Assert.Equal("Save", saveEntry.Value);
        Assert.Equal("Save button label", saveEntry.Comment);
    }

    [Fact]
    public void Parse_ValidResxFile_ParsesEmptyValues()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var languageInfo = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "en",
            Name = "English (Default)",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "TestResource.resx")
        };

        // Act
        var result = parser.Parse(languageInfo);

        // Assert
        var emptyEntry = result.Entries.FirstOrDefault(e => e.Key == "EmptyValue");
        Assert.NotNull(emptyEntry);
        Assert.Equal(string.Empty, emptyEntry.Value);
    }

    [Fact]
    public void Parse_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var languageInfo = new LanguageInfo
        {
            BaseName = "NonExistent",
            Code = "en",
            Name = "English",
            IsDefault = true,
            FilePath = Path.Combine(_testDataPath, "NonExistent.resx")
        };

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => parser.Parse(languageInfo));
    }

    [Fact]
    public void Write_ValidResourceFile_CreatesFile()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var tempFile = Path.Combine(Path.GetTempPath(), "TempTest.resx");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TempTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "TestKey", Value = "Test Value", Comment = "Test Comment" }
            }
        };

        try
        {
            // Act
            parser.Write(resourceFile);

            // Assert
            Assert.True(File.Exists(tempFile));

            // Verify we can read it back
            var readBack = parser.Parse(resourceFile.Language);
            Assert.Single(readBack.Entries);
            Assert.Equal("TestKey", readBack.Entries[0].Key);
            Assert.Equal("Test Value", readBack.Entries[0].Value);
            Assert.Equal("Test Comment", readBack.Entries[0].Comment);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Write_ShouldPreserveOriginalOrder()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var tempFile = Path.Combine(Path.GetTempPath(), "OrderTest.resx");

        // Create initial file with specific order
        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "OrderTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Zebra", Value = "Z" },
                new() { Key = "Alpha", Value = "A" },
                new() { Key = "Mike", Value = "M" },
                new() { Key = "Beta", Value = "B" }
            }
        };

        try
        {
            // Act - Write initial file
            parser.Write(initialFile);

            // Update one value (not adding/removing, just updating)
            var updatedFile = parser.Parse(initialFile.Language);
            updatedFile.Entries.First(e => e.Key == "Mike").Value = "Modified M";
            parser.Write(updatedFile);

            // Assert - Order should be preserved
            var result = parser.Parse(initialFile.Language);
            Assert.Equal(4, result.Entries.Count);
            Assert.Equal("Zebra", result.Entries[0].Key); // Still first
            Assert.Equal("Alpha", result.Entries[1].Key); // Still second
            Assert.Equal("Mike", result.Entries[2].Key);  // Still third
            Assert.Equal("Beta", result.Entries[3].Key);  // Still fourth
            Assert.Equal("Modified M", result.Entries[2].Value);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_ShouldPreserveXmlSchema()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var sourceFile = Path.Combine(_testDataPath, "TestResource.resx");
        var tempFile = Path.Combine(Path.GetTempPath(), "SchemaTest.resx");

        // Copy test file to temp location
        File.Copy(sourceFile, tempFile, true);

        try
        {
            // Read original XML to check schema
            var originalXml = File.ReadAllText(tempFile);
            var hasSchema = originalXml.Contains("http://www.w3.org/2001/XMLSchema");

            // Act - Parse and write back
            var languageInfo = new LanguageInfo
            {
                BaseName = "SchemaTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            };
            var resourceFile = parser.Parse(languageInfo);

            // Modify one value
            resourceFile.Entries.First(e => e.Key == "Save").Value = "Modified Save";
            parser.Write(resourceFile);

            // Assert - Schema should still be present
            var modifiedXml = File.ReadAllText(tempFile);

            if (hasSchema)
            {
                Assert.Contains("http://www.w3.org/2001/XMLSchema", modifiedXml);
            }

            // Verify the change was applied
            var result = parser.Parse(languageInfo);
            Assert.Equal("Modified Save", result.Entries.First(e => e.Key == "Save").Value);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_ShouldOnlyModifyChangedEntries()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var tempFile = Path.Combine(Path.GetTempPath(), "ModifyTest.resx");

        // Create initial file
        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "ModifyTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Value1", Comment = "Comment1" },
                new() { Key = "Key2", Value = "Value2", Comment = "Comment2" },
                new() { Key = "Key3", Value = "Value3", Comment = "Comment3" }
            }
        };

        try
        {
            // Act - Write, then modify only one entry
            parser.Write(initialFile);
            var originalXml = File.ReadAllText(tempFile);

            var updatedFile = parser.Parse(initialFile.Language);
            updatedFile.Entries.First(e => e.Key == "Key2").Value = "Modified Value2";
            parser.Write(updatedFile);

            var modifiedXml = File.ReadAllText(tempFile);

            // Assert - Key1 and Key3 sections should remain unchanged
            // Only Key2 should be different
            Assert.Contains("Value1", modifiedXml); // Key1 unchanged
            Assert.Contains("Modified Value2", modifiedXml); // Key2 changed
            Assert.Contains("Value3", modifiedXml); // Key3 unchanged

            // Verify all entries are still present
            var result = parser.Parse(initialFile.Language);
            Assert.Equal(3, result.Entries.Count);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_ShouldAddNewEntriesAtEnd()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var tempFile = Path.Combine(Path.GetTempPath(), "AddTest.resx");

        // Create initial file with 2 entries
        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "AddTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "First", Value = "1" },
                new() { Key = "Second", Value = "2" }
            }
        };

        try
        {
            // Act - Write initial, then add a new entry
            parser.Write(initialFile);

            var updatedFile = parser.Parse(initialFile.Language);
            updatedFile.Entries.Add(new ResourceEntry { Key = "Third", Value = "3" });
            parser.Write(updatedFile);

            // Assert - New entry should be at the end
            var result = parser.Parse(initialFile.Language);
            Assert.Equal(3, result.Entries.Count);
            Assert.Equal("First", result.Entries[0].Key);
            Assert.Equal("Second", result.Entries[1].Key);
            Assert.Equal("Third", result.Entries[2].Key); // Added at end
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_ShouldRemoveDeletedEntries()
    {
        // Arrange
        var parser = new ResourceFileParser();
        var tempFile = Path.Combine(Path.GetTempPath(), "DeleteTest.resx");

        // Create initial file with 3 entries
        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "DeleteTest",
                Code = "en",
                Name = "English",
                IsDefault = true,
                FilePath = tempFile
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Keep1", Value = "K1" },
                new() { Key = "Remove", Value = "R" },
                new() { Key = "Keep2", Value = "K2" }
            }
        };

        try
        {
            // Act - Write initial, then remove middle entry
            parser.Write(initialFile);

            var updatedFile = parser.Parse(initialFile.Language);
            updatedFile.Entries.RemoveAll(e => e.Key == "Remove");
            parser.Write(updatedFile);

            // Assert - Removed entry should be gone, others preserved in order
            var result = parser.Parse(initialFile.Language);
            Assert.Equal(2, result.Entries.Count);
            Assert.Equal("Keep1", result.Entries[0].Key);
            Assert.Equal("Keep2", result.Entries[1].Key);
            Assert.DoesNotContain(result.Entries, e => e.Key == "Remove");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
