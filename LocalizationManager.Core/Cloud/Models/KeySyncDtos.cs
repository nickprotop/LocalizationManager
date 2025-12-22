// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json.Serialization;

namespace LocalizationManager.Core.Cloud.Models;

#region Push DTOs

/// <summary>
/// Request for key-level push operation.
/// </summary>
public class KeySyncPushRequest
{
    /// <summary>
    /// Entry changes to push (additions and modifications).
    /// </summary>
    public List<EntryChange> Entries { get; set; } = new();

    /// <summary>
    /// Entries to delete.
    /// </summary>
    public List<EntryDeletion> Deletions { get; set; } = new();

    /// <summary>
    /// Configuration changes to push.
    /// </summary>
    public ConfigChanges? Config { get; set; }

    /// <summary>
    /// Optional message describing this push (for audit trail).
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Represents a single entry change (add or modify).
/// </summary>
public class EntryChange
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code (e.g., "en", "fr", "es").
    /// </summary>
    public required string Lang { get; set; }

    /// <summary>
    /// The translation value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Optional comment for this translation.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Whether this is a plural entry.
    /// </summary>
    public bool IsPlural { get; set; }

    /// <summary>
    /// Plural forms if IsPlural is true.
    /// Key: plural category (zero, one, two, few, many, other)
    /// Value: translation for that category
    /// </summary>
    public Dictionary<string, string>? PluralForms { get; set; }

    /// <summary>
    /// Hash of the entry from last sync (for conflict detection).
    /// Null if this is a new entry.
    /// </summary>
    public string? BaseHash { get; set; }
}

/// <summary>
/// Represents an entry to delete.
/// </summary>
public class EntryDeletion
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code. If null, deletes the key and all its translations.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Hash of the entry from last sync (for conflict detection).
    /// </summary>
    public required string BaseHash { get; set; }
}

/// <summary>
/// Configuration changes for push.
/// </summary>
public class ConfigChanges
{
    /// <summary>
    /// List of property changes (additions and modifications).
    /// </summary>
    public List<ConfigPropertyChange> Changes { get; set; } = new();

    /// <summary>
    /// List of property deletions.
    /// </summary>
    public List<ConfigPropertyDeletion> Deletions { get; set; } = new();

    /// <summary>
    /// Whether there are any changes.
    /// </summary>
    public bool HasChanges => Changes.Count > 0 || Deletions.Count > 0;
}

/// <summary>
/// Represents a config property to delete.
/// </summary>
public class ConfigPropertyDeletion
{
    /// <summary>
    /// Property path to delete.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Hash of the property from last sync (for conflict detection).
    /// </summary>
    public required string BaseHash { get; set; }
}

/// <summary>
/// Represents a single configuration property change.
/// </summary>
public class ConfigPropertyChange
{
    /// <summary>
    /// Property path (e.g., "defaultLanguage", "translation.provider").
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// New value (JSON serialized).
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Hash of the property from last sync (for conflict detection).
    /// Null if this is a new property.
    /// </summary>
    public string? BaseHash { get; set; }
}

/// <summary>
/// Response for key-level push operation.
/// </summary>
public class KeySyncPushResponse
{
    /// <summary>
    /// Number of entries successfully applied.
    /// </summary>
    public int Applied { get; set; }

    /// <summary>
    /// Number of entries deleted.
    /// </summary>
    public int Deleted { get; set; }

    /// <summary>
    /// Whether configuration was applied.
    /// </summary>
    public bool ConfigApplied { get; set; }

    /// <summary>
    /// List of conflicts that need resolution.
    /// </summary>
    public List<EntryConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// New hashes for successfully applied entries.
    /// Key: entry key name, Value: { languageCode: hash }
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> NewEntryHashes { get; set; } = new();

    /// <summary>
    /// New hashes for successfully applied config properties.
    /// Key: property path, Value: hash
    /// </summary>
    public Dictionary<string, string> NewConfigHashes { get; set; } = new();
}

#endregion

#region Pull DTOs

/// <summary>
/// Response for key-level pull operation.
/// </summary>
public class KeySyncPullResponse
{
    /// <summary>
    /// All entries in the project.
    /// </summary>
    public List<EntryData> Entries { get; set; } = new();

