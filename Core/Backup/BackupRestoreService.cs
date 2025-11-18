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

using LocalizationManager.Core.Models;
using LocalizationManager.Shared.Models;

namespace LocalizationManager.Core.Backup;

/// <summary>
/// Service for restoring files from backups.
/// </summary>
public class BackupRestoreService
{
    private readonly BackupVersionManager _backupManager;
    private readonly BackupDiffService _diffService;
    private readonly ResourceFileParser _parser;

    public BackupRestoreService(BackupVersionManager backupManager)
    {
        _backupManager = backupManager;
        _diffService = new BackupDiffService();
        _parser = new ResourceFileParser();
    }

    /// <summary>
    /// Previews what would change if a backup were restored.
    /// </summary>
    /// <param name="fileName">Name of the file to restore.</param>
    /// <param name="version">Backup version to restore from.</param>
    /// <param name="currentFilePath">Path to the current file.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>Diff showing what would change.</returns>
    public async Task<BackupDiffResult> PreviewRestoreAsync(
        string fileName,
        int version,
        string currentFilePath,
        string basePath)
    {
        var backup = await _backupManager.GetBackupAsync(fileName, version, basePath);
        if (backup == null)
        {
            throw new InvalidOperationException($"Backup version {version} not found for {fileName}");
        }

        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, version, basePath);
        if (backupFilePath == null || !File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
        }

        if (!File.Exists(currentFilePath))
        {
            throw new FileNotFoundException($"Current file not found: {currentFilePath}");
        }

        return await _diffService.PreviewRestoreAsync(backup, currentFilePath, backupFilePath);
    }

    /// <summary>
    /// Restores a file from a backup (full restore).
    /// </summary>
    /// <param name="fileName">Name of the file to restore.</param>
    /// <param name="version">Backup version to restore from.</param>
    /// <param name="targetFilePath">Path where the file should be restored.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <param name="createBackupBeforeRestore">Whether to create a backup before restoring (default: true).</param>
    /// <returns>True if restored successfully.</returns>
    public async Task<bool> RestoreAsync(
        string fileName,
        int version,
        string targetFilePath,
        string basePath,
        bool createBackupBeforeRestore = true)
    {
        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, version, basePath);
        if (backupFilePath == null || !File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found");
        }

        // Create a backup of the current file before restoring
        if (createBackupBeforeRestore && File.Exists(targetFilePath))
        {
            await _backupManager.CreateBackupAsync(targetFilePath, "pre-restore", basePath);
        }

        // Copy backup file to target location
        File.Copy(backupFilePath, targetFilePath, overwrite: true);

        return true;
    }

    /// <summary>
    /// Restores specific keys from a backup (partial restore).
    /// </summary>
    /// <param name="fileName">Name of the file to restore.</param>
    /// <param name="version">Backup version to restore from.</param>
    /// <param name="keys">List of keys to restore.</param>
    /// <param name="targetFilePath">Path to the file to update.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <param name="createBackupBeforeRestore">Whether to create a backup before restoring (default: true).</param>
    /// <returns>Number of keys restored.</returns>
    public async Task<int> RestoreKeysAsync(
        string fileName,
        int version,
        List<string> keys,
        string targetFilePath,
        string basePath,
        bool createBackupBeforeRestore = true)
    {
        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, version, basePath);
        if (backupFilePath == null || !File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found");
        }

        if (!File.Exists(targetFilePath))
        {
            throw new FileNotFoundException($"Target file not found: {targetFilePath}");
        }

        // Create a backup of the current file before restoring
        if (createBackupBeforeRestore)
        {
            await _backupManager.CreateBackupAsync(targetFilePath, "pre-selective-restore", basePath);
        }

        // Parse both files
        var backupLangInfo = CreateLanguageInfo(backupFilePath);
        var targetLangInfo = CreateLanguageInfo(targetFilePath);
        var backupFile = await Task.Run(() => _parser.Parse(backupLangInfo));
        var targetFile = await Task.Run(() => _parser.Parse(targetLangInfo));

        // Create a dictionary of backup entries for fast lookup (case-insensitive)
        var backupEntries = backupFile.Entries.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

        var restoredCount = 0;

        // Restore specified keys
        foreach (var key in keys)
        {
            if (backupEntries.TryGetValue(key, out var backupEntry))
            {
                // Find or create entry in target file (case-insensitive)
                var targetEntry = targetFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (targetEntry != null)
                {
                    // Update existing entry
                    targetEntry.Value = backupEntry.Value;
                    targetEntry.Comment = backupEntry.Comment;
                }
                else
                {
                    // Add new entry
                    targetFile.Entries.Add(new Models.ResourceEntry
                    {
                        Key = backupEntry.Key,
                        Value = backupEntry.Value,
                        Comment = backupEntry.Comment
                    });
                }
                restoredCount++;
            }
        }

        // Save the updated file
        if (restoredCount > 0)
        {
            await Task.Run(() => _parser.Write(targetFile));
        }

        return restoredCount;
    }

    /// <summary>
    /// Gets a list of keys that would be restored from a backup.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="version">Backup version.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>List of keys in the backup.</returns>
    public async Task<List<string>> GetBackupKeysAsync(
        string fileName,
        int version,
        string basePath)
    {
        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, version, basePath);
        if (backupFilePath == null || !File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found");
        }

        var backupLangInfo = CreateLanguageInfo(backupFilePath);
        var backupFile = await Task.Run(() => _parser.Parse(backupLangInfo));
        return backupFile.Entries.Select(e => e.Key).ToList();
    }

    /// <summary>
    /// Validates that a restore operation is safe.
    /// </summary>
    /// <param name="fileName">Name of the file to restore.</param>
    /// <param name="version">Backup version to restore from.</param>
    /// <param name="targetFilePath">Path to the target file.</param>
    /// <param name="basePath">Base path for the resource files.</param>
    /// <returns>Validation result with any warnings or errors.</returns>
    public async Task<RestoreValidationResult> ValidateRestoreAsync(
        string fileName,
        int version,
        string targetFilePath,
        string basePath)
    {
        var result = new RestoreValidationResult
        {
            IsValid = true
        };

        // Check if backup exists
        var backup = await _backupManager.GetBackupAsync(fileName, version, basePath);
        if (backup == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Backup version {version} not found for {fileName}");
            return result;
        }

        var backupFilePath = await _backupManager.GetBackupFilePathAsync(fileName, version, basePath);
        if (backupFilePath == null || !File.Exists(backupFilePath))
        {
            result.IsValid = false;
            result.Errors.Add("Backup file not found on disk");
            return result;
        }

        // Check if target file exists
        if (!File.Exists(targetFilePath))
        {
            result.Warnings.Add($"Target file does not exist. A new file will be created.");
        }
        else
        {
            // Check if target file is writable
            try
            {
                using var stream = File.Open(targetFilePath, FileMode.Open, FileAccess.Write);
            }
            catch
            {
                result.IsValid = false;
                result.Errors.Add("Target file is not writable");
                return result;
            }

            // Preview the changes
            try
            {
                var diff = await PreviewRestoreAsync(fileName, version, targetFilePath, basePath);
                if (diff.Statistics.TotalChanges > 0)
                {
                    result.Warnings.Add($"This will modify {diff.Statistics.TotalChanges} key(s)");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Could not generate preview: {ex.Message}");
            }
        }

        return result;
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

/// <summary>
/// Result of validating a restore operation.
/// </summary>
public class RestoreValidationResult
{
    /// <summary>
    /// Whether the restore operation is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of errors that prevent restore.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of warnings about the restore.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
