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

        var languageList = languages.ToList();

        // Cache reads so a file shared between columns (or read while resolving
        // collisions) is only parsed once.
        var fileCache = new Dictionary<string, ResourceFile>(StringComparer.OrdinalIgnoreCase);
        async Task<ResourceFile> ReadCachedAsync(LanguageInfo lang)
        {
            var path = lang.FilePath ?? string.Empty;
            if (!fileCache.TryGetValue(path, out var file))
            {
                file = await _backend.Reader.ReadAsync(lang, cancellationToken);
                fileCache[path] = file;
            }
            return file;
        }

        // Process each resource group independently so shared key names across
        // groups don't collide, and so column-merging is scoped per group.
        foreach (var group in languageList.GroupBy(l => l.BaseName ?? string.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupLanguages = group.ToList();
            var byPath = groupLanguages
                .Where(l => l.FilePath != null)
                .GroupBy(l => l.FilePath!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Merge the group's files into one column per effective language code.
            // When the suffix-less default file (labeled with the configured
            // DefaultLanguageCode, e.g. "it") and an explicit culture file share
            // the same code, they collapse into a single column. The default file
            // wins; the colliding file is only consulted to gap-fill keys the
            // winner lacks. This mirrors the display merge and ensures push emits
            // exactly one entry per (BaseName, Key, effectiveCode) slot.
            var columns = MergedLanguageColumns.Build(groupLanguages);

            foreach (var column in columns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Winner-first ordered list of files for this column.
                var orderedPaths = new List<string> { column.WinningFilePath };
                orderedPaths.AddRange(column.ConflictingFilePaths);

                var orderedLangs = orderedPaths
                    .Where(p => !string.IsNullOrEmpty(p) && byPath.ContainsKey(p))
                    .Select(p => byPath[p])
                    .ToList();

                if (orderedLangs.Count == 0)
                {
                    continue;
                }

                // The winner determines the emitted Lang/BaseName/IsDefault so the
                // default file's entries still go out under its raw code (e.g. "it"
                // or "" for a blank default) - never the synthetic "default" code.
                var winnerLang = orderedLangs[0];

                // For each distinct key (winner-first order), emit ONE entry from
                // the first file that has it: default-wins + gap-fill.
                var emittedKeys = new HashSet<string>(StringComparer.Ordinal);

                foreach (var fileLang in orderedLangs)
                {
                    var resourceFile = await ReadCachedAsync(fileLang);

                    foreach (var entry in resourceFile.Entries)
                    {
                        if (!emittedKeys.Add(entry.Key))
                        {
                            // A higher-priority file (winner or earlier conflict)
                            // already supplied this key - skip the duplicate.
                            continue;
                        }

                        entries.Add(BuildLocalEntry(entry, winnerLang));
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Builds a <see cref="LocalEntry"/> for a resource entry, labeling it with the
    /// winning language file's code/base-name/default-flag.
    /// </summary>
    private static LocalEntry BuildLocalEntry(ResourceEntry entry, LanguageInfo winnerLang)
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

        return new LocalEntry
        {
            Key = entry.Key,
            BaseName = winnerLang.BaseName ?? string.Empty,
            Lang = winnerLang.Code,
            Value = entry.Value ?? string.Empty,
            Comment = entry.Comment,
            IsPlural = entry.IsPlural,
            PluralForms = entry.PluralForms,
            Hash = hash,
            // Pass SourceText for storage in ResourceKey.SourceText
            // For default language: use entry.SourceText (e.g. msgid for PO) or fall back to Value
            // For non-default language: pass entry.SourceText if available (PO format has msgid in all files)
            SourceText = winnerLang.IsDefault ? (entry.SourceText ?? entry.Value) : entry.SourceText,
            // Pass SourcePluralText (msgid_plural for PO, available in all PO files)
            SourcePluralText = entry.SourcePluralText
        };
    }

    /// <summary>
    /// Extracts entries as a dictionary for quick lookup, keyed by
    /// (BaseName, Key, Lang) so multi-group projects with shared key names
    /// don't collide.
    /// </summary>
    /// <param name="languages">Language files to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary keyed by (BaseName, Key, Lang)</returns>
    public async Task<Dictionary<(string BaseName, string Key, string Lang), LocalEntry>> ExtractEntriesAsDictionaryAsync(
        IEnumerable<LanguageInfo> languages,
        CancellationToken cancellationToken = default)
    {
        var entries = await ExtractEntriesAsync(languages, cancellationToken);
        return entries.ToDictionary(e => (e.BaseName, e.Key, e.Lang), e => e);
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