    /// <summary>
    /// Keys that have been deleted since the 'since' timestamp (for incremental sync).
    /// </summary>
    public List<string> DeletedKeys { get; set; } = new();

    /// <summary>
    /// Configuration properties.
    /// </summary>
    public ConfigData? Config { get; set; }

    /// <summary>
    /// The project's default language code (e.g., "en").
    /// Used to identify which translations belong to the default language file.
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// Timestamp to use for next delta sync.
    /// </summary>
    public DateTime SyncTimestamp { get; set; }

    /// <summary>
    /// Whether this is an incremental response (delta sync).
    /// </summary>
    public bool IsIncremental { get; set; }

    /// <summary>
    /// Total number of entries (for pagination).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Whether there are more entries to fetch.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
/// Represents a resource key with all its translations.
/// </summary>
public class EntryData
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Comment for the key.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Whether this key has plural forms.
    /// </summary>
    public bool IsPlural { get; set; }

    /// <summary>
    /// Translations by language code.
    /// </summary>
    public Dictionary<string, TranslationData> Translations { get; set; } = new();
}

/// <summary>
/// Represents a translation for a specific language.
/// </summary>
public class TranslationData
{
    /// <summary>
    /// The translation value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Comment for this translation.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// SHA256 hash of the entry.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Translation status (pending, translated, reviewed, approved).
    /// </summary>
    public string Status { get; set; } = "translated";

    /// <summary>
    /// When this translation was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Plural forms if IsPlural is true on the parent key.
    /// </summary>
    public Dictionary<string, string>? PluralForms { get; set; }
}

/// <summary>
/// Configuration data from the server.
/// </summary>
public class ConfigData
{
    /// <summary>
    /// Configuration properties with their hashes.
    /// Key: property path, Value: property data
    /// </summary>
    public Dictionary<string, ConfigPropertyData> Properties { get; set; } = new();
}

/// <summary>
/// Represents a configuration property value.
/// </summary>
public class ConfigPropertyData
{
    /// <summary>
    /// The property value (JSON serialized).
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// SHA256 hash of the property value.
    /// </summary>
    public required string Hash { get; set; }
}

#endregion

#region Conflict DTOs

/// <summary>
/// Represents a conflict between local and remote entry.
/// </summary>
public class EntryConflict
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code.
    /// </summary>
    public required string Lang { get; set; }

    /// <summary>
    /// Type of conflict.
    /// </summary>
    public ConflictType Type { get; set; }

    /// <summary>
    /// Local value (what the user is trying to push).
    /// </summary>
    public string? LocalValue { get; set; }

    /// <summary>
    /// Remote value (current server state).
    /// </summary>
    public string? RemoteValue { get; set; }

    /// <summary>
    /// Hash of remote value.
    /// </summary>
    public string? RemoteHash { get; set; }

    /// <summary>
    /// When the remote value was last updated.
    /// </summary>
    public DateTime? RemoteUpdatedAt { get; set; }

    /// <summary>
    /// Who updated the remote value.
    /// </summary>
    public string? RemoteUpdatedBy { get; set; }
}

/// <summary>
/// Represents a conflict in configuration.
/// </summary>
public class ConfigConflict
{
    /// <summary>
    /// Property path.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Type of conflict.
    /// </summary>
    public ConflictType Type { get; set; }

    /// <summary>
    /// Local value.
    /// </summary>
    public string? LocalValue { get; set; }

    /// <summary>
    /// Remote value.
    /// </summary>
    public string? RemoteValue { get; set; }

    /// <summary>
    /// Hash of remote value.
    /// </summary>
    public string? RemoteHash { get; set; }
}

/// <summary>
/// Types of conflicts that can occur during sync.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConflictType
{
    /// <summary>
    /// Both local and remote modified the same entry to different values.
    /// </summary>
    BothModified,

    /// <summary>
    /// Entry was deleted locally but modified remotely.
    /// </summary>
    DeletedLocallyModifiedRemotely,

    /// <summary>
    /// Entry was deleted remotely but modified locally.
    /// </summary>
    DeletedRemotelyModifiedLocally
}

#endregion

#region Conflict Resolution DTOs

