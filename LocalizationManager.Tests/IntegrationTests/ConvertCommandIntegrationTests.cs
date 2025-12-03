// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using System.Text.Json;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the convert command.
/// </summary>
public class ConvertCommandIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _sourceResxDir;
    private readonly string _sourceJsonDir;
    private readonly string _outputDir;

    public ConvertCommandIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ConvertTests_{Guid.NewGuid()}");
        _sourceResxDir = Path.Combine(_tempDirectory, "source_resx");
        _sourceJsonDir = Path.Combine(_tempDirectory, "source_json");
        _outputDir = Path.Combine(_tempDirectory, "output");

        Directory.CreateDirectory(_sourceResxDir);
        Directory.CreateDirectory(_sourceJsonDir);
        Directory.CreateDirectory(_outputDir);

        // Create source files
        CreateSourceResxFiles();
        CreateSourceJsonFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private void CreateSourceResxFiles()
    {
        var backend = new ResxResourceBackend();
        var entries = new List<ResourceEntry>
        {
            new() { Key = "Save", Value = "Save", Comment = "Save button" },
            new() { Key = "Cancel", Value = "Cancel", Comment = "Cancel button" },
            new() { Key = "Errors.NotFound", Value = "Not found" },
            new() { Key = "Errors.AccessDenied", Value = "Access denied" }
        };

        var languages = new[] { "", "el", "fr" };
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "Resources.resx" : $"Resources.{lang}.resx";
            var file = new ResourceFile
            {
                Language = new LanguageInfo
                {
                    BaseName = "Resources",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(_sourceResxDir, fileName)
                },
                Entries = entries.Select(e => new ResourceEntry
                {
                    Key = e.Key,
                    Value = string.IsNullOrEmpty(lang) ? e.Value : $"{e.Value}_{lang}",
                    Comment = e.Comment
                }).ToList()
            };
            backend.Writer.Write(file);
        }
    }

    private void CreateSourceJsonFiles()
    {
        var config = new JsonFormatConfiguration { UseNestedKeys = true, IncludeMeta = true, PreserveComments = true };
        var backend = new JsonResourceBackend(config);
        var entries = new List<ResourceEntry>
        {
            new() { Key = "Save", Value = "Save", Comment = "Save button" },
            new() { Key = "Cancel", Value = "Cancel", Comment = "Cancel button" },
            new() { Key = "Errors.NotFound", Value = "Not found" },
            new() { Key = "Errors.AccessDenied", Value = "Access denied" }
        };

        var languages = new[] { "", "el", "fr" };
        foreach (var lang in languages)
        {
            var fileName = string.IsNullOrEmpty(lang) ? "strings.json" : $"strings.{lang}.json";
            var file = new ResourceFile
            {
                Language = new LanguageInfo
                {
                    BaseName = "strings",
                    Code = lang,
                    Name = string.IsNullOrEmpty(lang) ? "Default" : lang,
                    IsDefault = string.IsNullOrEmpty(lang),
                    FilePath = Path.Combine(_sourceJsonDir, fileName)
                },
                Entries = entries.Select(e => new ResourceEntry
                {
                    Key = e.Key,
                    Value = string.IsNullOrEmpty(lang) ? e.Value : $"{e.Value}_{lang}",
                    Comment = e.Comment
                }).ToList()
            };
            backend.Writer.Write(file);
        }
    }

    #region RESX to JSON Conversion Tests

    [Fact]
    public void Convert_ResxToJson_ConvertsAllFiles()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonBackend = new JsonResourceBackend();
        var outputDir = Path.Combine(_outputDir, "resx_to_json");
        Directory.CreateDirectory(outputDir);

        // Act - simulate convert command
        var languages = resxBackend.Discovery.DiscoverLanguages(_sourceResxDir);

        foreach (var lang in languages)
        {
            var resourceFile = resxBackend.Reader.Read(lang);

            // Update file path for JSON output
            var jsonFileName = lang.IsDefault
                ? $"{lang.BaseName}.json"
                : $"{lang.BaseName}.{lang.Code}.json";
            resourceFile.Language = new LanguageInfo
            {
                BaseName = lang.BaseName,
                Code = lang.Code,
                Name = lang.Name,
                IsDefault = lang.IsDefault,
                FilePath = Path.Combine(outputDir, jsonFileName)
            };

            jsonBackend.Writer.Write(resourceFile);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(outputDir, "Resources.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "Resources.el.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "Resources.fr.json")));

        // Verify JSON discovery finds all files
        var jsonLanguages = jsonBackend.Discovery.DiscoverLanguages(outputDir);
        Assert.Equal(3, jsonLanguages.Count);
    }

    [Fact]
    public void Convert_ResxToJson_PreservesEntries()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonConfig = new JsonFormatConfiguration { UseNestedKeys = false, PreserveComments = true };
        var jsonBackend = new JsonResourceBackend(jsonConfig);
        var outputDir = Path.Combine(_outputDir, "resx_to_json_entries");
        Directory.CreateDirectory(outputDir);

        // Act
        var languages = resxBackend.Discovery.DiscoverLanguages(_sourceResxDir);
        var defaultLang = languages.First(l => l.IsDefault);
        var resourceFile = resxBackend.Reader.Read(defaultLang);

        var jsonFilePath = Path.Combine(outputDir, "Resources.json");
        resourceFile.Language = new LanguageInfo
        {
            BaseName = defaultLang.BaseName,
            Code = defaultLang.Code,
            Name = defaultLang.Name,
            IsDefault = defaultLang.IsDefault,
            FilePath = jsonFilePath
        };
        jsonBackend.Writer.Write(resourceFile);

        // Read back
        var jsonLangInfo = new LanguageInfo
        {
            BaseName = "Resources",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = jsonFilePath
        };
        var jsonReader = new JsonResourceReader(jsonConfig);
        var jsonFile = jsonReader.Read(jsonLangInfo);

        // Assert
        Assert.Equal(resourceFile.Entries.Count, jsonFile.Entries.Count);
        Assert.Equal("Save", jsonFile.Entries.First(e => e.Key == "Save").Value);
        Assert.Equal("Save button", jsonFile.Entries.First(e => e.Key == "Save").Comment);
    }

    [Fact]
    public void Convert_ResxToJson_WithNestedKeys_CreatesNestedStructure()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonConfig = new JsonFormatConfiguration { UseNestedKeys = true, IncludeMeta = false, PreserveComments = false };
        var jsonBackend = new JsonResourceBackend(jsonConfig);
        var outputDir = Path.Combine(_outputDir, "resx_to_json_nested");
        Directory.CreateDirectory(outputDir);

        // Act
        var languages = resxBackend.Discovery.DiscoverLanguages(_sourceResxDir);
        var defaultLang = languages.First(l => l.IsDefault);
        var resourceFile = resxBackend.Reader.Read(defaultLang);

        var jsonFilePath = Path.Combine(outputDir, "Resources.json");
        resourceFile.Language = new LanguageInfo
        {
            BaseName = defaultLang.BaseName,
            Code = defaultLang.Code,
            Name = defaultLang.Name,
            IsDefault = defaultLang.IsDefault,
            FilePath = jsonFilePath
        };
        jsonBackend.Writer.Write(resourceFile);

        // Assert - verify nested structure
        var json = File.ReadAllText(jsonFilePath);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("Errors", out var errors));
        Assert.True(errors.TryGetProperty("NotFound", out _));
    }

    #endregion

    #region JSON to RESX Conversion Tests

    [Fact]
    public void Convert_JsonToResx_ConvertsAllFiles()
    {
        // Arrange
        var jsonBackend = new JsonResourceBackend();
        var resxBackend = new ResxResourceBackend();
        var outputDir = Path.Combine(_outputDir, "json_to_resx");
        Directory.CreateDirectory(outputDir);

        // Act - simulate convert command
        var languages = jsonBackend.Discovery.DiscoverLanguages(_sourceJsonDir);

        foreach (var lang in languages)
        {
            var resourceFile = jsonBackend.Reader.Read(lang);

            // Update file path for RESX output
            var resxFileName = lang.IsDefault
                ? $"{lang.BaseName}.resx"
                : $"{lang.BaseName}.{lang.Code}.resx";
            resourceFile.Language = new LanguageInfo
            {
                BaseName = lang.BaseName,
                Code = lang.Code,
                Name = lang.Name,
                IsDefault = lang.IsDefault,
                FilePath = Path.Combine(outputDir, resxFileName)
            };

            resxBackend.Writer.Write(resourceFile);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(outputDir, "strings.resx")));
        Assert.True(File.Exists(Path.Combine(outputDir, "strings.el.resx")));
        Assert.True(File.Exists(Path.Combine(outputDir, "strings.fr.resx")));

        // Verify RESX discovery finds all files
        var resxLanguages = resxBackend.Discovery.DiscoverLanguages(outputDir);
        Assert.Equal(3, resxLanguages.Count);
    }

    [Fact]
    public void Convert_JsonToResx_FlattensNestedKeys()
    {
        // Arrange
        var jsonBackend = new JsonResourceBackend();
        var resxBackend = new ResxResourceBackend();
        var outputDir = Path.Combine(_outputDir, "json_to_resx_flatten");
        Directory.CreateDirectory(outputDir);

        // Act
        var languages = jsonBackend.Discovery.DiscoverLanguages(_sourceJsonDir);
        var defaultLang = languages.First(l => l.IsDefault);
        var resourceFile = jsonBackend.Reader.Read(defaultLang);

        var resxFilePath = Path.Combine(outputDir, "strings.resx");
        resourceFile.Language = new LanguageInfo
        {
            BaseName = defaultLang.BaseName,
            Code = defaultLang.Code,
            Name = defaultLang.Name,
            IsDefault = defaultLang.IsDefault,
            FilePath = resxFilePath
        };
        resxBackend.Writer.Write(resourceFile);

        // Read back
        var resxLangInfo = new LanguageInfo
        {
            BaseName = "strings",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = resxFilePath
        };
        var resxFile = resxBackend.Reader.Read(resxLangInfo);

        // Assert - nested keys should be flattened with dots
        Assert.NotNull(resxFile.Entries.FirstOrDefault(e => e.Key == "Errors.NotFound"));
        Assert.NotNull(resxFile.Entries.FirstOrDefault(e => e.Key == "Errors.AccessDenied"));
    }

    [Fact]
    public void Convert_JsonToResx_PreservesComments()
    {
        // Arrange
        var jsonConfig = new JsonFormatConfiguration { PreserveComments = true };
        var jsonBackend = new JsonResourceBackend(jsonConfig);
        var resxBackend = new ResxResourceBackend();
        var outputDir = Path.Combine(_outputDir, "json_to_resx_comments");
        Directory.CreateDirectory(outputDir);

        // Act
        var languages = jsonBackend.Discovery.DiscoverLanguages(_sourceJsonDir);
        var defaultLang = languages.First(l => l.IsDefault);
        var resourceFile = jsonBackend.Reader.Read(defaultLang);

        var resxFilePath = Path.Combine(outputDir, "strings.resx");
        resourceFile.Language = new LanguageInfo
        {
            BaseName = defaultLang.BaseName,
            Code = defaultLang.Code,
            Name = defaultLang.Name,
            IsDefault = defaultLang.IsDefault,
            FilePath = resxFilePath
        };
        resxBackend.Writer.Write(resourceFile);

        // Read back
        var resxLangInfo = new LanguageInfo
        {
            BaseName = "strings",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = resxFilePath
        };
        var resxFile = resxBackend.Reader.Read(resxLangInfo);

        // Assert - comments should be preserved
        var saveEntry = resxFile.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.NotNull(saveEntry);
        Assert.Equal("Save button", saveEntry.Comment);
    }

    #endregion

    #region Round-Trip Conversion Tests

    [Fact]
    public void Convert_RoundTrip_ResxToJsonToResx_PreservesData()
    {
        // Arrange
        var resxBackend = new ResxResourceBackend();
        var jsonConfig = new JsonFormatConfiguration { UseNestedKeys = false, PreserveComments = true, IncludeMeta = false };
        var jsonBackend = new JsonResourceBackend(jsonConfig);

        var jsonOutputDir = Path.Combine(_outputDir, "roundtrip_json");
        var resxOutputDir = Path.Combine(_outputDir, "roundtrip_resx");
        Directory.CreateDirectory(jsonOutputDir);
        Directory.CreateDirectory(resxOutputDir);

        // Act - RESX -> JSON
        var originalLanguages = resxBackend.Discovery.DiscoverLanguages(_sourceResxDir);
        var originalDefault = originalLanguages.First(l => l.IsDefault);
        var originalFile = resxBackend.Reader.Read(originalDefault);

        var jsonFilePath = Path.Combine(jsonOutputDir, "Resources.json");
        var jsonLangInfo = new LanguageInfo
        {
            BaseName = originalDefault.BaseName,
            Code = originalDefault.Code,
            Name = originalDefault.Name,
            IsDefault = originalDefault.IsDefault,
            FilePath = jsonFilePath
        };
        originalFile.Language = jsonLangInfo;
        jsonBackend.Writer.Write(originalFile);

        // Act - JSON -> RESX
        var jsonFile = jsonBackend.Reader.Read(jsonLangInfo);
        var resxFilePath = Path.Combine(resxOutputDir, "Resources.resx");
        jsonFile.Language = new LanguageInfo
        {
            BaseName = "Resources",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = resxFilePath
        };
        resxBackend.Writer.Write(jsonFile);

        // Read final result
        var finalFile = resxBackend.Reader.Read(jsonFile.Language);

        // Assert - compare with original
        Assert.Equal(originalFile.Entries.Count, finalFile.Entries.Count);

        foreach (var originalEntry in originalFile.Entries)
        {
            var finalEntry = finalFile.Entries.FirstOrDefault(e => e.Key == originalEntry.Key);
            Assert.NotNull(finalEntry);
            Assert.Equal(originalEntry.Value, finalEntry.Value);
            // Comments should be preserved
            if (!string.IsNullOrEmpty(originalEntry.Comment))
            {
                Assert.Equal(originalEntry.Comment, finalEntry.Comment);
            }
        }
    }

    #endregion

    #region Format Auto-Detection Tests

    [Fact]
    public void Convert_AutoDetect_DetectsResxFormat()
    {
        // Arrange
        var factory = new Core.Backends.ResourceBackendFactory();

        // Act
        var backend = factory.ResolveFromPath(_sourceResxDir);

        // Assert
        Assert.Equal("resx", backend.Name);
    }

    [Fact]
    public void Convert_AutoDetect_DetectsJsonFormat()
    {
        // Arrange
        var factory = new Core.Backends.ResourceBackendFactory();

        // Act
        var backend = factory.ResolveFromPath(_sourceJsonDir);

        // Assert
        Assert.Equal("json", backend.Name);
    }

    #endregion
}
