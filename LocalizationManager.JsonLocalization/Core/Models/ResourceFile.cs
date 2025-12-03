// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Core.Models;

/// <summary>
/// Represents a complete JSON localization file with all its entries.
/// </summary>
public class ResourceFile
{
    /// <summary>
    /// Language information for this resource file.
    /// </summary>
    public required LanguageInfo Language { get; set; }

    /// <summary>
    /// Collection of all resource entries in this file.
    /// </summary>
    public List<ResourceEntry> Entries { get; set; } = new();

    /// <summary>
    /// Gets the number of entries in this resource file.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    /// Gets the number of non-empty entries.
    /// </summary>
    public int CompletedCount => Entries.Count(e => !e.IsEmpty);

    /// <summary>
    /// Gets the translation completion percentage.
    /// </summary>
    public double CompletionPercentage => Count > 0 ? (double)CompletedCount / Count * 100 : 0;

    /// <summary>
    /// Attempts to find an entry by key.
    /// </summary>
    public ResourceEntry? FindEntry(string key) => Entries.FirstOrDefault(e => e.Key == key);
}