/// <summary>
/// Request to resolve conflicts.
/// </summary>
public class ConflictResolutionRequest
{
    /// <summary>
    /// List of conflict resolutions.
    /// </summary>
    public List<ConflictResolution> Resolutions { get; set; } = new();
}

/// <summary>
/// Resolution for a single conflict.
/// </summary>
public class ConflictResolution
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code. Null for config conflicts.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Whether this is an entry or config conflict.
    /// </summary>
    public ResolutionTargetType TargetType { get; set; }

    /// <summary>
    /// How to resolve the conflict.
    /// </summary>
    public ResolutionChoice Resolution { get; set; }

    /// <summary>
    /// Custom value if Resolution is Edit.
    /// </summary>
    public string? EditedValue { get; set; }
}

/// <summary>
/// Target type for conflict resolution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResolutionTargetType
{
    Entry,
    Config
}

/// <summary>
/// How to resolve a conflict.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResolutionChoice
{
    /// <summary>
    /// Keep the local value.
    /// </summary>
    Local,

    /// <summary>
    /// Accept the remote value.
    /// </summary>
    Remote,

    /// <summary>
    /// Use a custom edited value.
    /// </summary>
    Edit,

    /// <summary>
    /// Skip this conflict (abort for this entry).
    /// </summary>
    Skip
}

/// <summary>
/// Response for conflict resolution.
/// </summary>
public class ConflictResolutionResponse
{
    /// <summary>
    /// Number of conflicts resolved.
    /// </summary>
    public int Applied { get; set; }

    /// <summary>
    /// New hashes after resolution.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> NewHashes { get; set; } = new();
}

#endregion

#region Merge Result DTOs (CLI-side)

/// <summary>
/// Result of merging local and remote entries.
/// </summary>
public class MergeResult
{
    /// <summary>
    /// Entries that should be written to local files (accepted from remote or resolved).
    /// </summary>
    public List<MergedEntry> ToWrite { get; set; } = new();

    /// <summary>
    /// Entries that need user resolution (conflicts).
    /// </summary>
    public List<EntryConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Configuration properties to update locally.
    /// </summary>
    public Dictionary<string, string> ConfigToWrite { get; set; } = new();

    /// <summary>
    /// Configuration conflicts.
    /// </summary>
    public List<ConfigConflict> ConfigConflicts { get; set; } = new();

    /// <summary>
    /// New entry hashes to save to sync-state.json.
    /// Uses helper class for easy manipulation during merge.
    /// </summary>
    public MergeHashes NewHashes { get; } = new();

    /// <summary>
    /// New config property hashes to save to sync-state.json.
    /// </summary>
    public Dictionary<string, string> NewConfigHashes { get; set; } = new();

    /// <summary>
    /// Number of entries auto-merged (no conflict).
    /// </summary>
    public int AutoMerged { get; set; }

    /// <summary>
    /// Number of entries unchanged (same local and remote).
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// Whether there are unresolved conflicts.
    /// </summary>
    public bool HasConflicts => Conflicts.Count > 0 || ConfigConflicts.Count > 0;
}

/// <summary>
/// Helper class to track entry hashes during merge operations.
/// </summary>
public class MergeHashes
{
    private readonly Dictionary<string, Dictionary<string, string>> _entries = new();

    public void SetEntryHash(string key, string lang, string hash)
    {
        if (!_entries.ContainsKey(key))
        {
            _entries[key] = new Dictionary<string, string>();
        }
        _entries[key][lang] = hash;
    }

    public string? GetEntryHash(string key, string lang)
    {
        if (_entries.TryGetValue(key, out var langHashes))
        {
            if (langHashes.TryGetValue(lang, out var hash))
            {
                return hash;
            }
        }
        return null;
    }

    public void RemoveEntryHash(string key, string? lang = null)
    {
        if (lang == null)
        {
            _entries.Remove(key);
        }
        else if (_entries.TryGetValue(key, out var langHashes))
        {
            langHashes.Remove(lang);
            if (langHashes.Count == 0)
            {
                _entries.Remove(key);
            }
        }
    }

    public Dictionary<string, Dictionary<string, string>> ToDictionary() =>
        new(_entries.ToDictionary(
            kvp => kvp.Key,
            kvp => new Dictionary<string, string>(kvp.Value)));

