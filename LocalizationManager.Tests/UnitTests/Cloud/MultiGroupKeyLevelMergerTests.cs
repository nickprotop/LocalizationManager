// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class MultiGroupKeyLevelMergerTests
{
    [Fact]
    public void ComputePushChanges_DistinguishesSameKeyAcrossGroups()
    {
        var locals = new[]
        {
            new LocalEntry { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm", Hash = "h1" },
            new LocalEntry { Key = "OK", BaseName = "SharedResources",   Lang = "", Value = "OK",      Hash = "h2" }
        };

        var merger = new KeyLevelMerger();
        var changes = merger.ComputePushChanges(locals, syncState: null);

        Assert.Equal(2, changes.Additions.Count);
        Assert.Contains(changes.Additions, c => c.Key == "OK" && c.BaseName == "CustomerResources" && c.Value == "Confirm");
        Assert.Contains(changes.Additions, c => c.Key == "OK" && c.BaseName == "SharedResources"   && c.Value == "OK");
    }

    [Fact]
    public void ComputePushChanges_UsesSyncStateBaseHashScopedToGroup()
    {
        var locals = new[]
        {
            new LocalEntry { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm v2", Hash = "h-new" }
        };

        var state = SyncState.CreateNew();
        state.SetEntryHash("CustomerResources", "OK", "", "h-old");
        // Also a hash for the same key in a different group: should be flagged as deleted (no local entry covers it).
        state.SetEntryHash("SharedResources", "OK", "", "h-other");

        var merger = new KeyLevelMerger();
        var changes = merger.ComputePushChanges(locals, state);

        Assert.Single(changes.Modifications);
        Assert.Empty(changes.Additions);
        Assert.Single(changes.Deletions);
        Assert.Equal("SharedResources", changes.Deletions[0].BaseName);
    }

    [Fact]
    public void MergeForPull_ScopesConflictsToGroup()
    {
        var locals = new[]
        {
            new LocalEntry { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm-local", Hash = "h-local" }
        };

        var remotes = new[]
        {
            new EntryData
            {
                Key = "OK",
                BaseName = "CustomerResources",
                Translations =
                {
                    [""] = new TranslationData { Value = "Confirm-remote", Hash = "h-remote" }
                }
            }
        };

        var merger = new KeyLevelMerger();
        var result = merger.MergeForPull(locals, remotes, syncState: null);

        Assert.Single(result.Conflicts);
        Assert.Equal("CustomerResources", result.Conflicts[0].BaseName);
        Assert.Equal("Confirm-local", result.Conflicts[0].LocalValue);
        Assert.Equal("Confirm-remote", result.Conflicts[0].RemoteValue);
    }
}
