// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.IO.Compression;
using System.Text.Json;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Manages backups before pull operations to enable rollback.
/// </summary>
public class PullBackupManager
{
    private readonly string _projectDirectory;
    private const string BackupDirectory = ".lrm/pull-backups";

    public PullBackupManager(string projectDirectory)
    {
        _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory));
    }

    /// <summary>
    /// Creates a backup of the current state before pulling.
    /// </summary>
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupDir = Path.Combine(_projectDirectory, BackupDirectory);
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        // Generate backup name with timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupName = $"pull-backup-{timestamp}";
        var backupPath = Path.Combine(backupDir, $"{backupName}.zip");

        // Create backup metadata
        var metadata = new BackupMetadata
        {
            BackupName = backupName,
            Timestamp = DateTime.UtcNow,
            ProjectDirectory = _projectDirectory
        };

        // Create zip archive
        using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
        {
            // Backup lrm.json
            var configPath = Path.Combine(_projectDirectory, "lrm.json");
            if (File.Exists(configPath))
            {
                archive.CreateEntryFromFile(configPath, "lrm.json");
            }

            // Backup all resource files in Resources directory
            var resourcesPath = Path.Combine(_projectDirectory, "Resources");
            if (Directory.Exists(resourcesPath))
            {
                BackupDirectoryContents(archive, resourcesPath, "Resources");
            }

            // Add metadata
            var metadataEntry = archive.CreateEntry("backup-metadata.json");
            using (var writer = new StreamWriter(metadataEntry.Open()))
            {
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await writer.WriteAsync(json);
            }
        }

        return backupPath;
    }

    /// <summary>
    /// Lists all available backups.
    /// </summary>
    public List<BackupInfo> ListBackups()
    {
        var backupDir = Path.Combine(_projectDirectory, BackupDirectory);
        if (!Directory.Exists(backupDir))
        {
            return new List<BackupInfo>();
        }

        var backups = new List<BackupInfo>();

        foreach (var file in Directory.GetFiles(backupDir, "*.zip").OrderByDescending(f => f))
        {
            try
            {
                using var archive = ZipFile.OpenRead(file);
                var metadataEntry = archive.GetEntry("backup-metadata.json");

                if (metadataEntry != null)
                {
                    using var reader = new StreamReader(metadataEntry.Open());
                    var json = reader.ReadToEnd();
                    var metadata = JsonSerializer.Deserialize<BackupMetadata>(json);

                    if (metadata != null)
                    {
                        backups.Add(new BackupInfo
                        {
                            BackupPath = file,
                            BackupName = metadata.BackupName,
                            Timestamp = metadata.Timestamp,
                            Size = new FileInfo(file).Length
                        });
                    }
                }
            }
            catch
            {
                // Skip invalid backups
            }
        }

        return backups;
    }

    /// <summary>
    /// Restores a backup.
    /// </summary>
    public async Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup not found: {backupPath}");
        }

        using var archive = ZipFile.OpenRead(backupPath);

        // Restore lrm.json
        var configEntry = archive.GetEntry("lrm.json");
        if (configEntry != null)
        {
            var configPath = Path.Combine(_projectDirectory, "lrm.json");
            configEntry.ExtractToFile(configPath, overwrite: true);
        }

        // Restore Resources directory
        var resourcesPath = Path.Combine(_projectDirectory, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            // Clear existing resources
            Directory.Delete(resourcesPath, recursive: true);
        }

        Directory.CreateDirectory(resourcesPath);

        // Extract all entries that start with "Resources/"
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("Resources/") && !string.IsNullOrEmpty(entry.Name))
            {
                var destPath = Path.Combine(_projectDirectory, entry.FullName);
                var destDir = Path.GetDirectoryName(destPath);

                if (destDir != null && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Deletes old backups, keeping only the most recent ones.
    /// </summary>
    public void PruneBackups(int keepCount = 10)
    {
        var backups = ListBackups();

        if (backups.Count <= keepCount)
        {
            return;
        }

        var toDelete = backups
            .OrderByDescending(b => b.Timestamp)
            .Skip(keepCount)
            .ToList();

        foreach (var backup in toDelete)
        {
            try
            {
                File.Delete(backup.BackupPath);
            }
            catch
            {
                // Ignore errors deleting old backups
            }
        }
    }

    private void BackupDirectoryContents(ZipArchive archive, string sourcePath, string entryPrefix)
    {
        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var entryName = Path.Combine(entryPrefix, relativePath).Replace("\\", "/");
            archive.CreateEntryFromFile(file, entryName);
        }
    }

    private class BackupMetadata
    {
        public string BackupName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ProjectDirectory { get; set; } = string.Empty;
    }

    public class BackupInfo
    {
        public string BackupPath { get; set; } = string.Empty;
        public string BackupName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long Size { get; set; }
    }
}
