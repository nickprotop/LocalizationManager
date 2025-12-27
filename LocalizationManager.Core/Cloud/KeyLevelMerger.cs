// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Performs three-way merge at the key/entry level for sync operations.
/// </summary>
public class KeyLevelMerger
{
    /// <summary>
    /// Computes changes between local entries and last sync state (for push).
    /// </summary>
    /// <param name="localEntries">Current local entries with computed hashes</param>
    /// <param name="syncState">Last sync state (null for first push)</param>
    /// <returns>Changes to push and deletions</returns>
    public PushChanges ComputePushChanges(
        IEnumerable<LocalEntry> localEntries,
        SyncState? syncState)
    {
        var result = new PushChanges();
        var localEntriesByKey = localEntries
            .GroupBy(e => (e.Key, e.Lang))
            .ToDictionary(g => g.Key, g => g.First());

        // Track which keys we've seen locally
        var seenKeys = new HashSet<(string Key, string Lang)>();

        foreach (var entry in localEntries)
        {
            var keyPair = (entry.Key, entry.Lang);
            seenKeys.Add(keyPair);

            var baseHash = syncState?.GetEntryHash(entry.Key, entry.Lang);

            if (baseHash == null)
            {
                // New entry (not in last sync state)
                result.Additions.Add(new EntryChange
                {
                    Key = entry.Key,
                    Lang = entry.Lang,
                    Value = entry.Value,
                    Comment = entry.Comment,
                    IsPlural = entry.IsPlural,
                    PluralForms = entry.PluralForms,
                    BaseHash = null
                });
            }
            else if (baseHash != entry.Hash)
            {
                // Modified entry (hash changed since last sync)
                result.Modifications.Add(new EntryChange
                {
                    Key = entry.Key,
                    Lang = entry.Lang,
                    Value = entry.Value,
                    Comment = entry.Comment,
                    IsPlural = entry.IsPlural,
                    PluralForms = entry.PluralForms,
                    BaseHash = baseHash
                });
            }
            // If baseHash == entry.Hash, entry unchanged, skip it
        }

        // Find deletions: entries in sync state but not in local
        if (syncState != null)
        {
            foreach (var (key, langHashes) in syncState.Entries)
            {
                foreach (var (lang, hash) in langHashes)
                {
                    if (!seenKeys.Contains((key, lang)))
                    {
                        result.Deletions.Add(new EntryDeletion
                        {
                            Key = key,
                            Lang = lang,
                            BaseHash = hash
                        });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Performs three-way merge of remote entries with local state (for pull).
    /// </summary>
    /// <param name="localEntries">Current local entries with computed hashes</param>
    /// <param name="remoteEntries">Entries from server</param>
    /// <param name="syncState">Last sync state (BASE for three-way merge)</param>
    /// <returns>Merge result with entries to write and conflicts</returns>
    public MergeResult MergeForPull(
        IEnumerable<LocalEntry> localEntries,
        IEnumerable<EntryData> remoteEntries,
        SyncState? syncState)
    {
        var result = new MergeResult();

        // Build lookup for local entries
        var localByKey = localEntries
            .GroupBy(e => (e.Key, e.Lang))
            .ToDictionary(g => g.Key, g => g.First());

        // Build lookup for remote entries
        // Note: We use translation.Comment (per-language) rather than entry.Comment (key-level)
        var remoteByKey = new Dictionary<(string Key, string Lang), (string Value, string Hash, string? Comment, bool IsPlural, Dictionary<string, string>? PluralForms)>();
        foreach (var entry in remoteEntries)
        {
            foreach (var (lang, translation) in entry.Translations)
            {
                remoteByKey[(entry.Key, lang)] = (translation.Value, translation.Hash, translation.Comment, entry.IsPlural, translation.PluralForms);
            }
        }

        // Track all keys we need to consider
        var allKeys = new HashSet<(string Key, string Lang)>();
        foreach (var e in localEntries) allKeys.Add((e.Key, e.Lang));
        foreach (var key in remoteByKey.Keys) allKeys.Add(key);
        if (syncState != null)
        {
            foreach (var (key, langHashes) in syncState.Entries)
            {
                foreach (var lang in langHashes.Keys)
                {
                    allKeys.Add((key, lang));
                }
            }
        }

        foreach (var (key, lang) in allKeys)
        {
            var baseHash = syncState?.GetEntryHash(key, lang);
            var hasLocal = localByKey.TryGetValue((key, lang), out var localEntry);
            var hasRemote = remoteByKey.TryGetValue((key, lang), out var remoteEntry);

            var localHash = localEntry?.Hash;
            var remoteHash = hasRemote ? remoteEntry.Hash : null;

            // Three-way merge logic
            if (!hasLocal && !hasRemote)
            {
                // Both deleted (or never existed) - nothing to do
                continue;
            }

            if (!hasLocal && hasRemote)
            {
                // Only exists on remote
                if (baseHash == null)
                {
                    // New from remote - accept
                    result.ToWrite.Add(CreateMergedEntry(key, lang, remoteEntry, MergeSource.Remote));
                    result.NewHashes.SetEntryHash(key, lang, remoteEntry.Hash);
                }
                else if (baseHash == remoteHash)
                {
                    // Remote unchanged, local deleted - keep deleted (don't write)
                    result.NewHashes.RemoveEntryHash(key, lang);
                }
                else
                {
                    // CONFLICT: Deleted locally, modified remotely
                    result.Conflicts.Add(new EntryConflict
                    {
                        Key = key,
                        Lang = lang,
                        Type = ConflictType.DeletedLocallyModifiedRemotely,
                        LocalValue = null,
                        LocalComment = null,
                        RemoteValue = remoteEntry.Value,
                        RemoteComment = remoteEntry.Comment,
                        RemoteHash = remoteEntry.Hash
                    });
                }
            }
            else if (hasLocal && !hasRemote)
            {
                // Only exists locally
                if (baseHash == null)
                {
                    // New locally - keep (will be pushed later)
                    result.NewHashes.SetEntryHash(key, lang, localEntry!.Hash);
                }
                else if (baseHash == localHash)
                {
                    // Local unchanged, remote deleted - delete locally
                    result.NewHashes.RemoveEntryHash(key, lang);
                    // Don't add to ToWrite - file regenerator will omit it
                }
                else
                {
                    // CONFLICT: Deleted remotely, modified locally
                    result.Conflicts.Add(new EntryConflict
                    {
                        Key = key,
                        Lang = lang,
                        Type = ConflictType.DeletedRemotelyModifiedLocally,
                        LocalValue = localEntry!.Value,
                        LocalComment = localEntry.Comment,
                        RemoteValue = null,
                        RemoteComment = null,
                        RemoteHash = null
                    });
                }
            }
            else
            {
                // Both exist
                if (localHash == remoteHash)
                {
                    // Same value (or both unchanged from base)
                    result.Unchanged++;
                    result.NewHashes.SetEntryHash(key, lang, localHash!);
                }
                else if (baseHash == localHash)
                {
                    // Only remote changed - accept remote
                    result.ToWrite.Add(CreateMergedEntry(key, lang, remoteEntry, MergeSource.Remote));
                    result.NewHashes.SetEntryHash(key, lang, remoteHash!);
                    result.AutoMerged++;
                }
                else if (baseHash == remoteHash)
                {
                    // Only local changed - keep local (don't overwrite)
                    result.NewHashes.SetEntryHash(key, lang, localHash!);
                }
                else if (baseHash == null)
                {
                    // Both new (no base) and different - conflict
                    result.Conflicts.Add(new EntryConflict
                    {
                        Key = key,
                        Lang = lang,
                        Type = ConflictType.BothModified,
                        LocalValue = localEntry!.Value,
                        LocalComment = localEntry.Comment,
                        RemoteValue = remoteEntry.Value,
                        RemoteComment = remoteEntry.Comment,
                        RemoteHash = remoteEntry.Hash
                    });
                }
                else
                {
                    // Both changed from base and different - conflict
                    result.Conflicts.Add(new EntryConflict
                    {
                        Key = key,
                        Lang = lang,
                        Type = ConflictType.BothModified,
                        LocalValue = localEntry!.Value,
                        LocalComment = localEntry.Comment,
                        RemoteValue = remoteEntry.Value,
                        RemoteComment = remoteEntry.Comment,
                        RemoteHash = remoteEntry.Hash
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a merge result for first pull (no local state) - accepts all remote.
    /// </summary>
    /// <param name="remoteEntries">Entries from server</param>
    /// <returns>Merge result with all remote entries to write</returns>
    public MergeResult MergeForFirstPull(IEnumerable<EntryData> remoteEntries)
    {
        var result = new MergeResult();

        foreach (var entry in remoteEntries)
        {
            foreach (var (lang, translation) in entry.Translations)
            {
                // Use translation.Comment (per-language) rather than entry.Comment (key-level)
                result.ToWrite.Add(new MergedEntry
                {
                    Key = entry.Key,
                    Lang = lang,
                    Value = translation.Value,
                    Comment = translation.Comment,
                    IsPlural = entry.IsPlural,
                    PluralForms = translation.PluralForms,
                    Hash = translation.Hash,
                    Source = MergeSource.Remote
                });
                result.NewHashes.SetEntryHash(entry.Key, lang, translation.Hash);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies conflict resolutions to a merge result.
    /// </summary>
    /// <param name="mergeResult">Original merge result with conflicts</param>
    /// <param name="resolutions">User's conflict resolutions</param>
    /// <param name="localEntries">Local entries (for getting local values)</param>
    /// <returns>Updated merge result with conflicts resolved</returns>
    public MergeResult ApplyResolutions(
        MergeResult mergeResult,
        IEnumerable<ConflictResolution> resolutions,
        Dictionary<(string Key, string Lang), LocalEntry> localEntries)
    {
        var resolutionByKey = resolutions
            .Where(r => r.TargetType == ResolutionTargetType.Entry)
            .ToDictionary(r => (r.Key, r.Lang ?? ""));

        var remainingConflicts = new List<EntryConflict>();

        foreach (var conflict in mergeResult.Conflicts)
        {
            if (!resolutionByKey.TryGetValue((conflict.Key, conflict.Lang), out var resolution))
            {
                remainingConflicts.Add(conflict);
                continue;
            }

            switch (resolution.Resolution)
            {
                case ResolutionChoice.Local:
                    if (localEntries.TryGetValue((conflict.Key, conflict.Lang), out var localEntry))
                    {
                        var hash = EntryHasher.ComputeHash(localEntry.Value, localEntry.Comment);
                        mergeResult.ToWrite.Add(new MergedEntry
                        {
                            Key = conflict.Key,
                            Lang = conflict.Lang,
                            Value = localEntry.Value,
                            Comment = localEntry.Comment,
                            IsPlural = localEntry.IsPlural,
                            PluralForms = localEntry.PluralForms,
                            Hash = hash,
                            Source = MergeSource.Local
                        });
                        mergeResult.NewHashes.SetEntryHash(conflict.Key, conflict.Lang, hash);
                    }
                    break;

                case ResolutionChoice.Remote:
                    if (conflict.RemoteValue != null)
                    {
                        mergeResult.ToWrite.Add(new MergedEntry
                        {
                            Key = conflict.Key,
                            Lang = conflict.Lang,
                            Value = conflict.RemoteValue,
                            Comment = conflict.RemoteComment,
                            IsPlural = false,
                            PluralForms = null,
                            Hash = conflict.RemoteHash!,
                            Source = MergeSource.Remote
                        });
                        mergeResult.NewHashes.SetEntryHash(conflict.Key, conflict.Lang, conflict.RemoteHash!);
                    }
                    break;

                case ResolutionChoice.Edit:
                    if (!string.IsNullOrEmpty(resolution.EditedValue))
                    {
                        // Use edited comment if provided, otherwise preserve remote comment
                        var comment = resolution.EditedComment ?? conflict.RemoteComment;
                        var hash = EntryHasher.ComputeHash(resolution.EditedValue, comment);
                        mergeResult.ToWrite.Add(new MergedEntry
                        {
                            Key = conflict.Key,
                            Lang = conflict.Lang,
                            Value = resolution.EditedValue,
                            Comment = comment,
                            IsPlural = false,
                            PluralForms = null,
                            Hash = hash,
                            Source = MergeSource.Edited
                        });
                        mergeResult.NewHashes.SetEntryHash(conflict.Key, conflict.Lang, hash);
                    }
                    break;

                case ResolutionChoice.Skip:
                    // Keep the conflict unresolved - it will need to be handled later
                    remainingConflicts.Add(conflict);
                    break;
            }
        }

        mergeResult.Conflicts = remainingConflicts;
        return mergeResult;
    }

    private static MergedEntry CreateMergedEntry(
        string key,
        string lang,
        (string Value, string Hash, string? Comment, bool IsPlural, Dictionary<string, string>? PluralForms) remote,
        MergeSource source)
    {
        return new MergedEntry
        {
            Key = key,
            Lang = lang,
            Value = remote.Value,
            Comment = remote.Comment,
            IsPlural = remote.IsPlural,
            PluralForms = remote.PluralForms,
            Hash = remote.Hash,
            Source = source
        };
    }
}

/// <summary>
/// Represents a local entry with computed hash.
/// </summary>
public class LocalEntry
{
    public required string Key { get; init; }
    public required string Lang { get; init; }
    public required string Value { get; init; }
    public string? Comment { get; init; }
    public bool IsPlural { get; init; }
    public Dictionary<string, string>? PluralForms { get; init; }
    public required string Hash { get; init; }
}

/// <summary>
/// Result of computing push changes.
/// </summary>
public class PushChanges
{
    public List<EntryChange> Additions { get; } = new();
    public List<EntryChange> Modifications { get; } = new();
    public List<EntryDeletion> Deletions { get; } = new();

    /// <summary>
    /// All entry changes (additions + modifications combined).
    /// </summary>
    public IEnumerable<EntryChange> Entries => Additions.Concat(Modifications);

    public int TotalChanges => Additions.Count + Modifications.Count + Deletions.Count;
    public bool HasChanges => TotalChanges > 0;
}
