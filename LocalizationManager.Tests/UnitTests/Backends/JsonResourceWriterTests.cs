// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using System.Text.Json;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

public class JsonResourceWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JsonResourceReader _reader;

    public JsonResourceWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"JsonWriterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _reader = new JsonResourceReader();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Basic Write Tests

    [Fact]
    public void Write_ValidResourceFile_CreatesFile()
    {
        // Arrange
        var writer = new JsonResourceWriter();
        var filePath = Path.Combine(_tempDirectory, "test.json");
        var resourceFile = CreateTestResourceFile(filePath);

        // Act
        writer.Write(resourceFile);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Write_ValidResourceFile_CanBeReadBack()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "roundtrip.json");
        var resourceFile = CreateTestResourceFile(filePath);

        // Act
        writer.Write(resourceFile);
        var readBack = reader.Read(resourceFile.Language);

        // Assert
        Assert.Equal(resourceFile.Entries.Count, readBack.Entries.Count);
        Assert.Equal("Test Value", readBack.Entries.First(e => e.Key == "TestKey").Value);
        Assert.Equal("Test Comment", readBack.Entries.First(e => e.Key == "TestKey").Comment);
    }

    [Fact]
    public void Write_WithNestedKeys_CreatesNestedStructure()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = true, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "nested.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "nested",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Errors.NotFound", Value = "Not found" },
                new() { Key = "Errors.AccessDenied", Value = "Access denied" },
                new() { Key = "Buttons.OK", Value = "OK" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - verify nested structure
        Assert.True(doc.RootElement.TryGetProperty("Errors", out var errors));
        Assert.True(errors.TryGetProperty("NotFound", out var notFound));
        Assert.Equal("Not found", notFound.GetString());
    }

    [Fact]
    public void Write_WithFlatKeys_CreatesFlatStructure()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "flat.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "flat",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Errors.NotFound", Value = "Not found" },
                new() { Key = "Errors.AccessDenied", Value = "Access denied" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - verify flat structure
        Assert.True(doc.RootElement.TryGetProperty("Errors.NotFound", out var notFound));
        Assert.Equal("Not found", notFound.GetString());
    }

    #endregion

    #region Meta Tests

    [Fact]
    public void Write_WithIncludeMeta_AddsMetaSection()
    {
        // Arrange
        var config = new JsonFormatConfiguration { IncludeMeta = true };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "meta.json");
        var resourceFile = CreateTestResourceFile(filePath);

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("_meta", out var meta));
        Assert.True(meta.TryGetProperty("version", out _));
        Assert.True(meta.TryGetProperty("generator", out _));
        Assert.True(meta.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public void Write_WithoutIncludeMeta_NoMetaSection()
    {
        // Arrange
        var config = new JsonFormatConfiguration { IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "nometa.json");
        var resourceFile = CreateTestResourceFile(filePath);

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.False(doc.RootElement.TryGetProperty("_meta", out _));
    }

    #endregion

    #region Comments Tests

    [Fact]
    public void Write_WithPreserveComments_IncludesComments()
    {
        // Arrange
        var config = new JsonFormatConfiguration { PreserveComments = true, UseNestedKeys = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "comments.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "comments",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save", Comment = "Save button label" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - with comments, entry should be an object with _value and _comment
        Assert.True(doc.RootElement.TryGetProperty("Save", out var save));
        Assert.Equal(JsonValueKind.Object, save.ValueKind);
        Assert.True(save.TryGetProperty("_value", out var value));
        Assert.Equal("Save", value.GetString());
        Assert.True(save.TryGetProperty("_comment", out var comment));
        Assert.Equal("Save button label", comment.GetString());
    }

    [Fact]
    public void Write_WithoutPreserveComments_NoComments()
    {
        // Arrange
        var config = new JsonFormatConfiguration { PreserveComments = false, UseNestedKeys = false, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "nocomments.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "nocomments",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save", Comment = "This should not appear" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - without comments, entry should be a simple string
        Assert.True(doc.RootElement.TryGetProperty("Save", out var save));
        Assert.Equal(JsonValueKind.String, save.ValueKind);
        Assert.Equal("Save", save.GetString());
    }

    #endregion

    #region Order Preservation Tests

    [Fact]
    public void Write_ShouldPreserveKeyOrder()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false, PreserveComments = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "order.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "order",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Zebra", Value = "Z" },
                new() { Key = "Alpha", Value = "A" },
                new() { Key = "Mike", Value = "M" },
                new() { Key = "Beta", Value = "B" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - verify original order is preserved
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal("Zebra", keys[0]);
        Assert.Equal("Alpha", keys[1]);
        Assert.Equal("Mike", keys[2]);
        Assert.Equal("Beta", keys[3]);
    }

    #endregion

    #region Update/Modify Tests

    [Fact]
    public void Write_UpdateExistingFile_PreservesOtherEntries()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false, PreserveComments = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "update.json");

        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "update",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Value1" },
                new() { Key = "Key2", Value = "Value2" },
                new() { Key = "Key3", Value = "Value3" }
            }
        };

        // Act - write initial, then update one entry
        writer.Write(initialFile);
        var updatedFile = reader.Read(initialFile.Language);
        updatedFile.Entries.First(e => e.Key == "Key2").Value = "Modified Value2";
        writer.Write(updatedFile);

        // Assert
        var result = reader.Read(initialFile.Language);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("Value1", result.Entries.First(e => e.Key == "Key1").Value);
        Assert.Equal("Modified Value2", result.Entries.First(e => e.Key == "Key2").Value);
        Assert.Equal("Value3", result.Entries.First(e => e.Key == "Key3").Value);
    }

    [Fact]
    public void Write_AddNewEntry_AppendsToEnd()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false, PreserveComments = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "add.json");

        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "add",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "First", Value = "1" },
                new() { Key = "Second", Value = "2" }
            }
        };

        // Act
        writer.Write(initialFile);
        var updatedFile = reader.Read(initialFile.Language);
        updatedFile.Entries.Add(new ResourceEntry { Key = "Third", Value = "3" });
        writer.Write(updatedFile);

        // Assert
        var result = reader.Read(initialFile.Language);
        Assert.Equal(3, result.Entries.Count);
    }

    [Fact]
    public void Write_RemoveEntry_EntryIsRemoved()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false, PreserveComments = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "remove.json");

        var initialFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "remove",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Keep1", Value = "K1" },
                new() { Key = "Remove", Value = "R" },
                new() { Key = "Keep2", Value = "K2" }
            }
        };

        // Act
        writer.Write(initialFile);
        var updatedFile = reader.Read(initialFile.Language);
        updatedFile.Entries.RemoveAll(e => e.Key == "Remove");
        writer.Write(updatedFile);

        // Assert
        var result = reader.Read(initialFile.Language);
        Assert.Equal(2, result.Entries.Count);
        Assert.DoesNotContain(result.Entries, e => e.Key == "Remove");
    }

    #endregion

    #region Plural Tests

    [Fact]
    public void Write_PluralEntry_WritesNestedCLDRFormat()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "plural.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "plural",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "itemCount",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "{0} item",
                        ["other"] = "{0} items"
                    }
                }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - verify nested CLDR plural structure
        Assert.True(doc.RootElement.TryGetProperty("itemCount", out var itemCount));
        Assert.Equal(JsonValueKind.Object, itemCount.ValueKind);
        Assert.True(itemCount.TryGetProperty("_plural", out var plural));
        Assert.True(plural.GetBoolean());
        Assert.True(itemCount.TryGetProperty("one", out var one));
        Assert.Equal("{0} item", one.GetString());
        Assert.True(itemCount.TryGetProperty("other", out var other));
        Assert.Equal("{0} items", other.GetString());
    }

    [Fact]
    public void Write_PluralEntry_RoundTrip()
    {
        // Arrange
        var config = new JsonFormatConfiguration { UseNestedKeys = false, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "plural_roundtrip.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "plural_roundtrip",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "messages",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["zero"] = "No messages",
                        ["one"] = "{0} message",
                        ["other"] = "{0} messages"
                    }
                }
            }
        };

        // Act
        writer.Write(resourceFile);
        var readBack = reader.Read(resourceFile.Language);

        // Assert
        var entry = readBack.Entries.First(e => e.Key == "messages");
        Assert.True(entry.IsPlural);
        Assert.NotNull(entry.PluralForms);
        Assert.Equal(3, entry.PluralForms.Count);
        Assert.Equal("No messages", entry.PluralForms["zero"]);
        Assert.Equal("{0} message", entry.PluralForms["one"]);
        Assert.Equal("{0} messages", entry.PluralForms["other"]);
    }

    #endregion

    #region i18next Mode Tests

    [Fact]
    public void Write_I18nextMode_PluralEntry_WritesFlatSuffixFormat()
    {
        // Arrange - i18next mode should write plurals as flat keys with suffixes
        var config = new JsonFormatConfiguration { I18nextCompatible = true, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "i18next_plural.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "en",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "items",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "{{count}} item",
                        ["other"] = "{{count}} items"
                    }
                }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - i18next format: flat keys with suffixes (items_one, items_other)
        Assert.True(doc.RootElement.TryGetProperty("items_one", out var one));
        Assert.Equal("{{count}} item", one.GetString());
        Assert.True(doc.RootElement.TryGetProperty("items_other", out var other));
        Assert.Equal("{{count}} items", other.GetString());

        // Should NOT have nested object format
        Assert.False(doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public void Write_I18nextMode_PluralEntry_RoundTrip()
    {
        // Arrange - i18next mode read/write roundtrip
        var config = new JsonFormatConfiguration { I18nextCompatible = true, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var reader = new JsonResourceReader(config);
        var filePath = Path.Combine(_tempDirectory, "i18next_roundtrip.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "en",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "messages",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["zero"] = "No messages",
                        ["one"] = "{{count}} message",
                        ["other"] = "{{count}} messages"
                    }
                }
            }
        };

        // Act
        writer.Write(resourceFile);
        var readBack = reader.Read(resourceFile.Language);

        // Assert - should read back as a single consolidated plural entry
        var entry = readBack.Entries.First(e => e.Key == "messages");
        Assert.True(entry.IsPlural);
        Assert.NotNull(entry.PluralForms);
        Assert.Equal(3, entry.PluralForms.Count);
        Assert.Equal("No messages", entry.PluralForms["zero"]);
        Assert.Equal("{{count}} message", entry.PluralForms["one"]);
        Assert.Equal("{{count}} messages", entry.PluralForms["other"]);
    }

    [Fact]
    public void Write_I18nextMode_MixedEntries_WritesCorrectFormat()
    {
        // Arrange - mix of singular and plural entries
        var config = new JsonFormatConfiguration { I18nextCompatible = true, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "i18next_mixed.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "en",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "title", Value = "Welcome" },
                new()
                {
                    Key = "items",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "{{count}} item",
                        ["other"] = "{{count}} items"
                    }
                },
                new() { Key = "footer", Value = "Copyright 2025" }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - singular entries as strings, plural as flat suffix keys
        Assert.True(doc.RootElement.TryGetProperty("title", out var title));
        Assert.Equal(JsonValueKind.String, title.ValueKind);
        Assert.Equal("Welcome", title.GetString());

        Assert.True(doc.RootElement.TryGetProperty("items_one", out var itemsOne));
        Assert.Equal("{{count}} item", itemsOne.GetString());

        Assert.True(doc.RootElement.TryGetProperty("items_other", out var itemsOther));
        Assert.Equal("{{count}} items", itemsOther.GetString());

        Assert.True(doc.RootElement.TryGetProperty("footer", out var footer));
        Assert.Equal("Copyright 2025", footer.GetString());
    }

    [Fact]
    public void Write_NonI18nextMode_PluralEntry_DoesNotWriteFlatFormat()
    {
        // Arrange - standard mode should NOT write flat suffix format
        var config = new JsonFormatConfiguration { I18nextCompatible = false, IncludeMeta = false };
        var writer = new JsonResourceWriter(config);
        var filePath = Path.Combine(_tempDirectory, "standard_plural.json");
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "standard",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new()
                {
                    Key = "items",
                    Value = "",
                    IsPlural = true,
                    PluralForms = new Dictionary<string, string>
                    {
                        ["one"] = "{0} item",
                        ["other"] = "{0} items"
                    }
                }
            }
        };

        // Act
        writer.Write(resourceFile);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        // Assert - should NOT have flat suffix keys
        Assert.False(doc.RootElement.TryGetProperty("items_one", out _));
        Assert.False(doc.RootElement.TryGetProperty("items_other", out _));

        // Should have nested CLDR structure
        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Object, items.ValueKind);
        Assert.True(items.TryGetProperty("_plural", out _));
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task WriteAsync_ValidResourceFile_CreatesFile()
    {
        // Arrange
        var writer = new JsonResourceWriter();
        var filePath = Path.Combine(_tempDirectory, "async.json");
        var resourceFile = CreateTestResourceFile(filePath);

        // Act
        await writer.WriteAsync(resourceFile);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    #endregion

    #region Helper Methods

    private ResourceFile CreateTestResourceFile(string filePath)
    {
        return new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(filePath),
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "TestKey", Value = "Test Value", Comment = "Test Comment" }
            }
        };
    }

    #endregion
}
