// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LrmCloud.Shared.DTOs.Sync;

#region Push DTOs

/// <summary>
/// Request for key-level push operation.
/// </summary>
public class KeySyncPushRequest
{
    /// <summary>
    /// Entry changes to push (additions and modifications).
    /// </summary>
    public List<EntryChangeDto> Entries { get; set; } = new();

    /// <summary>
    /// Entries to delete.
    /// </summary>
    public List<EntryDeletionDto> Deletions { get; set; } = new();

    /// <summary>
    /// Configuration changes to push.
    /// </summary>
    public ConfigChangesDto? Config { get; set; }

    /// <summary>
    /// Optional message describing this push (for audit trail).
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Represents a single entry change (add or modify).
/// </summary>
public class EntryChangeDto
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
    /// For plural keys, the source plural text pattern (PO msgid_plural or "other" form).
    /// Only set when pushing from source/default language.
    /// </summary>
    public string? SourcePluralText { get; set; }

    /// <summary>
    /// Hash of the entry from last sync (for conflict detection).
    /// Null if this is a new entry.
    /// </summary>
    public string? BaseHash { get; set; }
}

/// <summary>
/// Represents an entry to delete.
/// </summary>
public class EntryDeletionDto
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
public class ConfigChangesDto
{
    /// <summary>
    /// List of property changes.
    /// </summary>
    public List<ConfigPropertyChangeDto> Changes { get; set; } = new();

    /// <summary>
    /// List of property deletions.
    /// </summary>
    public List<ConfigPropertyDeletionDto> Deletions { get; set; } = new();
}

/// <summary>
/// Represents a single configuration property change.
/// </summary>
public class ConfigPropertyChangeDto
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
/// Represents a config property to delete.
/// </summary>
public class ConfigPropertyDeletionDto
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
    public List<EntryConflictDto> Conflicts { get; set; } = new();

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
    public List<EntryDataDto> Entries { get; set; } = new();

    /// <summary>
    /// Keys that have been deleted since the 'since' timestamp (for incremental sync).
    /// </summary>
    public List<string> DeletedKeys { get; set; } = new();

    /// <summary>
    /// Configuration properties.
    /// </summary>
    public ConfigDataDto? Config { get; set; }

    /// <summary>
    /// The project's default language code (e.g., "en").
    /// Used by CLI to identify which translations belong to the default language file.
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
public class EntryDataDto
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
    /// For plural keys, the source plural text pattern (PO msgid_plural or "other" form).
    /// </summary>
    public string? SourcePluralText { get; set; }

    /// <summary>
    /// Translations by language code.
    /// </summary>
    public Dictionary<string, TranslationDataDto> Translations { get; set; } = new();
}

/// <summary>
/// Represents a translation for a specific language.
/// </summary>
public class TranslationDataDto
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
public class ConfigDataDto
{
    /// <summary>
    /// Configuration properties with their hashes.
    /// Key: property path, Value: property data
    /// </summary>
    public Dictionary<string, ConfigPropertyDataDto> Properties { get; set; } = new();
}

/// <summary>
/// Represents a configuration property value.
/// </summary>
public class ConfigPropertyDataDto
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
public class EntryConflictDto
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
    public string Type { get; set; } = "BothModified";

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
    public List<ConflictResolutionDto> Resolutions { get; set; } = new();
}

/// <summary>
/// Resolution for a single conflict.
/// </summary>
public class ConflictResolutionDto
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
    public string TargetType { get; set; } = "Entry";

    /// <summary>
    /// How to resolve the conflict: Local, Remote, Edit, Skip
    /// </summary>
    public string Resolution { get; set; } = "Local";

    /// <summary>
    /// Custom value if Resolution is Edit.
    /// </summary>
    public string? EditedValue { get; set; }
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
    /// Source of the sync: "cli", "web-edit", "github".
    /// </summary>
    public string Source { get; set; } = "cli";

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
    public required SyncHistoryDto History { get; set; }

    /// <summary>
    /// Number of entries restored.
    /// </summary>
    public int EntriesRestored { get; set; }
}

#endregion
