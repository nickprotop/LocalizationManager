// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Request to batch save multiple changes with sync history recording.
/// </summary>
public class BatchSaveRequest
{
    /// <summary>
    /// Optional commit message (like CLI push).
    /// Defaults to "Updated via web editor" if not provided.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Key metadata changes (comment updates).
    /// </summary>
    public List<KeyChangeDto> KeyChanges { get; set; } = new();

    /// <summary>
    /// Translation value/status changes.
    /// </summary>
    public List<TranslationChangeDto> TranslationChanges { get; set; } = new();
}

/// <summary>
/// A key metadata change.
/// </summary>
public class KeyChangeDto
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string KeyName { get; set; }

    /// <summary>
    /// New comment for the key (null to clear).
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// A translation value/status change.
/// </summary>
public class TranslationChangeDto
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string KeyName { get; set; }

    /// <summary>
    /// The language code.
    /// </summary>
    public required string LanguageCode { get; set; }

    /// <summary>
    /// New translation value (null to clear).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// New status (null to keep existing).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Plural form category (e.g., "one", "other", "zero").
    /// Empty string or null for non-plural translations.
    /// </summary>
    public string? PluralForm { get; set; }
}

/// <summary>
/// Response from batch save operation.
/// </summary>
public class BatchSaveResponse
{
    /// <summary>
    /// Number of changes applied.
    /// </summary>
    public int Applied { get; set; }

    /// <summary>
    /// Sync history entry ID (e.g., "abc12345").
    /// Null if no history was recorded (e.g., no changes).
    /// </summary>
    public string? HistoryId { get; set; }

    /// <summary>
    /// Number of key metadata changes applied.
    /// </summary>
    public int KeysModified { get; set; }

    /// <summary>
    /// Number of translation changes applied.
    /// </summary>
    public int TranslationsModified { get; set; }
}
