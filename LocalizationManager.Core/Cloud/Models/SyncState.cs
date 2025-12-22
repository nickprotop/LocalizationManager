// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Cloud.Models;

/// <summary>
/// Represents the sync state for key-level synchronization.
/// Stored in .lrm/sync-state.json (git-ignored).
/// </summary>
public class SyncState
{
    /// <summary>
    /// Schema version for migration support.
    /// Version 1: File-based (legacy)
    /// Version 2: Entry-based (current)
    /// </summary>
    public int Version { get; set; } = 2;

    /// <summary>
    /// Timestamp of the last successful sync operation.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Entry-level tracking for translations.
    /// Key: Resource key name
    /// Value: Dictionary of language code to entry hash
    /// Example: { "WelcomeMessage": { "en": "abc123", "fr": "def456" } }
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Entries { get; set; } = new();

    /// <summary>
    /// Property-level tracking for configuration (lrm.json).
    /// Key: Property path (e.g., "defaultLanguage", "translation.provider")
    /// Value: SHA256 hash of the property value
    /// </summary>
    public Dictionary<string, string> ConfigProperties { get; set; } = new();

    #region Legacy Properties (for migration from v1)

    /// <summary>
    /// [DEPRECATED - v1 only] SHA256 hash of the lrm.json configuration file.
    /// Kept for backward compatibility during migration.
    /// </summary>
    public string? ConfigHash { get; set; }

    /// <summary>
    /// [DEPRECATED - v1 only] Dictionary mapping file paths to their SHA256 hashes.
    /// Kept for backward compatibility during migration.
    /// </summary>
    public Dictionary<string, string>? Files { get; set; }

    #endregion

    /// <summary>
    /// Gets or sets the hash for a specific entry.
    /// </summary>
    /// <param name="key">Resource key name</param>
    /// <param name="lang">Language code</param>
    /// <returns>Entry hash, or null if not found</returns>
    public string? GetEntryHash(string key, string lang)
    {
        if (Entries.TryGetValue(key, out var langHashes))
        {
            if (langHashes.TryGetValue(lang, out var hash))
            {
                return hash;
            }
        }
        return null;
    }

    /// <summary>
    /// Sets the hash for a specific entry.
    /// </summary>
    /// <param name="key">Resource key name</param>
    /// <param name="lang">Language code</param>
    /// <param name="hash">Entry hash</param>
    public void SetEntryHash(string key, string lang, string hash)
    {
        if (!Entries.ContainsKey(key))
        {
            Entries[key] = new Dictionary<string, string>();
        }
        Entries[key][lang] = hash;
    }

    /// <summary>
    /// Removes the hash for a specific entry.
    /// </summary>
    /// <param name="key">Resource key name</param>
    /// <param name="lang">Language code. If null, removes the entire key.</param>
    public void RemoveEntryHash(string key, string? lang = null)
    {
        if (lang == null)
        {
            Entries.Remove(key);
        }
        else if (Entries.TryGetValue(key, out var langHashes))
        {
            langHashes.Remove(lang);
            if (langHashes.Count == 0)
            {
                Entries.Remove(key);
            }
        }
    }

    /// <summary>
    /// Checks if this is a legacy v1 sync state that needs migration.
    /// </summary>
    public bool NeedsMigration => Version < 2 || (Files != null && Files.Count > 0 && Entries.Count == 0);

    /// <summary>
    /// Creates a new v2 sync state.
    /// </summary>
    public static SyncState CreateNew()
    {
        return new SyncState
        {
            Version = 2,
            Timestamp = DateTime.UtcNow,
            Entries = new Dictionary<string, Dictionary<string, string>>(),
            ConfigProperties = new Dictionary<string, string>()
        };
    }
}
