// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Enums;
using LocalizationManager.Shared.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backup;

public class BackupDiffServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly BackupDiffService _service;

    public BackupDiffServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"lrm-diff-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _service = new BackupDiffService();
    }

    [Fact]
    public async Task CompareAsync_DetectsAddedKeys()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Key1", "Value1" },
            { "Key2", "Value2" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal("Key2", change.Key);
        Assert.Equal(ChangeType.Added, change.Type);
        Assert.Null(change.OldValue);
        Assert.Equal("Value2", change.NewValue);
        Assert.Equal(1, result.Statistics.AddedCount);
    }

    [Fact]
    public async Task CompareAsync_DetectsDeletedKeys()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Key1", "Value1" },
            { "Key2", "Value2" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal("Key2", change.Key);
        Assert.Equal(ChangeType.Deleted, change.Type);
        Assert.Equal("Value2", change.OldValue);
        Assert.Null(change.NewValue);
        Assert.Equal(1, result.Statistics.DeletedCount);
    }

    [Fact]
    public async Task CompareAsync_DetectsModifiedKeys()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Key1", "OldValue" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Key1", "NewValue" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal("Key1", change.Key);
        Assert.Equal(ChangeType.Modified, change.Type);
        Assert.Equal("OldValue", change.OldValue);
        Assert.Equal("NewValue", change.NewValue);
        Assert.Equal(1, result.Statistics.ModifiedCount);
    }

    [Fact]
    public async Task CompareAsync_DetectsMultipleChangeTypes()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Unchanged", "Same" },
            { "Modified", "OldValue" },
            { "Deleted", "Gone" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Unchanged", "Same" },
            { "Modified", "NewValue" },
            { "Added", "New" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Equal(3, result.Changes.Count);
        Assert.Equal(1, result.Statistics.AddedCount);
        Assert.Equal(1, result.Statistics.ModifiedCount);
        Assert.Equal(1, result.Statistics.DeletedCount);
        Assert.Equal(3, result.Statistics.TotalChanges);
    }

    [Fact]
    public async Task CompareAsync_IncludesUnchangedWhenRequested()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Unchanged1", "Same1" },
            { "Unchanged2", "Same2" },
            { "Modified", "OldValue" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Unchanged1", "Same1" },
            { "Unchanged2", "Same2" },
            { "Modified", "NewValue" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: true);

        // Assert
        Assert.Equal(3, result.Changes.Count);
        Assert.Equal(2, result.Changes.Count(c => c.Type == ChangeType.Unchanged));
        Assert.Single(result.Changes, c => c.Type == ChangeType.Modified);
    }

    [Fact]
    public async Task CompareAsync_ExcludesUnchangedByDefault()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>
        {
            { "Unchanged", "Same" },
            { "Modified", "OldValue" }
        });

        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>
        {
            { "Unchanged", "Same" },
            { "Modified", "NewValue" }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Single(result.Changes);
        Assert.DoesNotContain(result.Changes, c => c.Type == ChangeType.Unchanged);
    }

    [Fact]
    public async Task CompareAsync_HandlesEmptyFiles()
    {
        // Arrange
        var oldFile = CreateResxFile("old.resx", new Dictionary<string, string>());
        var newFile = CreateResxFile("new.resx", new Dictionary<string, string>());

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Empty(result.Changes);
        Assert.Equal(0, result.Statistics.TotalChanges);
    }

    [Fact]
    public async Task CompareWithCurrentAsync_DetectsChanges()
    {
        // Arrange
        var backupMetadata = new BackupMetadata
        {
            Version = 1,
            FilePath = "TestResource.v001.resx",
            Operation = "test",
            Timestamp = DateTime.UtcNow,
            KeyCount = 2,
            Hash = "testhash"
        };

        var backupFile = CreateResxFile("backup.resx", new Dictionary<string, string>
        {
            { "Key1", "OldValue" },
            { "Key2", "Value2" }
        });

        var currentFile = CreateResxFile("current.resx", new Dictionary<string, string>
        {
            { "Key1", "NewValue" },
            { "Key2", "Value2" },
            { "Key3", "Added" }
        });

        // Act
        var result = await _service.CompareWithCurrentAsync(
            backupMetadata,
            backupFile,
            currentFile,
            includeUnchanged: false);

        // Assert
        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, c => c.Type == ChangeType.Modified && c.Key == "Key1");
        Assert.Contains(result.Changes, c => c.Type == ChangeType.Added && c.Key == "Key3");
    }

    [Fact]
    public async Task CompareAsync_DetectsCommentChanges()
    {
        // Arrange
        var oldFile = CreateResxFileWithComments("old.resx", new Dictionary<string, (string Value, string? Comment)>
        {
            { "Key1", ("Value1", "Old comment") }
        });

        var newFile = CreateResxFileWithComments("new.resx", new Dictionary<string, (string Value, string? Comment)>
        {
            { "Key1", ("Value1", "New comment") }
        });

        var versionA = new BackupMetadata { Version = 1, Timestamp = DateTime.UtcNow.AddHours(-1) };
        var versionB = new BackupMetadata { Version = 2, Timestamp = DateTime.UtcNow };

        // Act
        var result = await _service.CompareAsync(versionA, versionB, oldFile, newFile, includeUnchanged: false);

        // Assert
        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(ChangeType.CommentChanged, change.Type);
        Assert.Equal("Old comment", change.OldComment);
        Assert.Equal("New comment", change.NewComment);
    }

    private string CreateResxFile(string fileName, Dictionary<string, string> entries)
    {
        var filePath = Path.Combine(_testPath, fileName);
        var content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
";
        foreach (var entry in entries)
        {
            content += $@"  <data name=""{entry.Key}"" xml:space=""preserve"">
    <value>{entry.Value}</value>
  </data>
";
        }
        content += "</root>";

        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string CreateResxFileWithComments(string fileName, Dictionary<string, (string Value, string? Comment)> entries)
    {
        var filePath = Path.Combine(_testPath, fileName);
        var content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
";
        foreach (var entry in entries)
        {
            content += $@"  <data name=""{entry.Key}"" xml:space=""preserve"">
    <value>{entry.Value.Value}</value>
";
            if (!string.IsNullOrEmpty(entry.Value.Comment))
            {
                content += $"    <comment>{entry.Value.Comment}</comment>\n";
            }
            content += "  </data>\n";
        }
        content += "</root>";

        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
