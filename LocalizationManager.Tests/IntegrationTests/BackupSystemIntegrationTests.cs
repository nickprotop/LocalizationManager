// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Enums;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class BackupSystemIntegrationTests : IDisposable
{
    private readonly string _testPath;
    private readonly BackupVersionManager _backupManager;
    private readonly BackupDiffService _diffService;
    private readonly BackupRestoreService _restoreService;
    private readonly BackupRotationPolicy _rotationPolicy;

    public BackupSystemIntegrationTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"lrm-backup-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);

        _backupManager = new BackupVersionManager(maxVersions: 10);
        _diffService = new BackupDiffService();
        _restoreService = new BackupRestoreService(_backupManager);
        _rotationPolicy = new BackupRotationPolicy
        {
            KeepAllForHours = 24,
            KeepDailyForDays = 7,
            KeepWeeklyForWeeks = 4,
            KeepMonthlyForMonths = 6,
            MaxTotalBackups = 10
        };
    }

    [Fact]
    public async Task FullWorkflow_CreateBackupDiffRestore()
    {
        // Arrange - Create initial file
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "InitialValue1" },
            { "Key2", "InitialValue2" },
            { "Key3", "InitialValue3" }
        });

        // Act 1: Create backup
        var backup1 = await _backupManager.CreateBackupAsync(filePath, "initial", _testPath);
        Assert.Equal(1, backup1.Version);

        // Act 2: Modify file
        File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue1" },
            { "Key2", "InitialValue2" },   // Unchanged
            { "Key4", "NewValue4" }          // Added (Key3 deleted)
        }));

        // Act 3: Create second backup
        var backup2 = await _backupManager.CreateBackupAsync(filePath, "modified", _testPath);
        Assert.Equal(2, backup2.Version);

        // Act 4: Compare backups
        var backupFilePath1 = await _backupManager.GetBackupFilePathAsync(fileName, 1, _testPath);
        var backupFilePath2 = await _backupManager.GetBackupFilePathAsync(fileName, 2, _testPath);
        var diff = await _diffService.CompareAsync(backup1, backup2, backupFilePath1!, backupFilePath2!, includeUnchanged: false);

        // Assert: Verify diff
        Assert.Equal(3, diff.Changes.Count);
        Assert.Contains(diff.Changes, c => c.Key == "Key1" && c.Type == ChangeType.Modified);
        Assert.Contains(diff.Changes, c => c.Key == "Key3" && c.Type == ChangeType.Deleted);
        Assert.Contains(diff.Changes, c => c.Key == "Key4" && c.Type == ChangeType.Added);

        // Act 5: Restore first backup
        await _restoreService.RestoreAsync(fileName, 1, filePath, _testPath, createBackupBeforeRestore: true);

        // Assert: Verify restoration
        var content = File.ReadAllText(filePath);
        Assert.Contains("InitialValue1", content);
        Assert.Contains("InitialValue2", content);
        Assert.Contains("InitialValue3", content);
        Assert.DoesNotContain("ModifiedValue1", content);
        Assert.DoesNotContain("NewValue4", content);

        // Act 6: Verify pre-restore backup was created
        var backups = await _backupManager.ListBackupsAsync(fileName, _testPath);
        Assert.Equal(3, backups.Count);
        Assert.Contains(backups, b => b.Operation == "pre-restore");
    }

    [Fact]
    public async Task MultipleBackups_WithRotation()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        // Act: Create 15 backups (exceeds max of 10)
        for (int i = 1; i <= 15; i++)
        {
            File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
            {
                { "Key1", $"Value{i}" }
            }));
            await _backupManager.CreateBackupAsync(filePath, $"test{i}", _testPath);
            await Task.Delay(10); // Small delay to ensure different timestamps
        }

        // Assert - Rotation happens automatically during CreateBackupAsync
        var backups = await _backupManager.ListBackupsAsync(fileName, _testPath);
        Assert.Equal(10, backups.Count); // Should keep only 10 most recent
        Assert.Equal(15, backups[0].Version);  // Most recent
        Assert.Equal(6, backups[9].Version);   // Oldest kept
    }

    [Fact]
    public async Task PartialRestore_SelectiveKeys()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Original1" },
            { "Key2", "Original2" },
            { "Key3", "Original3" },
            { "Key4", "Original4" },
            { "Key5", "Original5" }
        });

        var backup = await _backupManager.CreateBackupAsync(filePath, "original", _testPath);

        // Modify all keys
        File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "Modified1" },
            { "Key2", "Modified2" },
            { "Key3", "Modified3" },
            { "Key4", "Modified4" },
            { "Key5", "Modified5" }
        }));

        // Act: Restore only Key2, Key3, and Key5
        await _restoreService.RestoreKeysAsync(
            fileName,
            backup.Version,
            new List<string> { "Key2", "Key3", "Key5" },
            filePath,
            _testPath,
            createBackupBeforeRestore: false);

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("Modified1", content);  // Not restored
        Assert.Contains("Original2", content);  // Restored
        Assert.Contains("Original3", content);  // Restored
        Assert.Contains("Modified4", content);  // Not restored
        Assert.Contains("Original5", content);  // Restored
    }

    [Fact]
    public async Task PreviewBeforeRestore_CompareChanges()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "KeyA", "OriginalA" },
            { "KeyB", "OriginalB" }
        });

        var backup = await _backupManager.CreateBackupAsync(filePath, "original", _testPath);

        // Modify
        File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
        {
            { "KeyA", "ModifiedA" },
            { "KeyB", "OriginalB" },
            { "KeyC", "NewC" }
        }));

        // Act: Preview restore
        var preview = await _restoreService.PreviewRestoreAsync(fileName, backup.Version, filePath, _testPath);

        // Assert: Should show what will change
        Assert.Equal(2, preview.Changes.Count);
        Assert.Contains(preview.Changes, c => c.Key == "KeyA" && c.Type == ChangeType.Modified);
        Assert.Contains(preview.Changes, c => c.Key == "KeyC" && c.Type == ChangeType.Deleted);

        // Act: Perform actual restore
        await _restoreService.RestoreAsync(fileName, backup.Version, filePath, _testPath, createBackupBeforeRestore: false);

        // Assert: Verify changes match preview
        var content = File.ReadAllText(filePath);
        Assert.Contains("OriginalA", content);
        Assert.Contains("OriginalB", content);
        Assert.DoesNotContain("ModifiedA", content);
        Assert.DoesNotContain("NewC", content);
    }

    [Fact]
    public async Task CompareBackupWithCurrent_ShowsDifferences()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Status", "Draft" },
            { "Title", "Original Title" }
        });

        var backup = await _backupManager.CreateBackupAsync(filePath, "draft", _testPath);

        // Modify current file
        File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
        {
            { "Status", "Published" },
            { "Title", "Updated Title" },
            { "Author", "John Doe" }
        }));

        // Act
        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, backup.Version, _testPath);
        var diff = await _diffService.CompareWithCurrentAsync(backup, backupFilePath!, filePath, includeUnchanged: false);

        // Assert
        Assert.Equal(3, diff.Changes.Count);
        Assert.Contains(diff.Changes, c => c.Key == "Status" && c.OldValue == "Draft" && c.NewValue == "Published");
        Assert.Contains(diff.Changes, c => c.Key == "Title" && c.OldValue == "Original Title" && c.NewValue == "Updated Title");
        Assert.Contains(diff.Changes, c => c.Key == "Author" && c.Type == ChangeType.Added);
    }

    [Fact]
    public async Task HashVerification_DetectsFileChanges()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var backup1 = await _backupManager.CreateBackupAsync(filePath, "test1", _testPath);
        var hash1 = backup1.Hash;

        // Modify file slightly
        File.WriteAllText(filePath, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "Value2" }  // Different value
        }));

        var backup2 = await _backupManager.CreateBackupAsync(filePath, "test2", _testPath);
        var hash2 = backup2.Hash;

        // Assert
        Assert.NotEqual(hash1, hash2); // Hashes should be different
    }

    [Fact]
    public async Task DeleteBackup_RemovesFromManifestAndDisk()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var backup1 = await _backupManager.CreateBackupAsync(filePath, "test1", _testPath);
        var backup2 = await _backupManager.CreateBackupAsync(filePath, "test2", _testPath);
        var backup3 = await _backupManager.CreateBackupAsync(filePath, "test3", _testPath);

        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, backup2.Version, _testPath);
        Assert.True(File.Exists(backupFilePath));

        // Act
        var deleted = await _backupManager.DeleteBackupAsync(fileName, backup2.Version, _testPath);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(backupFilePath));

        var backups = await _backupManager.ListBackupsAsync(fileName, _testPath);
        Assert.Equal(2, backups.Count);
        Assert.DoesNotContain(backups, b => b.Version == 2);
    }

    private string CreateResxFile(string fileName, Dictionary<string, string> entries)
    {
        var filePath = Path.Combine(_testPath, fileName);
        var content = GetResxContent(entries);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string GetResxContent(Dictionary<string, string> entries)
    {
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
        return content;
    }

    #region JSON Backup Tests

    [Fact]
    public async Task Json_FullWorkflow_CreateBackupAndRestore()
    {
        // Arrange - Create initial JSON file
        var fileName = "TestResource.json";
        var filePath = CreateJsonFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "InitialValue1" },
            { "Key2", "InitialValue2" },
            { "Key3", "InitialValue3" }
        });

        // Act 1: Create backup
        var backup1 = await _backupManager.CreateBackupAsync(filePath, "initial", _testPath);
        Assert.Equal(1, backup1.Version);

        // Act 2: Modify file
        File.WriteAllText(filePath, GetJsonContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue1" },
            { "Key2", "InitialValue2" },
            { "Key4", "NewValue4" }
        }));

        // Act 3: Create second backup
        var backup2 = await _backupManager.CreateBackupAsync(filePath, "modified", _testPath);
        Assert.Equal(2, backup2.Version);

        // Act 4: Restore first backup
        await _restoreService.RestoreAsync(fileName, 1, filePath, _testPath, createBackupBeforeRestore: true);

        // Assert: Verify restoration
        var content = File.ReadAllText(filePath);
        Assert.Contains("InitialValue1", content);
        Assert.Contains("InitialValue2", content);
        Assert.Contains("InitialValue3", content);
        Assert.DoesNotContain("ModifiedValue1", content);
        Assert.DoesNotContain("NewValue4", content);
    }

    [Fact]
    public async Task Json_MultipleBackups_WorksLikeResx()
    {
        // Arrange
        var fileName = "TestResource.json";
        var filePath = CreateJsonFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        // Act: Create 5 backups
        for (int i = 1; i <= 5; i++)
        {
            File.WriteAllText(filePath, GetJsonContent(new Dictionary<string, string>
            {
                { "Key1", $"Value{i}" }
            }));
            await _backupManager.CreateBackupAsync(filePath, $"test{i}", _testPath);
            await Task.Delay(10);
        }

        // Assert
        var backups = await _backupManager.ListBackupsAsync(fileName, _testPath);
        Assert.Equal(5, backups.Count);
        Assert.Equal(5, backups[0].Version);
        Assert.Equal(1, backups[4].Version);
    }

    [Fact]
    public async Task Json_HashVerification_DetectsChanges()
    {
        // Arrange
        var fileName = "TestResource.json";
        var filePath = CreateJsonFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var backup1 = await _backupManager.CreateBackupAsync(filePath, "test1", _testPath);
        var hash1 = backup1.Hash;

        // Modify file
        File.WriteAllText(filePath, GetJsonContent(new Dictionary<string, string>
        {
            { "Key1", "Value2" }
        }));

        var backup2 = await _backupManager.CreateBackupAsync(filePath, "test2", _testPath);
        var hash2 = backup2.Hash;

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    private string CreateJsonFile(string fileName, Dictionary<string, string> entries)
    {
        var filePath = Path.Combine(_testPath, fileName);
        var content = GetJsonContent(entries);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string GetJsonContent(Dictionary<string, string> entries)
    {
        var pairs = entries.Select(e => $"  \"{e.Key}\": \"{e.Value}\"");
        return "{\n" + string.Join(",\n", pairs) + "\n}";
    }

    #endregion

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
