// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Integration tests for add/update/delete operations across both RESX and JSON backends.
/// </summary>
public class AddCommandIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _resxDirectory;
    private readonly string _jsonDirectory;
    private readonly ResxResourceReader _reader = new();
    private readonly ResxResourceWriter _writer = new();
    private readonly ResxResourceDiscovery _discovery = new();
    private readonly IResourceBackend _jsonBackend;

    public AddCommandIntegrationTests()
    {
        // Create temporary test directories
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LrmTests_{Guid.NewGuid()}");
        _resxDirectory = Path.Combine(_testDirectory, "resx");
        _jsonDirectory = Path.Combine(_testDirectory, "json");
        Directory.CreateDirectory(_resxDirectory);
        Directory.CreateDirectory(_jsonDirectory);

        // Using _reader and _writer initialized above
        // Using _discovery initialized above
        _jsonBackend = new JsonResourceBackend(new JsonFormatConfiguration
        {
            UseNestedKeys = false,
            IncludeMeta = false,
            PreserveComments = false
        });

        // Create initial test resource files
        CreateInitialResourceFiles();
    }

    private void CreateInitialResourceFiles()
    {
        CreateResxFiles();
        CreateJsonFiles();
    }

    private void CreateResxFiles()
    {
        // Create default resource file
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "English (Default)",
                IsDefault = true,
                FilePath = Path.Combine(_resxDirectory, "TestResource.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save", Comment = "Save button" },
                new() { Key = "Cancel", Value = "Cancel" }
            }
        };

        // Create Greek resource file
        var greekFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "el",
                Name = "Ελληνικά (el)",
                IsDefault = false,
                FilePath = Path.Combine(_resxDirectory, "TestResource.el.resx")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Αποθήκευση", Comment = "Save button" },
                new() { Key = "Cancel", Value = "Ακύρωση" }
            }
        };

        _writer.Write(defaultFile);
        _writer.Write(greekFile);
    }

    private void CreateJsonFiles()
    {
        // Create default resource file
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "",
                Name = "English (Default)",
                IsDefault = true,
                FilePath = Path.Combine(_jsonDirectory, "TestResource.json")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save" },
                new() { Key = "Cancel", Value = "Cancel" }
            }
        };

        // Create Greek resource file
        var greekFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = "TestResource",
                Code = "el",
                Name = "Ελληνικά (el)",
                IsDefault = false,
                FilePath = Path.Combine(_jsonDirectory, "TestResource.el.json")
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Αποθήκευση" },
                new() { Key = "Cancel", Value = "Ακύρωση" }
            }
        };

        _jsonBackend.Writer.Write(defaultFile);
        _jsonBackend.Writer.Write(greekFile);
    }

    #region RESX Tests

    [Fact]
    public void AddKey_ToAllLanguages_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var newKey = "Delete";
        var values = new Dictionary<string, string>
        {
            { "", "Delete" },
            { "el", "Διαγραφή" }
        };

        // Act
        var resourceFiles = new List<ResourceFile>();
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            file.Entries.Add(new ResourceEntry
            {
                Key = newKey,
                Value = values[lang.Code],
                Comment = "Delete button"
            });
            _writer.Write(file);
            resourceFiles.Add(file);
        }

        // Assert - Re-read and verify
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == newKey);

            Assert.NotNull(entry);
            Assert.Equal(values[lang.Code], entry.Value);
            Assert.Equal("Delete button", entry.Comment);
        }
    }

    [Fact]
    public void AddKey_WithEmptyValue_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var newKey = "Placeholder";

        // Act
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            file.Entries.Add(new ResourceEntry
            {
                Key = newKey,
                Value = string.Empty
            });
            _writer.Write(file);
        }

        // Assert
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == newKey);

            Assert.NotNull(entry);
            Assert.Equal(string.Empty, entry.Value);
        }
    }

    [Fact]
    public void UpdateKey_ExistingKey_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var keyToUpdate = "Save";
        var newValues = new Dictionary<string, string>
        {
            { "", "Save Changes" },
            { "el", "Αποθήκευση Αλλαγών" }
        };

        // Act
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);
            if (entry != null)
            {
                entry.Value = newValues[lang.Code];
            }
            _writer.Write(file);
        }

        // Assert
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);

            Assert.NotNull(entry);
            Assert.Equal(newValues[lang.Code], entry.Value);
        }
    }

    [Fact]
    public void DeleteKey_ExistingKey_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var keyToDelete = "Cancel";

        // Act
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToDelete);
            if (entry != null)
            {
                file.Entries.Remove(entry);
            }
            _writer.Write(file);
        }

        // Assert
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToDelete);

            Assert.Null(entry);
        }
    }

    [Fact]
    public void AddKey_UsingDefaultAlias_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var newKey = "NewKeyWithDefaultAlias";
        var values = new Dictionary<string, string>
        {
            { "", "Default Value" },
            { "el", "Ελληνική Τιμή" }
        };

        // Act
        var resourceFiles = new List<ResourceFile>();
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            file.Entries.Add(new ResourceEntry
            {
                Key = newKey,
                Value = values[lang.Code],
                Comment = "Test with default alias"
            });
            _writer.Write(file);
            resourceFiles.Add(file);
        }

        // Assert - Re-read and verify
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == newKey);

            Assert.NotNull(entry);
            Assert.Equal(values[lang.Code], entry.Value);
        }
    }

    [Fact]
    public void UpdateKey_UsingDefaultAlias_Success()
    {
        // Arrange
        var languages = _discovery.DiscoverLanguages(_resxDirectory);
        var keyToUpdate = "Save";
        var newValues = new Dictionary<string, string>
        {
            { "", "Save with Default Alias" },
            { "el", "Αποθήκευση με Default Alias" }
        };

        // Act
        foreach (var lang in languages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);
            if (entry != null)
            {
                entry.Value = newValues[lang.Code];
            }
            _writer.Write(file);
        }

        // Assert
        var verifyLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);

            Assert.NotNull(entry);
            Assert.Equal(newValues[lang.Code], entry.Value);
        }
    }

    #endregion

    #region JSON Tests

    [Fact]
    public void Json_AddKey_ToAllLanguages_Success()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var newKey = "Delete";
        var values = new Dictionary<string, string>
        {
            { "", "Delete" },
            { "el", "Διαγραφή" }
        };

        // Act
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            file.Entries.Add(new ResourceEntry
            {
                Key = newKey,
                Value = values[lang.Code]
            });
            _jsonBackend.Writer.Write(file);
        }

        // Assert - Re-read and verify
        var verifyLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == newKey);

            Assert.NotNull(entry);
            Assert.Equal(values[lang.Code], entry.Value);
        }
    }

    [Fact]
    public void Json_AddKey_WithEmptyValue_Success()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var newKey = "Placeholder";

        // Act
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            file.Entries.Add(new ResourceEntry
            {
                Key = newKey,
                Value = string.Empty
            });
            _jsonBackend.Writer.Write(file);
        }

        // Assert
        var verifyLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == newKey);

            Assert.NotNull(entry);
            Assert.Equal(string.Empty, entry.Value);
        }
    }

    [Fact]
    public void Json_UpdateKey_ExistingKey_Success()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var keyToUpdate = "Save";
        var newValues = new Dictionary<string, string>
        {
            { "", "Save Changes" },
            { "el", "Αποθήκευση Αλλαγών" }
        };

        // Act
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);
            if (entry != null)
            {
                entry.Value = newValues[lang.Code];
            }
            _jsonBackend.Writer.Write(file);
        }

        // Assert
        var verifyLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToUpdate);

            Assert.NotNull(entry);
            Assert.Equal(newValues[lang.Code], entry.Value);
        }
    }

    [Fact]
    public void Json_DeleteKey_ExistingKey_Success()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var keyToDelete = "Cancel";

        // Act
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            file.Entries.RemoveAll(e => e.Key == keyToDelete);
            _jsonBackend.Writer.Write(file);
        }

        // Assert
        var verifyLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var entry = file.Entries.FirstOrDefault(e => e.Key == keyToDelete);

            Assert.Null(entry);
        }
    }

    [Fact]
    public void Json_AddKey_PreservesExistingKeys()
    {
        // Arrange
        var languages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        var newKey = "NewKey";

        // Act
        foreach (var lang in languages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            var initialCount = file.Entries.Count;
            file.Entries.Add(new ResourceEntry { Key = newKey, Value = "New Value" });
            _jsonBackend.Writer.Write(file);
        }

        // Assert
        var verifyLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);
        foreach (var lang in verifyLanguages)
        {
            var file = _jsonBackend.Reader.Read(lang);
            Assert.Equal(3, file.Entries.Count); // Original 2 + new 1
            Assert.Contains(file.Entries, e => e.Key == "Save");
            Assert.Contains(file.Entries, e => e.Key == "Cancel");
            Assert.Contains(file.Entries, e => e.Key == newKey);
        }
    }

    [Fact]
    public void Json_BackendParity_SameInitialKeyCount()
    {
        // Both backends should have the same initial key count
        var resxLanguages = _discovery.DiscoverLanguages(_resxDirectory);
        var jsonLanguages = _jsonBackend.Discovery.DiscoverLanguages(_jsonDirectory);

        var resxDefault = _reader.Read(resxLanguages.First(l => l.IsDefault));
        var jsonDefault = _jsonBackend.Reader.Read(jsonLanguages.First(l => l.IsDefault));

        Assert.Equal(resxDefault.Entries.Count, jsonDefault.Entries.Count);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
