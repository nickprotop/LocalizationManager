// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.Core;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Output;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class OutputFormatTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();
    private readonly ResxResourceDiscovery _discovery = new();
    private readonly ResourceValidator _validator;

    public OutputFormatTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Using _reader and _writer initialized above
        // Using _discovery initialized above
        _validator = new ResourceValidator();

        // Create initial test resource files
        CreateTestResourceFiles();
    }

    private void CreateTestResourceFiles()
    {
        // Create default resource file
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "Default",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Value1", Comment = "Comment 1" },
                new() { Key = "Key2", Value = "Value2" },
                new() { Key = "EmptyKey", Value = "", Comment = "Has empty value" }
            }
        };

        // Create Greek resource file with a missing key
        var greekFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "el",
                Name = "Ελληνικά (el)",
                IsDefault = false,
                FilePath = Path.Combine(_testDirectory, "TestResource.el.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Key1", Value = "Τιμή1" },
                new() { Key = "Key2", Value = "Τιμή2" },
                // Missing EmptyKey - will trigger validation error
                new() { Key = "ExtraKey", Value = "Extra" } // Extra key not in default
            }
        };

        _writer.Write(defaultFile);
        _writer.Write(greekFile);
    }

    [Fact]
    public void OutputFormatter_FormatJson_ReturnsValidJson()
    {
        // Arrange
        var data = new
        {
            TestString = "value",
            TestNumber = 42,
            TestBool = true,
            TestArray = new[] { 1, 2, 3 }
        };

        // Act
        var json = OutputFormatter.FormatJson(data);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(json);
        Assert.NotNull(jsonDoc);

        // Verify properties
        var root = jsonDoc.RootElement;
        Assert.Equal("value", root.GetProperty("testString").GetString());
        Assert.Equal(42, root.GetProperty("testNumber").GetInt32());
        Assert.True(root.GetProperty("testBool").GetBoolean());
        Assert.Equal(3, root.GetProperty("testArray").GetArrayLength());
    }

    [Fact]
    public void OutputFormatter_ParseFormat_Table_ReturnsTableFormat()
    {
        // Act
        var format = OutputFormatter.ParseFormat("table");

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Table, format);
    }

    [Fact]
    public void OutputFormatter_ParseFormat_Json_ReturnsJsonFormat()
    {
        // Act
        var format = OutputFormatter.ParseFormat("json");

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Json, format);
    }

    [Fact]
    public void OutputFormatter_ParseFormat_Simple_ReturnsSimpleFormat()
    {
        // Act
        var format = OutputFormatter.ParseFormat("simple");

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Simple, format);
    }

    [Fact]
    public void OutputFormatter_ParseFormat_CaseInsensitive_ReturnsCorrectFormat()
    {
        // Act
        var formatUpper = OutputFormatter.ParseFormat("JSON");
        var formatMixed = OutputFormatter.ParseFormat("TaBlE");
        var formatLower = OutputFormatter.ParseFormat("simple");

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Json, formatUpper);
        Assert.Equal(Core.Enums.OutputFormat.Table, formatMixed);
        Assert.Equal(Core.Enums.OutputFormat.Simple, formatLower);
    }

    [Fact]
    public void OutputFormatter_ParseFormat_Invalid_ReturnsDefault()
    {
        // Act
        var format = OutputFormatter.ParseFormat("invalid");

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Table, format);
    }

    [Fact]
    public void OutputFormatter_ParseFormat_Null_ReturnsDefault()
    {
        // Act
        var format = OutputFormatter.ParseFormat(null!);

        // Assert
        Assert.Equal(Core.Enums.OutputFormat.Table, format);
    }

    [Fact]
    public void ValidationResult_SerializesToJson_ContainsAllProperties()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = new List<ResourceFile>();
        foreach (var lang in languages)
        {
            resourceFiles.Add(_reader.Read(lang));
        }

        // Act
        var validationResult = _validator.Validate(resourceFiles);

        var output = new
        {
            isValid = validationResult.IsValid,
            totalIssues = validationResult.TotalIssues,
            missingKeys = validationResult.MissingKeys,
            extraKeys = validationResult.ExtraKeys,
            duplicateKeys = validationResult.DuplicateKeys,
            emptyValues = validationResult.EmptyValues
        };

        var json = OutputFormatter.FormatJson(output);

        // Assert
        Assert.NotNull(json);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("isValid", out var isValid));
        Assert.False(isValid.GetBoolean()); // Should be false due to validation errors

        Assert.True(root.TryGetProperty("totalIssues", out var totalIssues));
        Assert.True(totalIssues.GetInt32() > 0);

        Assert.True(root.TryGetProperty("missingKeys", out _));
        Assert.True(root.TryGetProperty("extraKeys", out _));
        Assert.True(root.TryGetProperty("duplicateKeys", out _));
        Assert.True(root.TryGetProperty("emptyValues", out _));
    }

    [Fact]
    public void ResourceFile_Statistics_SerializesToJson_WithAllFields()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var resourceFiles = new List<ResourceFile>();
        foreach (var lang in languages)
        {
            resourceFiles.Add(_reader.Read(lang));
        }

        // Act
        var stats = resourceFiles.Select(rf => new
        {
            language = rf.Language.Name,
            isDefault = rf.Language.IsDefault,
            totalKeys = rf.Count,
            completedKeys = rf.CompletedCount,
            emptyKeys = rf.Count - rf.CompletedCount,
            coveragePercentage = rf.CompletionPercentage,
            filePath = rf.Language.FilePath,
            fileSizeBytes = new FileInfo(rf.Language.FilePath).Length
        }).ToList();

        var output = new
        {
            totalLanguages = resourceFiles.Count,
            statistics = stats
        };

        var json = OutputFormatter.FormatJson(output);

        // Assert
        Assert.NotNull(json);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("totalLanguages", out var totalLanguages));
        Assert.Equal(2, totalLanguages.GetInt32());

        Assert.True(root.TryGetProperty("statistics", out var statistics));
        Assert.Equal(JsonValueKind.Array, statistics.ValueKind);
        Assert.Equal(2, statistics.GetArrayLength());

        // Check first stat has all expected properties
        var firstStat = statistics[0];
        Assert.True(firstStat.TryGetProperty("language", out _));
        Assert.True(firstStat.TryGetProperty("isDefault", out _));
        Assert.True(firstStat.TryGetProperty("totalKeys", out _));
        Assert.True(firstStat.TryGetProperty("completedKeys", out _));
        Assert.True(firstStat.TryGetProperty("emptyKeys", out _));
        Assert.True(firstStat.TryGetProperty("coveragePercentage", out _));
        Assert.True(firstStat.TryGetProperty("filePath", out _));
        Assert.True(firstStat.TryGetProperty("fileSizeBytes", out _));
    }

    [Fact]
    public void JsonSerializedOutput_HandlesSpecialCharacters()
    {
        // Arrange
        var data = new
        {
            greekText = "Ελληνικά",
            quotes = "Test \"quotes\"",
            newlines = "Line1\nLine2",
            specialChars = "Special: <>&'\""
        };

        // Act
        var json = OutputFormatter.FormatJson(data);

        // Assert
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        Assert.Equal("Ελληνικά", root.GetProperty("greekText").GetString());
        Assert.Equal("Test \"quotes\"", root.GetProperty("quotes").GetString());
        Assert.Equal("Line1\nLine2", root.GetProperty("newlines").GetString());
        Assert.Equal("Special: <>&'\"", root.GetProperty("specialChars").GetString());
    }

    [Fact]
    public void JsonSerializedOutput_ProperlyFormatsIndentation()
    {
        // Arrange
        var data = new
        {
            level1 = new
            {
                level2 = new
                {
                    value = "test"
                }
            }
        };

        // Act
        var json = OutputFormatter.FormatJson(data);

        // Assert
        Assert.Contains("  ", json); // Should have indentation
        Assert.Contains("{\n", json); // Should have newlines
        var lines = json.Split('\n');
        Assert.True(lines.Length > 3); // Multi-line output
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