    public IEnumerable<(string Key, string Lang, string Hash)> GetAllEntries()
    {
        foreach (var (key, langHashes) in _entries)
        {
            foreach (var (lang, hash) in langHashes)
            {
                yield return (key, lang, hash);
            }
        }
    }
}

/// <summary>
/// A merged entry ready to write to file.
/// </summary>
public class MergedEntry
{
    /// <summary>
    /// Resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code.
    /// </summary>
    public required string Lang { get; set; }

    /// <summary>
    /// Value to write.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Comment to write.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Whether this is a plural entry.
    /// </summary>
    public bool IsPlural { get; set; }

    /// <summary>
    /// Plural forms if IsPlural is true.
    /// </summary>
    public Dictionary<string, string>? PluralForms { get; set; }

    /// <summary>
    /// Hash of this entry (for sync-state).
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Source of this entry (local, remote, or edited).
    /// </summary>
    public MergeSource Source { get; set; }
}

/// <summary>
/// Source of a merged entry.
/// </summary>
public enum MergeSource
{
    /// <summary>
    /// Kept from local (no change or only local changed).
    /// </summary>
    Local,

    /// <summary>
    /// Accepted from remote (only remote changed).
    /// </summary>
    Remote,

    /// <summary>
    /// User edited value.
    /// </summary>
    Edited,

    /// <summary>
    /// Both changed to same value (no conflict).
    /// </summary>
    BothSame
}

#endregion

#region History DTOs

/// <summary>
/// Response for listing sync history.
/// </summary>
public class SyncHistoryListResponse
{
    /// <summary>
    /// List of history entries.
    /// </summary>
    public List<SyncHistoryDto> Items { get; set; } = new();

    /// <summary>
    /// Total number of history entries.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Whether there are more pages.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
/// Summary of a sync history entry.
/// </summary>
public class SyncHistoryDto
{
    /// <summary>
    /// Short unique ID for this history entry (e.g., "abc12345").
    /// </summary>
    public required string HistoryId { get; set; }

    /// <summary>
    /// Type of operation: "push", "revert".
    /// </summary>
    public required string OperationType { get; set; }

    /// <summary>
    /// User-provided message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Email of user who performed the operation.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Display name of user.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Number of entries added.
    /// </summary>
    public int EntriesAdded { get; set; }

    /// <summary>
    /// Number of entries modified.
    /// </summary>
    public int EntriesModified { get; set; }

    /// <summary>
    /// Number of entries deleted.
    /// </summary>
    public int EntriesDeleted { get; set; }

    /// <summary>
    /// Status: "completed", "reverted".
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// When this operation was performed.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Full details of a sync history entry including changes.
/// </summary>
public class SyncHistoryDetailDto : SyncHistoryDto
{
    /// <summary>
    /// List of changes made in this operation.
    /// </summary>
    public List<SyncChangeDto> Changes { get; set; } = new();

    /// <summary>
    /// If this was a revert, the history ID that was reverted.
    /// </summary>
    public string? RevertedFromId { get; set; }
}

/// <summary>
/// Represents a single change in a sync operation.
/// </summary>
public class SyncChangeDto
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Language code.
    /// </summary>
    public required string Lang { get; set; }

    /// <summary>
    /// Type of change: "added", "modified", "deleted".
    /// </summary>
    public required string ChangeType { get; set; }

    /// <summary>
    /// Value before the change (null for additions).
    /// </summary>
    public string? BeforeValue { get; set; }

    /// <summary>
    /// Value after the change (null for deletions).
    /// </summary>
    public string? AfterValue { get; set; }

    /// <summary>
    /// Comment before the change.
    /// </summary>
    public string? BeforeComment { get; set; }

    /// <summary>
    /// Comment after the change.
    /// </summary>
    public string? AfterComment { get; set; }
}

/// <summary>
/// Request to revert to a specific history entry.
/// </summary>
public class RevertRequest
{
    /// <summary>
    /// Optional message describing why the revert was done.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Response for a revert operation.
/// </summary>
public class RevertResponse
{
    /// <summary>
    /// Whether the revert was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The new history entry created for the revert.
    /// </summary>
    public SyncHistoryDto? History { get; set; }

    /// <summary>
    /// Number of entries restored.
    /// </summary>
    public int EntriesRestored { get; set; }
}

#endregion
