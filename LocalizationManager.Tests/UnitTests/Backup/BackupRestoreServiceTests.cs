// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Enums;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backup;

public class BackupRestoreServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly string _backupPath;
    private readonly BackupVersionManager _backupManager;
    private readonly BackupRestoreService _restoreService;

    public BackupRestoreServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"lrm-restore-test-{Guid.NewGuid()}");
        _backupPath = Path.Combine(_testPath, ".lrm-backups");
        Directory.CreateDirectory(_testPath);
        Directory.CreateDirectory(_backupPath);

        _backupManager = new BackupVersionManager(maxVersions: 10);
        _restoreService = new BackupRestoreService(_backupManager);
    }

    [Fact]
    public async Task RestoreAsync_RestoresFullBackup()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "OriginalValue1" },
            { "Key2", "OriginalValue2" }
        });

        // Create backup
        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Modify the file
        File.WriteAllText(originalFile, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue1" },
            { "Key2", "ModifiedValue2" },
            { "Key3", "Added" }
        }));

        // Act
        await _restoreService.RestoreAsync(fileName, backup.Version, originalFile, _testPath, createBackupBeforeRestore: false);

        // Assert
        var content = File.ReadAllText(originalFile);
        Assert.Contains("OriginalValue1", content);
        Assert.Contains("OriginalValue2", content);
        Assert.DoesNotContain("ModifiedValue1", content);
        Assert.DoesNotContain("Added", content);
    }

    [Fact]
    public async Task RestoreAsync_CreatesBackupBeforeRestore()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "OriginalValue" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Modify file
        File.WriteAllText(originalFile, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue" }
        }));

        var backupsBeforeRestore = await _backupManager.ListBackupsAsync(fileName, _testPath);
        var countBefore = backupsBeforeRestore.Count;

        // Act
        await _restoreService.RestoreAsync(fileName, backup.Version, originalFile, _testPath, createBackupBeforeRestore: true);

        // Assert
        var backupsAfterRestore = await _backupManager.ListBackupsAsync(fileName, _testPath);
        Assert.Equal(countBefore + 1, backupsAfterRestore.Count);
        Assert.Contains(backupsAfterRestore, b => b.Operation == "pre-restore");
    }

    [Fact]
    public async Task RestorePartialAsync_RestoresSelectedKeys()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "OriginalValue1" },
            { "Key2", "OriginalValue2" },
            { "Key3", "OriginalValue3" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Modify file
        File.WriteAllText(originalFile, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue1" },
            { "Key2", "ModifiedValue2" },
            { "Key3", "ModifiedValue3" }
        }));

        var keysToRestore = new List<string> { "Key1", "Key3" };

        // Act
        await _restoreService.RestoreKeysAsync(fileName, backup.Version, keysToRestore, originalFile, _testPath, createBackupBeforeRestore: false);

        // Assert
        var content = File.ReadAllText(originalFile);
        Assert.Contains("OriginalValue1", content); // Restored
        Assert.Contains("ModifiedValue2", content); // Not restored (kept modified)
        Assert.Contains("OriginalValue3", content); // Restored
    }

    [Fact]
    public async Task PreviewRestoreAsync_GeneratesCorrectDiff()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "OriginalValue" },
            { "Key2", "OriginalValue2" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Modify file
        File.WriteAllText(originalFile, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "ModifiedValue" },
            { "Key2", "OriginalValue2" },
            { "Key3", "NewKey" }
        }));

        // Act
        var diff = await _restoreService.PreviewRestoreAsync(fileName, backup.Version, originalFile, _testPath);

        // Assert
        Assert.NotNull(diff);
        Assert.Contains(diff.Changes, c => c.Key == "Key1" && c.Type == ChangeType.Modified);
        Assert.Contains(diff.Changes, c => c.Key == "Key3" && c.Type == ChangeType.Deleted);
        Assert.Equal(2, diff.Statistics.TotalChanges);
    }

    [Fact]
    public async Task ValidateRestoreAsync_ReturnsTrueForValidBackup()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Act
        var result = await _restoreService.ValidateRestoreAsync(fileName, backup.Version, originalFile, _testPath);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateRestoreAsync_ReturnsFalseForNonExistentBackup()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = Path.Combine(_testPath, fileName);

        // Act
        var result = await _restoreService.ValidateRestoreAsync(fileName, 999, filePath, _testPath);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task RestoreAsync_ThrowsForNonExistentBackup()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var filePath = Path.Combine(_testPath, fileName);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _restoreService.RestoreAsync(fileName, 999, filePath, _testPath, createBackupBeforeRestore: false));
    }

    [Fact]
    public async Task RestorePartialAsync_OnlyRestoresSpecifiedKeys()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Original1" },
            { "Key2", "Original2" },
            { "Key3", "Original3" },
            { "Key4", "Original4" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Modify all keys
        File.WriteAllText(originalFile, GetResxContent(new Dictionary<string, string>
        {
            { "Key1", "Modified1" },
            { "Key2", "Modified2" },
            { "Key3", "Modified3" },
            { "Key4", "Modified4" }
        }));

        // Act - Restore only Key2 and Key4
        await _restoreService.RestoreKeysAsync(fileName, backup.Version, new List<string> { "Key2", "Key4" }, originalFile, _testPath, createBackupBeforeRestore: false);

        // Assert
        var content = File.ReadAllText(originalFile);
        Assert.Contains("Modified1", content);  // Not restored
        Assert.Contains("Original2", content);  // Restored
        Assert.Contains("Modified3", content);  // Not restored
        Assert.Contains("Original4", content);  // Restored
    }

    [Fact]
    public async Task PreviewRestoreAsync_ShowsNoChangesWhenIdentical()
    {
        // Arrange
        var fileName = "TestResource.resx";
        var originalFile = CreateResxFile(fileName, new Dictionary<string, string>
        {
            { "Key1", "Value1" },
            { "Key2", "Value2" }
        });

        var backup = await _backupManager.CreateBackupAsync(originalFile, "test", _testPath);

        // Don't modify the file

        // Act
        var diff = await _restoreService.PreviewRestoreAsync(fileName, backup.Version, originalFile, _testPath);

        // Assert
        Assert.Empty(diff.Changes);
        Assert.Equal(0, diff.Statistics.TotalChanges);
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
