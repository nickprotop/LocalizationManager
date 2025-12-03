// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Core.Models;

/// <summary>
/// Represents a single resource key-value pair from a JSON localization file.
/// </summary>
public class ResourceEntry
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The resource value/translation.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Optional comment associated with the resource entry.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Indicates if this entry is empty/null.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Indicates if this entry contains plural forms (JSON-serialized plural object).
    /// </summary>
    public bool IsPlural => Comment == "[plural]";
}
