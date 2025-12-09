// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Cloud.Models;

/// <summary>
/// Represents the sync state tracking file hashes for incremental push.
/// Stored in .lrm/sync-state.json (git-ignored).
/// </summary>
public class SyncState
{
    /// <summary>
    /// Timestamp of the last successful push.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// SHA256 hash of the lrm.json configuration file.
    /// Used to detect configuration changes.
    /// </summary>
    public string? ConfigHash { get; set; }

    /// <summary>
    /// Dictionary mapping file paths to their SHA256 hashes.
    /// Key: Relative file path (e.g., "Resources.resx", "strings.el.json")
    /// Value: SHA256 hash (base64 encoded)
    /// </summary>
    public Dictionary<string, string> Files { get; set; } = new();
}
