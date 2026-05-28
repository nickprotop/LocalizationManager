// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json.Serialization;

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
    /// Version 2: Entry-based, keyed by (Key, Lang)
    /// Version 3: Entry-based, keyed by (BaseName, Key, Lang) — multi-group aware
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// Timestamp of the last successful sync operation.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Entry-level tracking keyed by BaseName → Key → Lang → hash.
    /// BaseName "" = default/no-group (legacy single-group projects).
    /// </summary>
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> EntriesV3 { get; set; } = new();

    /// <summary>
    /// Property-level tracking for configuration (lrm.json).
    /// Key: Property path (e.g., "defaultLanguage", "translation.provider")
    /// Value: SHA256 hash of the property value
    /// </summary>
    public Dictionary<string, string> ConfigProperties { get; set; } = new();

    #region Legacy Properties (for migration from v1/v2)

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

    /// <summary>
    /// [DEPRECATED - v2 only] Entry-level tracking keyed by (Key, Lang).
    /// Auto-migrated to <see cref="EntriesV3"/> on load.
    /// Kept for backward compatibility (JSON deserialization).
    /// </summary>
    [JsonPropertyName("Entries")]
    public Dictionary<string, Dictionary<string, string>>? LegacyEntries { get; set; }

    #endregion

    #region Multi-group (v3) accessors

    /// <summary>
    /// Gets the hash for a specific entry. Multi-group-aware overload.
    /// </summary>
    public string? GetEntryHash(string baseName, string key, string lang)
    {
        if (EntriesV3.TryGetValue(baseName, out var byKey)
            && byKey.TryGetValue(key, out var byLang)
            && byLang.TryGetValue(lang, out var hash))
        {
            return hash;
        }
        return null;
    }

    /// <summary>
    /// Sets the hash for a specific entry. Multi-group-aware overload.
    /// </summary>
    public void SetEntryHash(string baseName, string key, string lang, string hash)
    {
        if (!EntriesV3.TryGetValue(baseName, out var byKey))
        {
            EntriesV3[baseName] = byKey = new Dictionary<string, Dictionary<string, string>>();
        }
        if (!byKey.TryGetValue(key, out var byLang))
        {
            byKey[key] = byLang = new Dictionary<string, string>();
        }
        byLang[lang] = hash;
    }

    /// <summary>
    /// Removes the hash for a specific entry. Multi-group-aware overload.
    /// </summary>
    public void RemoveEntryHash(string baseName, string key, string? lang = null)
    {
        if (!EntriesV3.TryGetValue(baseName, out var byKey)) return;
        if (lang == null)
        {
            byKey.Remove(key);
        }
        else if (byKey.TryGetValue(key, out var byLang))
        {
            byLang.Remove(lang);
            if (byLang.Count == 0) byKey.Remove(key);
        }
        if (byKey.Count == 0) EntriesV3.Remove(baseName);
    }

    #endregion

    #region Legacy (single-group) overloads — delegate to BaseName=""

    /// <summary>
    /// Gets the hash for a specific entry (legacy, single-group). Treats the
    /// entry as belonging to BaseName="".
    /// </summary>
    public string? GetEntryHash(string key, string lang) => GetEntryHash(string.Empty, key, lang);

    /// <summary>
    /// Sets the hash for a specific entry (legacy, single-group). Stores under
    /// BaseName="".
    /// </summary>
    public void SetEntryHash(string key, string lang, string hash) => SetEntryHash(string.Empty, key, lang, hash);

    /// <summary>
    /// Removes the hash for a specific entry (legacy, single-group). Removes
    /// from BaseName="".
    /// </summary>
    public void RemoveEntryHash(string key, string? lang = null) => RemoveEntryHash(string.Empty, key, lang);

    #endregion

    /// <summary>
    /// Legacy v2-shape view over <see cref="EntriesV3"/> for callers that
    /// haven't been updated yet. Returns the BaseName="" group's entries
    /// (single-group projects only).
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, Dictionary<string, string>> Entries =>
        EntriesV3.TryGetValue(string.Empty, out var byKey) ? byKey : new();

    /// <summary>
    /// Iterates every entry hash with its full (BaseName, Key, Lang) key.
    /// </summary>
    public IEnumerable<(string BaseName, string Key, string Lang, string Hash)> EnumerateEntries()
    {
        foreach (var (baseName, byKey) in EntriesV3)
        {
            foreach (var (key, byLang) in byKey)
            {
                foreach (var (lang, hash) in byLang)
                {
                    yield return (baseName, key, lang, hash);
                }
            }
        }
    }

    /// <summary>
    /// Checks if this state needs migration (v1 → v2 or v2 → v3).
    /// </summary>
    public bool NeedsMigration =>
        Version < 3
        || (LegacyEntries != null && LegacyEntries.Count > 0 && EntriesV3.Count == 0)
        || (Files != null && Files.Count > 0 && EntriesV3.Count == 0);

    /// <summary>
    /// Migrates a v2 sync state (Entries keyed by Key → Lang → hash) to v3
    /// (EntriesV3 keyed by BaseName → Key → Lang → hash). All v2 entries
    /// are assigned BaseName="" since that data predates multi-group support.
    /// </summary>
    public static SyncState MigrateToV3(SyncState old)
    {
        var migrated = CreateNew();
        migrated.Timestamp = old.Timestamp;
        migrated.ConfigProperties = old.ConfigProperties;

        // Copy v3 entries directly (if any).
        foreach (var (baseName, byKey) in old.EntriesV3)
        {
            foreach (var (key, byLang) in byKey)
            {
                foreach (var (lang, hash) in byLang)
                {
                    migrated.SetEntryHash(baseName, key, lang, hash);
                }
            }
        }

        // Copy legacy v2 entries into BaseName="".
        if (old.LegacyEntries != null)
        {
            foreach (var (key, byLang) in old.LegacyEntries)
            {
                foreach (var (lang, hash) in byLang)
                {
                    migrated.SetEntryHash(string.Empty, key, lang, hash);
                }
            }
        }

        return migrated;
    }

    /// <summary>
    /// Creates a new v3 sync state.
    /// </summary>
    public static SyncState CreateNew()
    {
        return new SyncState
        {
            Version = 3,
            Timestamp = DateTime.UtcNow,
            EntriesV3 = new(),
            ConfigProperties = new()
        };
    }
}
