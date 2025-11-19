// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Security.Cryptography;
using System.Text.Json;
using LocalizationManager.Core.Models;
using LocalizationManager.Shared.Models;

namespace LocalizationManager.Core.Backup;

/// <summary>
/// Manages backup versions of resource files with smart rotation.
/// </summary>
public class BackupVersionManager
{
    private const string BackupBasePath = ".lrm/backups";
    private const string ManifestFileName = "manifest.json";
    private readonly int _maxVersions;
    private readonly BackupRotationPolicy? _rotationPolicy;

    /// <summary>
    /// Initializes a new instance of BackupVersionManager.
    /// </summary>
    /// <param name="maxVersions">Maximum number of backup versions to keep (default: 10).</param>
    /// <param name="rotationPolicy">Optional rotation policy for smart cleanup.</param>
    public BackupVersionManager(int maxVersions = 10, BackupRotationPolicy? rotationPolicy = null)
    {
        _maxVersions = maxVersions;
        _rotationPolicy = rotationPolicy;
    }

    /// <summary>
    /// Creates a backup of the specified file.
    /// </summary>
    /// <param name="filePath">Path to the file to backup.</param>
    /// <param name="operation">Operation that triggered the backup (e.g., "update", "delete").</param>
    /// <param name="basePath">Base path for the resource files (used to determine backup location).</param>
    /// <returns>The created backup metadata.</returns>
    public async Task<BackupMetadata> CreateBackupAsync(
        string filePath,
        string operation,
        string basePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        // Determine backup directory
        var fileName = Path.GetFileName(filePath);
        var backupDir = GetBackupDirectory(basePath, fileName);
        Directory.CreateDirectory(backupDir);

        // Load or create manifest
        var manifest = await LoadManifestAsync(backupDir) ?? new BackupManifest
        {
            FileName = fileName
        };

        // Calculate next version number
        var nextVersion = (manifest.Backups.Any() ? manifest.Backups.Max(b => b.Version) : 0) + 1;

        // Create backup file
        var timestamp = DateTime.UtcNow;
        var backupFileName = $"v{nextVersion:D3}_{timestamp:yyyy-MM-ddTHH-mm-ss}.resx";
        var backupFilePath = Path.Combine(backupDir, backupFileName);

        // Copy file to backup location
        File.Copy(filePath, backupFilePath, overwrite: false);

        // Calculate hash and key count
        var hash = await CalculateFileHashAsync(backupFilePath);
        var keyCount = await CountKeysInFileAsync(backupFilePath);

        // Calculate changed keys (compare with previous version)
        var changedKeys = 0;
        List<string>? changedKeyNames = null;
        var previousBackup = manifest.GetLatest();
        if (previousBackup != null)
        {
            var previousBackupPath = Path.Combine(backupDir, previousBackup.FilePath);
            var (count, keyNames) = await CountChangedKeysAsync(previousBackupPath, backupFilePath);
            changedKeys = count;
            changedKeyNames = keyNames;
        }

        // Create metadata
        var metadata = new BackupMetadata
        {
            Version = nextVersion,
            Timestamp = timestamp,
            Hash = hash,
            Operation = operation,
            User = Environment.UserName,
            KeyCount = keyCount,
            ChangedKeys = changedKeys,
            ChangedKeyNames = changedKeyNames,
            FilePath = backupFileName
        };

        // Add to manifest
        manifest.Backups.Add(metadata);

        // Apply rotation policy
        if (_rotationPolicy != null)
        {
            await _rotationPolicy.ApplyRotationAsync(manifest, backupDir);
        }
        else
        {
            // Simple rotation: keep only last N versions
            await ApplySimpleRotationAsync(manifest, backupDir);
        }

        // Save manifest
        await SaveManifestAsync(backupDir, manifest);

        return metadata;
    }

    /// <summary>
    /// Lists all backup versions for a file.
    /// </summary>
    /// <param name="fileName">Name of the file to list backups for.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>List of backup metadata, ordered by version descending.</returns>
    public async Task<List<BackupMetadata>> ListBackupsAsync(string fileName, string basePath)
    {
        var backupDir = GetBackupDirectory(basePath, fileName);
        if (!Directory.Exists(backupDir))
        {
            return new List<BackupMetadata>();
        }

        var manifest = await LoadManifestAsync(backupDir);
        if (manifest == null)
        {
            return new List<BackupMetadata>();
        }

        return manifest.Backups.OrderByDescending(b => b.Version).ToList();
    }

    /// <summary>
    /// Gets a specific backup version.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="version">Version number to retrieve.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>The backup metadata, or null if not found.</returns>
    public async Task<BackupMetadata?> GetBackupAsync(string fileName, int version, string basePath)
    {
        var backupDir = GetBackupDirectory(basePath, fileName);
        if (!Directory.Exists(backupDir))
        {
            return null;
        }

        var manifest = await LoadManifestAsync(backupDir);
        return manifest?.GetByVersion(version);
    }

    /// <summary>
    /// Gets the file path for a specific backup version.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="version">Version number.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>Full path to the backup file, or null if not found.</returns>
    public async Task<string?> GetBackupFilePathAsync(string fileName, int version, string basePath)
    {
        var backup = await GetBackupAsync(fileName, version, basePath);
        if (backup == null)
        {
            return null;
        }

        var backupDir = GetBackupDirectory(basePath, fileName);
        return Path.Combine(backupDir, backup.FilePath);
    }

