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

namespace LocalizationManager.Shared.Models;

/// <summary>
/// Metadata for a single backup version.
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// Backup version number (1-based).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Timestamp when the backup was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// SHA256 hash of the backup file content.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Operation that triggered the backup (e.g., "update", "delete", "import", "translate").
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// User who created the backup (if available).
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Number of keys in this backup.
    /// </summary>
    public int KeyCount { get; set; }

    /// <summary>
    /// Number of keys that changed compared to previous version.
    /// </summary>
    public int ChangedKeys { get; set; }

    /// <summary>
    /// Relative path to the backup file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// Manifest for all backups of a specific resource file.
/// </summary>
public class BackupManifest
{
    /// <summary>
    /// Original file name being backed up.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// List of all backup versions, ordered by version number.
    /// </summary>
    public List<BackupMetadata> Backups { get; set; } = new();

    /// <summary>
    /// Gets the latest backup metadata.
    /// </summary>
    public BackupMetadata? GetLatest()
    {
        return Backups.OrderByDescending(b => b.Version).FirstOrDefault();
    }

    /// <summary>
    /// Gets a specific backup by version number.
    /// </summary>
    public BackupMetadata? GetByVersion(int version)
    {
        return Backups.FirstOrDefault(b => b.Version == version);
    }
}
