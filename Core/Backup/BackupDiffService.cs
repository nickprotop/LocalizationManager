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
using LocalizationManager.Shared.Enums;
using LocalizationManager.Shared.Models;

namespace LocalizationManager.Core.Backup;

/// <summary>
/// Service for comparing backup versions and generating diffs.
/// </summary>
public class BackupDiffService
{
    private readonly ResourceFileParser _parser;

    public BackupDiffService()
    {
        _parser = new ResourceFileParser();
    }

    /// <summary>
    /// Compares two backup versions and generates a diff.
    /// </summary>
    /// <param name="versionA">First backup metadata.</param>
    /// <param name="versionB">Second backup metadata.</param>
    /// <param name="filePathA">Full path to first backup file.</param>
    /// <param name="filePathB">Full path to second backup file.</param>
    /// <param name="includeUnchanged">Whether to include unchanged keys in the result.</param>
    /// <returns>The diff result.</returns>
    public async Task<BackupDiffResult> CompareAsync(
        BackupMetadata versionA,
        BackupMetadata versionB,
        string filePathA,
        string filePathB,
        bool includeUnchanged = false)
    {
        var langInfoA = CreateLanguageInfo(filePathA);
        var langInfoB = CreateLanguageInfo(filePathB);

        var fileA = await Task.Run(() => _parser.Parse(langInfoA));
        var fileB = await Task.Run(() => _parser.Parse(langInfoB));

        return Compare(versionA, versionB, fileA, fileB, includeUnchanged);
    }

    /// <summary>
    /// Compares a backup version with the current file.
    /// </summary>
    /// <param name="backupVersion">Backup metadata.</param>
    /// <param name="backupFilePath">Full path to backup file.</param>
    /// <param name="currentFilePath">Full path to current file.</param>
    /// <param name="includeUnchanged">Whether to include unchanged keys in the result.</param>
    /// <returns>The diff result.</returns>
    public async Task<BackupDiffResult> CompareWithCurrentAsync(
        BackupMetadata backupVersion,
        string backupFilePath,
        string currentFilePath,
        bool includeUnchanged = false)
    {
        var backupLangInfo = CreateLanguageInfo(backupFilePath);
        var currentLangInfo = CreateLanguageInfo(currentFilePath);

        var backupFile = await Task.Run(() => _parser.Parse(backupLangInfo));
        var currentFile = await Task.Run(() => _parser.Parse(currentLangInfo));

        var currentMetadata = new BackupMetadata
        {
            Version = backupVersion.Version + 1,
            Timestamp = DateTime.UtcNow,
            Operation = "current",
            KeyCount = currentFile.Entries.Count
        };

        return Compare(backupVersion, currentMetadata, backupFile, currentFile, includeUnchanged);
    }

    /// <summary>
    /// Previews what would change if a backup were restored to the current file.
    /// Shows changes from current state to backup state.
    /// </summary>
    /// <param name="currentFilePath">Full path to current file.</param>
    /// <param name="backupVersion">Backup metadata.</param>
    /// <param name="backupFilePath">Full path to backup file.</param>
    /// <param name="includeUnchanged">Whether to include unchanged keys in the result.</param>
    /// <returns>The diff result showing what will change.</returns>
    public async Task<BackupDiffResult> PreviewRestoreAsync(
        BackupMetadata backupVersion,
        string currentFilePath,
        string backupFilePath,
        bool includeUnchanged = false)
    {
        var backupLangInfo = CreateLanguageInfo(backupFilePath);
        var currentLangInfo = CreateLanguageInfo(currentFilePath);

        var backupFile = await Task.Run(() => _parser.Parse(backupLangInfo));
        var currentFile = await Task.Run(() => _parser.Parse(currentLangInfo));

        var currentMetadata = new BackupMetadata
        {
            Version = backupVersion.Version + 1,
            Timestamp = DateTime.UtcNow,
            Operation = "current",
            KeyCount = currentFile.Entries.Count
        };

        // Compare current → backup (reversed order) to show what will change when restoring
        return Compare(currentMetadata, backupVersion, currentFile, backupFile, includeUnchanged);
    }

