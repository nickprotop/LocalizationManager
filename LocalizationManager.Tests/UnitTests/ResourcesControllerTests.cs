// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Controllers;
using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

using CoreLanguageInfo = LocalizationManager.Core.Models.LanguageInfo;

namespace LocalizationManager.Tests.UnitTests;

public class ResourcesControllerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;
    private readonly ResourcesController _controller;

    public ResourcesControllerTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmControllerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();

        // Create initial test resource files
        CreateInitialResourceFiles();

        // Create controller with mock configuration
        var configData = new Dictionary<string, string?>
        {
            { "ResourcePath", _testDirectory }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        _controller = new ResourcesController(configuration);
    }

    private void CreateInitialResourceFiles()
    {
        // Create default resource file
        var defaultFile = new ResourceFile
        {
            Language = new CoreLanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "English (Default)",
                IsDefault = true,
                FilePath = Path.Combine(_testDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save", Comment = "Save button" },
                new() { Key = "Cancel", Value = "Cancel", Comment = null },
                new() { Key = "Delete", Value = "Delete", Comment = "Delete action" }
            }
        };

        // Create Greek resource file
        var greekFile = new ResourceFile
        {
            Language = new CoreLanguageInfo
            {
                BaseName = "TestResource",
                Code = "el",
                Name = "Greek",
                IsDefault = false,
                FilePath = Path.Combine(_testDirectory, "TestResource.el.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Αποθήκευση", Comment = "Greek save" },
                new() { Key = "Cancel", Value = "Ακύρωση", Comment = null },
                new() { Key = "Delete", Value = "Διαγραφή", Comment = null }
            }
        };

        _parser.Write(defaultFile);
        _parser.Write(greekFile);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void UpdateKey_WithPerLanguageComments_UpdatesBothValueAndComment()
    {
        // Arrange
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "default", new ResourceValue { Value = "Save Changes", Comment = "Updated default comment" } },
                { "el", new ResourceValue { Value = "Αποθήκευση Αλλαγών", Comment = "Updated Greek comment" } }
            }
        };

        // Act
        var result = _controller.UpdateKey("Save", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OperationResponse>(okResult.Value);
        Assert.True(response.Success);

        // Verify values and comments were updated
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        foreach (var lang in languages)
        {
            var file = _parser.Parse(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == "Save");

            Assert.NotNull(entry);

            if (lang.IsDefault)
            {
                Assert.Equal("Save Changes", entry.Value);
                Assert.Equal("Updated default comment", entry.Comment);
            }
            else if (lang.Code == "el")
            {
                Assert.Equal("Αποθήκευση Αλλαγών", entry.Value);
                Assert.Equal("Updated Greek comment", entry.Comment);
            }
        }
    }

    [Fact]
    public void UpdateKey_WithGlobalCommentFallback_AppliesGlobalCommentWhenPerLanguageNotProvided()
    {
        // Arrange
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "default", new ResourceValue { Value = "Cancel Action" } },  // No per-language comment
                { "el", new ResourceValue { Value = "Ακύρωση Ενέργειας" } }    // No per-language comment
            },
            Comment = "Global fallback comment"  // Global comment
        };

        // Act
        var result = _controller.UpdateKey("Cancel", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OperationResponse>(okResult.Value);
        Assert.True(response.Success);

        // Verify global comment was applied to all languages
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        foreach (var lang in languages)
        {
            var file = _parser.Parse(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == "Cancel");

            Assert.NotNull(entry);
            Assert.Equal("Global fallback comment", entry.Comment);
        }
    }

    [Fact]
    public void UpdateKey_PerLanguageCommentTakesPriorityOverGlobal()
    {
        // Arrange
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "default", new ResourceValue { Value = "Delete Item", Comment = "Specific default comment" } },
                { "el", new ResourceValue { Value = "Διαγραφή Στοιχείου" } }  // No per-language comment for Greek
            },
            Comment = "Global comment"  // Global fallback
        };

        // Act
        var result = _controller.UpdateKey("Delete", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OperationResponse>(okResult.Value);
        Assert.True(response.Success);

        // Verify per-language comment takes priority for default, global applied to Greek
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        foreach (var lang in languages)
        {
            var file = _parser.Parse(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == "Delete");

            Assert.NotNull(entry);

            if (lang.IsDefault)
            {
                Assert.Equal("Delete Item", entry.Value);
                Assert.Equal("Specific default comment", entry.Comment);  // Per-language takes priority
            }
            else if (lang.Code == "el")
            {
                Assert.Equal("Διαγραφή Στοιχείου", entry.Value);
                Assert.Equal("Global comment", entry.Comment);  // Global fallback applied
            }
        }
    }

    [Fact]
    public void UpdateKey_ValueOnlyUpdate_PreservesExistingComment()
    {
        // Arrange - Only update value, no comment provided
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "default", new ResourceValue { Value = "Save Now" } }  // No comment
            }
        };

        // Act
        var result = _controller.UpdateKey("Save", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OperationResponse>(okResult.Value);
        Assert.True(response.Success);

        // Verify value updated but original comment preserved
        var languages = _discovery.DiscoverLanguages(_testDirectory);
        var defaultLang = languages.First(l => l.IsDefault);
        var file = _parser.Parse(defaultLang);
        var entry = file.Entries.FirstOrDefault(e => e.Key == "Save");

        Assert.NotNull(entry);
        Assert.Equal("Save Now", entry.Value);
        Assert.Equal("Save button", entry.Comment);  // Original comment preserved
    }

    [Fact]
    public void UpdateKey_NonExistentKey_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "default", new ResourceValue { Value = "Value" } }
            }
        };

        // Act
        var result = _controller.UpdateKey("NonExistentKey", request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public void UpdateKey_PartialLanguageUpdate_OnlyUpdatesProvidedLanguages()
    {
        // Arrange - Only update Greek, leave default unchanged
        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                { "el", new ResourceValue { Value = "Νέα Ελληνική Τιμή", Comment = "New Greek comment" } }
            }
        };

        // Act
        var result = _controller.UpdateKey("Save", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);

        var languages = _discovery.DiscoverLanguages(_testDirectory);

        // Default should remain unchanged
        var defaultLang = languages.First(l => l.IsDefault);
        var defaultFile = _parser.Parse(defaultLang);
        var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.Equal("Save", defaultEntry?.Value);  // Original value
        Assert.Equal("Save button", defaultEntry?.Comment);  // Original comment

        // Greek should be updated
        var greekLang = languages.First(l => l.Code == "el");
        var greekFile = _parser.Parse(greekLang);
        var greekEntry = greekFile.Entries.FirstOrDefault(e => e.Key == "Save");
        Assert.Equal("Νέα Ελληνική Τιμή", greekEntry?.Value);
        Assert.Equal("New Greek comment", greekEntry?.Comment);
    }
}
