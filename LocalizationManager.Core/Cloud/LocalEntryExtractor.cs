// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Extracts LocalEntry objects from resource files for sync operations.
/// </summary>
public class LocalEntryExtractor
{
    private readonly IResourceBackend _backend;

    public LocalEntryExtractor(IResourceBackend backend)
    {
        _backend = backend;
    }

    /// <summary>
    /// Extracts all entries from language files with computed hashes.
    /// </summary>
    /// <param name="languages">Language files to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of local entries with hashes</returns>
    public async Task<List<LocalEntry>> ExtractEntriesAsync(
        IEnumerable<LanguageInfo> languages,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LocalEntry>();

        foreach (var lang in languages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resourceFile = await _backend.Reader.ReadAsync(lang, cancellationToken);

            foreach (var entry in resourceFile.Entries)
            {
                string hash;
                if (entry.IsPlural && entry.PluralForms != null && entry.PluralForms.Count > 0)
                {
                    hash = EntryHasher.ComputePluralHash(entry.PluralForms, entry.Comment);
                }
                else
                {
                    hash = EntryHasher.ComputeHash(entry.Value ?? string.Empty, entry.Comment);
                }

                entries.Add(new LocalEntry
                {
                    Key = entry.Key,
                    Lang = lang.Code,
                    Value = entry.Value ?? string.Empty,
                    Comment = entry.Comment,
                    IsPlural = entry.IsPlural,
                    PluralForms = entry.PluralForms,
                    Hash = hash
                });
            }
        }

        return entries;
    }

    /// <summary>
    /// Extracts entries as a dictionary for quick lookup.
    /// </summary>
    /// <param name="languages">Language files to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary keyed by (Key, Lang)</returns>
    public async Task<Dictionary<(string Key, string Lang), LocalEntry>> ExtractEntriesAsDictionaryAsync(
        IEnumerable<LanguageInfo> languages,
        CancellationToken cancellationToken = default)
    {
        var entries = await ExtractEntriesAsync(languages, cancellationToken);
        return entries.ToDictionary(e => (e.Key, e.Lang), e => e);
    }

    /// <summary>
    /// Extracts entries grouped by key.
    /// </summary>
    /// <param name="languages">Language files to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary keyed by Key, with all language translations</returns>
    public async Task<Dictionary<string, List<LocalEntry>>> ExtractEntriesByKeyAsync(
        IEnumerable<LanguageInfo> languages,
        CancellationToken cancellationToken = default)
    {
        var entries = await ExtractEntriesAsync(languages, cancellationToken);
        return entries
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