    /// <summary>
    /// Internal method to compare two ResourceFile objects.
    /// </summary>
    private BackupDiffResult Compare(
        BackupMetadata versionA,
        BackupMetadata versionB,
        ResourceFile fileA,
        ResourceFile fileB,
        bool includeUnchanged)
    {
        var changes = new List<KeyChange>();

        // Create dictionaries for faster lookup
        var entriesA = fileA.Entries.ToDictionary(e => e.Key);
        var entriesB = fileB.Entries.ToDictionary(e => e.Key);

        var allKeys = entriesA.Keys.Union(entriesB.Keys).ToHashSet();

        foreach (var key in allKeys)
        {
            var existsInA = entriesA.TryGetValue(key, out var entryA);
            var existsInB = entriesB.TryGetValue(key, out var entryB);

            if (!existsInA && existsInB)
            {
                // Key was added
                changes.Add(new KeyChange
                {
                    Key = key,
                    Type = ChangeType.Added,
                    NewValue = entryB!.Value,
                    NewComment = entryB.Comment,
                    Timestamp = versionB.Timestamp
                });
            }
            else if (existsInA && !existsInB)
            {
                // Key was deleted
                changes.Add(new KeyChange
                {
                    Key = key,
                    Type = ChangeType.Deleted,
                    OldValue = entryA!.Value,
                    OldComment = entryA.Comment,
                    Timestamp = versionB.Timestamp
                });
            }
            else if (existsInA && existsInB)
            {
                // Key exists in both - check if modified
                var valueChanged = entryA!.Value != entryB!.Value;
                var commentChanged = entryA.Comment != entryB.Comment;

                if (valueChanged && commentChanged)
                {
                    changes.Add(new KeyChange
                    {
                        Key = key,
                        Type = ChangeType.Modified,
                        OldValue = entryA.Value,
                        NewValue = entryB.Value,
                        OldComment = entryA.Comment,
                        NewComment = entryB.Comment,
                        Timestamp = versionB.Timestamp
                    });
                }
                else if (valueChanged)
                {
                    changes.Add(new KeyChange
                    {
                        Key = key,
                        Type = ChangeType.Modified,
                        OldValue = entryA.Value,
                        NewValue = entryB.Value,
                        OldComment = entryA.Comment,
                        NewComment = entryB.Comment,
                        Timestamp = versionB.Timestamp
                    });
                }
                else if (commentChanged)
                {
                    changes.Add(new KeyChange
                    {
                        Key = key,
                        Type = ChangeType.CommentChanged,
                        OldValue = entryA.Value,
                        NewValue = entryB.Value,
                        OldComment = entryA.Comment,
                        NewComment = entryB.Comment,
                        Timestamp = versionB.Timestamp
                    });
                }
                else if (includeUnchanged)
                {
                    changes.Add(new KeyChange
                    {
                        Key = key,
                        Type = ChangeType.Unchanged,
                        OldValue = entryA.Value,
                        NewValue = entryB.Value,
                        OldComment = entryA.Comment,
                        NewComment = entryB.Comment,
                        Timestamp = versionB.Timestamp
                    });
                }
            }
        }

        // Calculate statistics
        var stats = new DiffStatistics
        {
            TotalKeys = allKeys.Count,
            AddedCount = changes.Count(c => c.Type == ChangeType.Added),
            ModifiedCount = changes.Count(c => c.Type == ChangeType.Modified),
            DeletedCount = changes.Count(c => c.Type == ChangeType.Deleted),
            CommentChangedCount = changes.Count(c => c.Type == ChangeType.CommentChanged),
            UnchangedCount = includeUnchanged ? changes.Count(c => c.Type == ChangeType.Unchanged) : allKeys.Count - changes.Count
        };

        return new BackupDiffResult
        {
            VersionA = versionA,
            VersionB = versionB,
            Changes = changes.OrderBy(c => c.Key).ToList(),
            Statistics = stats
        };
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
/// Represents a change to a single key between backup versions.
/// </summary>
public class KeyChange
{
    /// <summary>
    /// The resource key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Type of change.
    /// </summary>
    public ChangeType Type { get; set; }

    /// <summary>
    /// Old value (if modified or deleted).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New value (if added or modified).
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Old comment (if modified or deleted).
    /// </summary>
    public string? OldComment { get; set; }

    /// <summary>
    /// New comment (if added or modified).
    /// </summary>
    public string? NewComment { get; set; }

    /// <summary>
    /// Timestamp of the change.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Result of comparing two backup versions.
/// </summary>
public class BackupDiffResult
{
    /// <summary>
    /// Metadata for the first version.
    /// </summary>
    public BackupMetadata VersionA { get; set; } = null!;

    /// <summary>
    /// Metadata for the second version.
    /// </summary>
    public BackupMetadata VersionB { get; set; } = null!;

    /// <summary>
    /// List of changes between the versions.
    /// </summary>
    public List<KeyChange> Changes { get; set; } = new();

    /// <summary>
    /// Statistics about the diff.
    /// </summary>
    public DiffStatistics Statistics { get; set; } = null!;
}

/// <summary>
/// Statistics about a diff.
/// </summary>
public class DiffStatistics
{
    /// <summary>
    /// Total number of unique keys across both versions.
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Number of keys added in version B.
    /// </summary>
    public int AddedCount { get; set; }

    /// <summary>
    /// Number of keys modified between versions.
    /// </summary>
    public int ModifiedCount { get; set; }

    /// <summary>
    /// Number of keys deleted in version B.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Number of keys with only comment changes.
    /// </summary>
    public int CommentChangedCount { get; set; }

    /// <summary>
    /// Number of unchanged keys.
    /// </summary>
    public int UnchangedCount { get; set; }

    /// <summary>
    /// Total number of changes (added + modified + deleted).
    /// </summary>
    public int TotalChanges => AddedCount + ModifiedCount + DeletedCount + CommentChangedCount;
}
