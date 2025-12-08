using LocalizationManager.Core.Cloud;
using System.IO.Compression;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class PullBackupManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PullBackupManager _backupManager;

    public PullBackupManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _backupManager = new PullBackupManager(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesZipFile()
    {
        // Arrange
        CreateTestFiles();

        // Act
        var backupPath = await _backupManager.CreateBackupAsync();

        // Assert
        Assert.True(File.Exists(backupPath));
        Assert.True(backupPath.EndsWith(".zip"));
        Assert.Contains("pull-backup-", backupPath);
    }

    [Fact]
    public async Task CreateBackupAsync_IncludesLrmJson()
    {
        // Arrange
        CreateTestFiles();
        var lrmJsonPath = Path.Combine(_testDirectory, "lrm.json");
        await File.WriteAllTextAsync(lrmJsonPath, "{\"format\":\"resx\"}");

        // Act
        var backupPath = await _backupManager.CreateBackupAsync();

        // Assert
        using var archive = ZipFile.OpenRead(backupPath);
        Assert.Contains(archive.Entries, e => e.FullName == "lrm.json");
    }

    [Fact]
    public async Task CreateBackupAsync_IncludesResourcesDirectory()
    {
        // Arrange
        CreateTestFiles();
        var resourcesPath = Path.Combine(_testDirectory, "Resources");
        Directory.CreateDirectory(resourcesPath);
        await File.WriteAllTextAsync(Path.Combine(resourcesPath, "test.resx"), "<root></root>");

        // Act
        var backupPath = await _backupManager.CreateBackupAsync();

        // Assert
        using var archive = ZipFile.OpenRead(backupPath);
        Assert.Contains(archive.Entries, e => e.FullName.StartsWith("Resources/"));
    }

    [Fact]
    public async Task CreateBackupAsync_IncludesMetadata()
    {
        // Arrange
        CreateTestFiles();

        // Act
        var backupPath = await _backupManager.CreateBackupAsync();

        // Assert
        using var archive = ZipFile.OpenRead(backupPath);
        var metadataEntry = archive.Entries.FirstOrDefault(e => e.FullName == "backup-metadata.json");
        Assert.NotNull(metadataEntry);

        using var stream = metadataEntry.Open();
        using var reader = new StreamReader(stream);
        var metadata = await reader.ReadToEndAsync();
        Assert.Contains("timestamp", metadata);
    }

    [Fact]
    public void ListBackups_ReturnsAllBackups()
    {
        // Arrange
        CreateTestFiles();
        var backupDir = Path.Combine(_testDirectory, ".lrm", "pull-backups");
        Directory.CreateDirectory(backupDir);

        var backup1 = Path.Combine(backupDir, "pull-backup-20231201-120000.zip");
        var backup2 = Path.Combine(backupDir, "pull-backup-20231202-120000.zip");
        File.WriteAllText(backup1, "test");
        File.WriteAllText(backup2, "test");

        // Act
        var backups = _backupManager.ListBackups();

        // Assert
        Assert.Equal(2, backups.Count);
        Assert.All(backups, b => Assert.EndsWith(".zip", b.BackupPath));
    }

    [Fact]
    public void ListBackups_OrdersByTimestampDescending()
    {
        // Arrange
        CreateTestFiles();
        var backupDir = Path.Combine(_testDirectory, ".lrm", "pull-backups");
        Directory.CreateDirectory(backupDir);

        var backup1 = Path.Combine(backupDir, "pull-backup-20231201-120000.zip");
        var backup2 = Path.Combine(backupDir, "pull-backup-20231202-120000.zip");
        var backup3 = Path.Combine(backupDir, "pull-backup-20231203-120000.zip");
        File.WriteAllText(backup1, "test");
        File.WriteAllText(backup2, "test");
        File.WriteAllText(backup3, "test");

        // Act
        var backups = _backupManager.ListBackups();

        // Assert
        Assert.Equal(3, backups.Count);
        Assert.Contains("20231203", backups[0].BackupPath);
        Assert.Contains("20231202", backups[1].BackupPath);
        Assert.Contains("20231201", backups[2].BackupPath);
    }

    [Fact]
    public async Task RestoreBackupAsync_RestoresFiles()
    {
        // Arrange
        CreateTestFiles();
        var lrmJsonPath = Path.Combine(_testDirectory, "lrm.json");
        await File.WriteAllTextAsync(lrmJsonPath, "{\"format\":\"resx\"}");

        var backupPath = await _backupManager.CreateBackupAsync();

        // Delete original file
        File.Delete(lrmJsonPath);
        Assert.False(File.Exists(lrmJsonPath));

        // Act
        await _backupManager.RestoreBackupAsync(backupPath);

        // Assert
        Assert.True(File.Exists(lrmJsonPath));
        var content = await File.ReadAllTextAsync(lrmJsonPath);
        Assert.Contains("resx", content);
    }

    [Fact]
    public void PruneBackups_KeepsSpecifiedCount()
    {
        // Arrange
        CreateTestFiles();
        var backupDir = Path.Combine(_testDirectory, ".lrm", "pull-backups");
        Directory.CreateDirectory(backupDir);

        // Create 15 backups
        for (int i = 0; i < 15; i++)
        {
            var backup = Path.Combine(backupDir, $"pull-backup-2023120{i:00}-120000.zip");
            File.WriteAllText(backup, "test");
        }

        // Act
        _backupManager.PruneBackups(keepCount: 10);

        // Assert
        var remainingBackups = Directory.GetFiles(backupDir, "*.zip");
        Assert.Equal(10, remainingBackups.Length);
    }

    [Fact]
    public void PruneBackups_DeletesOldestFirst()
    {
        // Arrange
        CreateTestFiles();
        var backupDir = Path.Combine(_testDirectory, ".lrm", "pull-backups");
        Directory.CreateDirectory(backupDir);

        var oldBackup = Path.Combine(backupDir, "pull-backup-20231201-120000.zip");
        var newBackup = Path.Combine(backupDir, "pull-backup-20231210-120000.zip");
        File.WriteAllText(oldBackup, "test");
        Thread.Sleep(10);
        File.WriteAllText(newBackup, "test");

        // Act
        _backupManager.PruneBackups(keepCount: 1);

        // Assert
        Assert.False(File.Exists(oldBackup));
        Assert.True(File.Exists(newBackup));
    }

    private void CreateTestFiles()
    {
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
    }
}
