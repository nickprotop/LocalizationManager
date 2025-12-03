// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests verifying parity between RESX and JSON backends.
/// All operations should produce equivalent results regardless of format.
/// </summary>
public class MultiBackendIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _resxDirectory;
    private readonly string _jsonDirectory;

    public MultiBackendIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"MultiBackendTests_{Guid.NewGuid()}");
        _resxDirectory = Path.Combine(_tempDirectory, "resx");
        _jsonDirectory = Path.Combine(_tempDirectory, "json");
        Directory.CreateDirectory(_resxDirectory);
        Directory.CreateDirectory(_jsonDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Backend Factory Tests

    [Fact]
    public void ResourceBackendFactory_GetBackend_ReturnsCorrectBackend()
    {
        // Arrange
        var factory = new ResourceBackendFactory();

        // Act
        var resxBackend = factory.GetBackend("resx");
        var jsonBackend = factory.GetBackend("json");

        // Assert
        Assert.IsType<ResxResourceBackend>(resxBackend);
        Assert.IsType<JsonResourceBackend>(jsonBackend);
    }

    [Fact]
    public void ResourceBackendFactory_ResolveFromPath_DetectsResx()
    {
        // Arrange
        var factory = new ResourceBackendFactory();
        CreateResxFile(Path.Combine(_resxDirectory, "Test.resx"), "Key1", "Value1");

        // Act
        var backend = factory.ResolveFromPath(_resxDirectory);

        // Assert
        Assert.Equal("resx", backend.Name);
    }

    [Fact]
    public void ResourceBackendFactory_ResolveFromPath_DetectsJson()
    {
        // Arrange
        var factory = new ResourceBackendFactory();
        CreateJsonFile(Path.Combine(_jsonDirectory, "strings.json"), "Key1", "Value1");

        // Act
        var backend = factory.ResolveFromPath(_jsonDirectory);

        // Assert
        Assert.Equal("json", backend.Name);
    }

    #endregion

    #region Read/Write Parity Tests

    [Fact]
    public void ReadWrite_BothFormats_ProduceSameEntries()
    {
        // Arrange
        var entries = new List<ResourceEntry>
        {
            new() { Key = "Save", Value = "Save", Comment = "Save button" },
            new() { Key = "Cancel", Value = "Cancel", Comment = "Cancel button" },
            new() { Key = "OK", Value = "OK" }
        };

        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration { UseNestedKeys = false });

        var resxFile = Path.Combine(_resxDirectory, "Test.resx");
        var jsonFile = Path.Combine(_jsonDirectory, "Test.json");

        var resxResourceFile = CreateResourceFile(resxFile, entries, "resx");
        var jsonResourceFile = CreateResourceFile(jsonFile, entries, "json");

        // Act
        resxBackend.Writer.Write(resxResourceFile);
        jsonBackend.Writer.Write(jsonResourceFile);

        var resxRead = resxBackend.Reader.Read(resxResourceFile.Language);
        var jsonRead = jsonBackend.Reader.Read(jsonResourceFile.Language);

        // Assert - same number of entries
        Assert.Equal(resxRead.Entries.Count, jsonRead.Entries.Count);

        // Assert - same keys and values
        foreach (var entry in entries)
        {
            var resxEntry = resxRead.Entries.FirstOrDefault(e => e.Key == entry.Key);
            var jsonEntry = jsonRead.Entries.FirstOrDefault(e => e.Key == entry.Key);

            Assert.NotNull(resxEntry);
            Assert.NotNull(jsonEntry);
            Assert.Equal(resxEntry.Value, jsonEntry.Value);
        }
    }

    [Fact]
    public void Discovery_BothFormats_FindSameLanguages()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();

        // Create RESX files
        CreateResxFile(Path.Combine(_resxDirectory, "Test.resx"), "Key1", "Value1");
        CreateResxFile(Path.Combine(_resxDirectory, "Test.el.resx"), "Key1", "Τιμή1");
        CreateResxFile(Path.Combine(_resxDirectory, "Test.fr.resx"), "Key1", "Valeur1");

        // Create JSON files
        CreateJsonFile(Path.Combine(_jsonDirectory, "Test.json"), "Key1", "Value1");
        CreateJsonFile(Path.Combine(_jsonDirectory, "Test.el.json"), "Key1", "Τιμή1");
        CreateJsonFile(Path.Combine(_jsonDirectory, "Test.fr.json"), "Key1", "Valeur1");

        // Act
        var resxLanguages = resxBackend.Discovery.DiscoverLanguages(_resxDirectory);
        var jsonLanguages = jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        // Assert - same number of languages
        Assert.Equal(resxLanguages.Count, jsonLanguages.Count);

        // Assert - same language codes
        var resxCodes = resxLanguages.Select(l => l.Code).OrderBy(c => c).ToList();
        var jsonCodes = jsonLanguages.Select(l => l.Code).OrderBy(c => c).ToList();
        Assert.Equal(resxCodes, jsonCodes);

        // Assert - both identify default correctly
        Assert.Single(resxLanguages, l => l.IsDefault);
        Assert.Single(jsonLanguages, l => l.IsDefault);
    }

    #endregion

    #region CRUD Operations Parity Tests

    [Fact]
    public void AddKey_BothFormats_AddsKeySuccessfully()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration { UseNestedKeys = false, PreserveComments = false, IncludeMeta = false });

        var resxFile = Path.Combine(_resxDirectory, "Add.resx");
        var jsonFile = Path.Combine(_jsonDirectory, "Add.json");

        var resxResourceFile = CreateResourceFile(resxFile, new List<ResourceEntry>
        {
            new() { Key = "Existing", Value = "Existing Value" }
        }, "resx");
        var jsonResourceFile = CreateResourceFile(jsonFile, new List<ResourceEntry>
        {
            new() { Key = "Existing", Value = "Existing Value" }
        }, "json");

        resxBackend.Writer.Write(resxResourceFile);
        jsonBackend.Writer.Write(jsonResourceFile);

        // Act - add new key to both
        var resxRead = resxBackend.Reader.Read(resxResourceFile.Language);
        resxRead.Entries.Add(new ResourceEntry { Key = "NewKey", Value = "New Value" });
        resxBackend.Writer.Write(resxRead);

        var jsonRead = jsonBackend.Reader.Read(jsonResourceFile.Language);
        jsonRead.Entries.Add(new ResourceEntry { Key = "NewKey", Value = "New Value" });
        jsonBackend.Writer.Write(jsonRead);

        // Assert
        var resxResult = resxBackend.Reader.Read(resxResourceFile.Language);
        var jsonResult = jsonBackend.Reader.Read(jsonResourceFile.Language);

        Assert.Equal(2, resxResult.Entries.Count);
        Assert.Equal(2, jsonResult.Entries.Count);
        Assert.NotNull(resxResult.Entries.FirstOrDefault(e => e.Key == "NewKey"));
        Assert.NotNull(jsonResult.Entries.FirstOrDefault(e => e.Key == "NewKey"));
    }

    [Fact]
    public void UpdateKey_BothFormats_UpdatesKeySuccessfully()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration { UseNestedKeys = false, PreserveComments = false, IncludeMeta = false });

        var resxFile = Path.Combine(_resxDirectory, "Update.resx");
        var jsonFile = Path.Combine(_jsonDirectory, "Update.json");

        var resxResourceFile = CreateResourceFile(resxFile, new List<ResourceEntry>
        {
            new() { Key = "ToUpdate", Value = "Original" }
        }, "resx");
        var jsonResourceFile = CreateResourceFile(jsonFile, new List<ResourceEntry>
        {
            new() { Key = "ToUpdate", Value = "Original" }
        }, "json");

        resxBackend.Writer.Write(resxResourceFile);
        jsonBackend.Writer.Write(jsonResourceFile);

        // Act - update key in both
        var resxRead = resxBackend.Reader.Read(resxResourceFile.Language);
        resxRead.Entries.First(e => e.Key == "ToUpdate").Value = "Updated";
        resxBackend.Writer.Write(resxRead);

        var jsonRead = jsonBackend.Reader.Read(jsonResourceFile.Language);
        jsonRead.Entries.First(e => e.Key == "ToUpdate").Value = "Updated";
        jsonBackend.Writer.Write(jsonRead);

        // Assert
        var resxResult = resxBackend.Reader.Read(resxResourceFile.Language);
        var jsonResult = jsonBackend.Reader.Read(jsonResourceFile.Language);

        Assert.Equal("Updated", resxResult.Entries.First(e => e.Key == "ToUpdate").Value);
        Assert.Equal("Updated", jsonResult.Entries.First(e => e.Key == "ToUpdate").Value);
    }

    [Fact]
    public void DeleteKey_BothFormats_DeletesKeySuccessfully()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration { UseNestedKeys = false, PreserveComments = false, IncludeMeta = false });

        var resxFile = Path.Combine(_resxDirectory, "Delete.resx");
        var jsonFile = Path.Combine(_jsonDirectory, "Delete.json");

        var resxResourceFile = CreateResourceFile(resxFile, new List<ResourceEntry>
        {
            new() { Key = "Keep", Value = "Keep" },
            new() { Key = "Delete", Value = "Delete" }
        }, "resx");
        var jsonResourceFile = CreateResourceFile(jsonFile, new List<ResourceEntry>
        {
            new() { Key = "Keep", Value = "Keep" },
            new() { Key = "Delete", Value = "Delete" }
        }, "json");

        resxBackend.Writer.Write(resxResourceFile);
        jsonBackend.Writer.Write(jsonResourceFile);

        // Act - delete key from both
        var resxRead = resxBackend.Reader.Read(resxResourceFile.Language);
        resxRead.Entries.RemoveAll(e => e.Key == "Delete");
        resxBackend.Writer.Write(resxRead);

        var jsonRead = jsonBackend.Reader.Read(jsonResourceFile.Language);
        jsonRead.Entries.RemoveAll(e => e.Key == "Delete");
        jsonBackend.Writer.Write(jsonRead);

        // Assert
        var resxResult = resxBackend.Reader.Read(resxResourceFile.Language);
        var jsonResult = jsonBackend.Reader.Read(jsonResourceFile.Language);

        Assert.Single(resxResult.Entries);
        Assert.Single(jsonResult.Entries);
        Assert.Null(resxResult.Entries.FirstOrDefault(e => e.Key == "Delete"));
        Assert.Null(jsonResult.Entries.FirstOrDefault(e => e.Key == "Delete"));
    }

    #endregion

    #region Multi-Language Parity Tests

    [Fact]
    public void MultiLanguage_BothFormats_HandleAllLanguages()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var languages = new[] { "", "el", "fr" };
        var entries = new List<ResourceEntry>
        {
            new() { Key = "Hello", Value = "Hello" },
            new() { Key = "Goodbye", Value = "Goodbye" }
        };

        // Create files for all languages in both formats
        foreach (var lang in languages)
        {
            var resxFileName = string.IsNullOrEmpty(lang) ? "Test.resx" : $"Test.{lang}.resx";
            var jsonFileName = string.IsNullOrEmpty(lang) ? "Test.json" : $"Test.{lang}.json";

            var resxFile = CreateResourceFile(Path.Combine(_resxDirectory, resxFileName), entries, "resx", lang);
            var jsonFile = CreateResourceFile(Path.Combine(_jsonDirectory, jsonFileName), entries, "json", lang);

            resxBackend.Writer.Write(resxFile);
            jsonBackend.Writer.Write(jsonFile);
        }

        // Act
        var resxLanguages = resxBackend.Discovery.DiscoverLanguages(_resxDirectory);
        var jsonLanguages = jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        // Assert
        Assert.Equal(3, resxLanguages.Count);
        Assert.Equal(3, jsonLanguages.Count);

        // Verify all languages can be read
        foreach (var lang in resxLanguages)
        {
            var resxFile = resxBackend.Reader.Read(lang);
            Assert.NotEmpty(resxFile.Entries);
        }

        foreach (var lang in jsonLanguages)
        {
            var jsonFile = jsonBackend.Reader.Read(lang);
            Assert.NotEmpty(jsonFile.Entries);
        }
    }

    #endregion

    #region Helper Methods

    private void CreateResxFile(string path, string key, string value)
    {
        var backend = new ResxResourceBackend();
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(path).Split('.')[0],
                Code = ExtractCultureCode(path),
                Name = "Test",
                IsDefault = !path.Contains('.', StringComparison.Ordinal) || path.EndsWith(".resx"),
                FilePath = path
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = key, Value = value }
            }
        };
        backend.Writer.Write(resourceFile);
    }

    private void CreateJsonFile(string path, string key, string value)
    {
        var config = new JsonFormatConfiguration { IncludeMeta = false, PreserveComments = false, UseNestedKeys = false };
        var backend = new JsonResourceBackend(config);
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = Path.GetFileNameWithoutExtension(path).Split('.')[0],
                Code = ExtractCultureCode(path),
                Name = "Test",
                IsDefault = !path.Contains('.', StringComparison.Ordinal) || path.EndsWith(".json") && !path.Contains('.', StringComparison.Ordinal),
                FilePath = path
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = key, Value = value }
            }
        };
        backend.Writer.Write(resourceFile);
    }

    private string ExtractCultureCode(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var parts = fileName.Split('.');
        return parts.Length > 1 ? parts[1] : "";
    }

    private ResourceFile CreateResourceFile(string path, List<ResourceEntry> entries, string format, string cultureCode = "")
    {
        var baseName = Path.GetFileNameWithoutExtension(path).Split('.')[0];
        return new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = string.IsNullOrEmpty(cultureCode) ? "Default" : cultureCode,
                IsDefault = string.IsNullOrEmpty(cultureCode),
                FilePath = path
            },
            Entries = entries.Select(e => new ResourceEntry
            {
                Key = e.Key,
                Value = e.Value,
                Comment = e.Comment
            }).ToList()
        };
    }

    #endregion
}
