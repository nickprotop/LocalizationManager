// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backup;

public class BackupVersionManagerTests : IDisposable
{
    private readonly string _testBackupPath;
    private readonly string _testFilePath;
    private readonly BackupVersionManager _manager;

    public BackupVersionManagerTests()
    {
        _testBackupPath = Path.Combine(Path.GetTempPath(), $"lrm-test-backups-{Guid.NewGuid()}");
        _testFilePath = Path.Combine(Path.GetTempPath(), $"TestResource-{Guid.NewGuid()}.resx");
        _manager = new BackupVersionManager(maxVersions: 5);

        Directory.CreateDirectory(_testBackupPath);
        File.WriteAllText(_testFilePath, GetSampleResxContent());
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);

        // Act
        var backup = await _manager.CreateBackupAsync(_testFilePath, "test", _testBackupPath);

        // Assert
        Assert.NotNull(backup);
        Assert.Equal(1, backup.Version);
        Assert.Equal("test", backup.Operation);
        Assert.True(backup.KeyCount > 0);

        var backupFilePath = await _manager.GetBackupFilePathAsync(fileName, backup.Version, _testBackupPath);
        Assert.True(File.Exists(backupFilePath));
    }

    [Fact]
    public async Task CreateBackupAsync_IncrementsVersionNumber()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);

        // Act
        var backup1 = await _manager.CreateBackupAsync(_testFilePath, "test1", _testBackupPath);
        var backup2 = await _manager.CreateBackupAsync(_testFilePath, "test2", _testBackupPath);
        var backup3 = await _manager.CreateBackupAsync(_testFilePath, "test3", _testBackupPath);

        // Assert
        Assert.Equal(1, backup1.Version);
        Assert.Equal(2, backup2.Version);
        Assert.Equal(3, backup3.Version);
    }

    [Fact]
    public async Task CreateBackupAsync_GeneratesValidHash()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);

        // Act
        var backup = await _manager.CreateBackupAsync(_testFilePath, "test", _testBackupPath);

        // Assert
        Assert.NotNull(backup.Hash);
        Assert.NotEmpty(backup.Hash);
        Assert.Equal(64, backup.Hash.Length); // SHA256 produces 64 hex characters
    }

    [Fact]
    public async Task ListBackupsAsync_ReturnsAllBackups()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);
        await _manager.CreateBackupAsync(_testFilePath, "test1", _testBackupPath);
        await _manager.CreateBackupAsync(_testFilePath, "test2", _testBackupPath);
        await _manager.CreateBackupAsync(_testFilePath, "test3", _testBackupPath);

        // Act
        var backups = await _manager.ListBackupsAsync(fileName, _testBackupPath);

        // Assert
        Assert.Equal(3, backups.Count);
        Assert.Equal(3, backups[0].Version); // Most recent first
        Assert.Equal(2, backups[1].Version);
        Assert.Equal(1, backups[2].Version);
    }

    [Fact]
    public async Task GetBackupAsync_ReturnsCorrectBackup()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);
        await _manager.CreateBackupAsync(_testFilePath, "test1", _testBackupPath);
        var backup2 = await _manager.CreateBackupAsync(_testFilePath, "test2", _testBackupPath);
        await _manager.CreateBackupAsync(_testFilePath, "test3", _testBackupPath);

        // Act
        var retrievedBackup = await _manager.GetBackupAsync(fileName, 2, _testBackupPath);

        // Assert
        Assert.NotNull(retrievedBackup);
        Assert.Equal(backup2.Version, retrievedBackup.Version);
        Assert.Equal(backup2.Operation, retrievedBackup.Operation);
        Assert.Equal(backup2.Hash, retrievedBackup.Hash);
    }

    [Fact]
    public async Task GetBackupAsync_ReturnsNullForNonExistentVersion()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);
        await _manager.CreateBackupAsync(_testFilePath, "test", _testBackupPath);

        // Act
        var backup = await _manager.GetBackupAsync(fileName, 999, _testBackupPath);

        // Assert
        Assert.Null(backup);
    }

    [Fact]
    public async Task DeleteBackupAsync_RemovesBackup()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);
        await _manager.CreateBackupAsync(_testFilePath, "test1", _testBackupPath);
        var backup2 = await _manager.CreateBackupAsync(_testFilePath, "test2", _testBackupPath);
        await _manager.CreateBackupAsync(_testFilePath, "test3", _testBackupPath);

        // Act
        var deleted = await _manager.DeleteBackupAsync(fileName, backup2.Version, _testBackupPath);

        // Assert
        Assert.True(deleted);
        var backups = await _manager.ListBackupsAsync(fileName, _testBackupPath);
        Assert.Equal(2, backups.Count);
        Assert.DoesNotContain(backups, b => b.Version == 2);
    }

    [Fact]
    public async Task CreateBackupAsync_AppliesSimpleRotation()
    {
        // Arrange
        var fileName = Path.GetFileName(_testFilePath);
        var manager = new BackupVersionManager(maxVersions: 3);

        // Create 5 backups (exceeds max of 3)
        await manager.CreateBackupAsync(_testFilePath, "test1", _testBackupPath);
        await manager.CreateBackupAsync(_testFilePath, "test2", _testBackupPath);
        await manager.CreateBackupAsync(_testFilePath, "test3", _testBackupPath);
        await manager.CreateBackupAsync(_testFilePath, "test4", _testBackupPath);
        await manager.CreateBackupAsync(_testFilePath, "test5", _testBackupPath);

        // Assert - Rotation happens automatically during CreateBackupAsync
        var backups = await manager.ListBackupsAsync(fileName, _testBackupPath);
        Assert.Equal(3, backups.Count);
        Assert.Equal(5, backups[0].Version); // Most recent
        Assert.Equal(4, backups[1].Version);
        Assert.Equal(3, backups[2].Version);
    }

    private string GetSampleResxContent()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <data name=""TestKey1"" xml:space=""preserve"">
    <value>Test Value 1</value>
    <comment>Test Comment 1</comment>
  </data>
  <data name=""TestKey2"" xml:space=""preserve"">
    <value>Test Value 2</value>
  </data>
</root>";
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);

            if (Directory.Exists(_testBackupPath))
                Directory.Delete(_testBackupPath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