    /// <summary>
    /// Deletes a specific backup version.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="version">Version number to delete.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public async Task<bool> DeleteBackupAsync(string fileName, int version, string basePath)
    {
        var backupDir = GetBackupDirectory(basePath, fileName);
        if (!Directory.Exists(backupDir))
        {
            return false;
        }

        var manifest = await LoadManifestAsync(backupDir);
        if (manifest == null)
        {
            return false;
        }

        var backup = manifest.GetByVersion(version);
        if (backup == null)
        {
            return false;
        }

        // Delete backup file
        var backupFilePath = Path.Combine(backupDir, backup.FilePath);
        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
        }

        // Remove from manifest
        manifest.Backups.Remove(backup);

        // Save manifest
        await SaveManifestAsync(backupDir, manifest);

        return true;
    }

    /// <summary>
    /// Deletes all backups for a file.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    public async Task DeleteAllBackupsAsync(string fileName, string basePath)
    {
        var backupDir = GetBackupDirectory(basePath, fileName);
        if (Directory.Exists(backupDir))
        {
            Directory.Delete(backupDir, recursive: true);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the backup directory for a specific file.
    /// </summary>
    private string GetBackupDirectory(string basePath, string fileName)
    {
        return Path.Combine(basePath, BackupBasePath, fileName);
    }

    /// <summary>
    /// Loads the manifest file from the backup directory.
    /// </summary>
    private async Task<BackupManifest?> LoadManifestAsync(string backupDir)
    {
        var manifestPath = Path.Combine(backupDir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<BackupManifest>(json);
        }
        catch
        {
            // If manifest is corrupted, return null
            return null;
        }
    }

    /// <summary>
    /// Saves the manifest file to the backup directory.
    /// </summary>
    private async Task SaveManifestAsync(string backupDir, BackupManifest manifest)
    {
        var manifestPath = Path.Combine(backupDir, ManifestFileName);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(manifest, options);
        await File.WriteAllTextAsync(manifestPath, json);
    }

    /// <summary>
    /// Calculates SHA256 hash of a file.
    /// </summary>
    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Counts the number of keys in a .resx file.
    /// </summary>
    private async Task<int> CountKeysInFileAsync(string filePath)
    {
        try
        {
            var parser = new ResourceFileParser();
            var langInfo = CreateLanguageInfo(filePath);
            var resourceFile = await Task.Run(() => parser.Parse(langInfo));
            return resourceFile.Entries.Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Counts the number of keys that changed between two versions and returns their names.
    /// </summary>
    private async Task<(int count, List<string> keyNames)> CountChangedKeysAsync(string oldFilePath, string newFilePath)
    {
        try
        {
            var parser = new ResourceFileParser();
            var oldLangInfo = CreateLanguageInfo(oldFilePath);
            var newLangInfo = CreateLanguageInfo(newFilePath);
            var oldFile = await Task.Run(() => parser.Parse(oldLangInfo));
            var newFile = await Task.Run(() => parser.Parse(newLangInfo));

            var oldKeys = new HashSet<string>(oldFile.Entries.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);
            var newKeys = new HashSet<string>(newFile.Entries.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);

            var changedKeyNames = new List<string>();

            // Added keys
            var addedKeys = newKeys.Except(oldKeys).ToList();
            changedKeyNames.AddRange(addedKeys);

            // Deleted keys
            var deletedKeys = oldKeys.Except(newKeys).ToList();
            changedKeyNames.AddRange(deletedKeys);

            // Modified keys (same key, different value or comment)
            var commonKeys = oldKeys.Intersect(newKeys);
            foreach (var key in commonKeys)
            {
                var oldEntry = oldFile.Entries.First(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                var newEntry = newFile.Entries.First(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (oldEntry.Value != newEntry.Value || oldEntry.Comment != newEntry.Comment)
                {
                    changedKeyNames.Add(key);
                }
            }

            return (changedKeyNames.Count, changedKeyNames);
        }
        catch
        {
            return (0, new List<string>());
        }
    }

    /// <summary>
    /// Applies simple rotation policy (keep only last N versions).
    /// </summary>
    private async Task ApplySimpleRotationAsync(BackupManifest manifest, string backupDir)
    {
        if (manifest.Backups.Count <= _maxVersions)
        {
            return;
        }

        // Sort by version and keep only the latest N
        var toDelete = manifest.Backups
            .OrderByDescending(b => b.Version)
            .Skip(_maxVersions)
            .ToList();

        foreach (var backup in toDelete)
        {
            // Delete backup file
            var backupFilePath = Path.Combine(backupDir, backup.FilePath);
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }

            // Remove from manifest
            manifest.Backups.Remove(backup);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a minimal LanguageInfo object from a file path for parsing backup files.
    /// </summary>
    private static LanguageInfo CreateLanguageInfo(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return new LanguageInfo
        {
            FilePath = filePath,
            BaseName = fileName,
            Code = "backup",
            Name = "Backup",
            IsDefault = false
        };
    }
}
