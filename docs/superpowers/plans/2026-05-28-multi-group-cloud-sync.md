# Multi-Group Cloud Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the LRM cloud sync pipeline (push / pull / clone, on both the client CLI and the cloud API/DB) preserve `BaseName` per resource entry, so directories with multiple resource groups (e.g. `CustomerResources.resx` + `SharedResources.resx`) can sync without data loss or key collisions.

**Architecture:** Add `BaseName` (a.k.a. resource group / base resx name) as a first-class disambiguator throughout the sync pipeline:

- **Cloud DB** (`resource_keys` table): add a `base_name TEXT NOT NULL DEFAULT ''` column and change the project-scoped unique index from `(ProjectId, KeyName)` to `(ProjectId, BaseName, KeyName)`. Backfill existing rows with `''`.
- **Wire DTOs** (`LocalEntry`, `MergedEntry`, `EntryChange`, `EntryDeletion`, `EntryData`, `PushRequest`, `PullResponse`): add an optional `BaseName` field (defaulting to `""` for legacy clients).
- **Client sync logic** (`LocalEntryExtractor`, `KeyLevelMerger`, `FileRegenerator`, `SyncState`): every lookup keyed by `(Key, Lang)` becomes `(BaseName, Key, Lang)`. File writes route per-group on pull.
- **Backward compatibility convention**: single-group projects continue to send `BaseName=""`; only multi-group projects emit non-empty `BaseName`. This keeps existing single-group projects (and any old clients still in the wild) syncing transparently.

**Tech Stack:** .NET 9 (LocalizationManager.Core + cloud API), Entity Framework Core + Npgsql, xUnit, Docker Compose (integration tests), Spectre.Console CLI.

**Commits:** Do NOT commit between tasks during execution — the operator will slice the diff into commits after the full plan is implemented and verified. The per-step "Commit" instructions are intentionally omitted.

**Out of scope:**
- Re-keying an already-synced single-group project after it grows a second group. Documented as a follow-up; the user-visible behavior would be that the new group's rows would be created fresh server-side and the old `BaseName=""` rows would remain orphaned. A `lrm cloud migrate-groups` command can be added later.
- GitHub sync (`GitHubSyncState` entity) — that's a parallel sync pathway used only when GitHub integration is configured. Documented as a separate follow-up plan.
- Cloud Web UI changes — the WASM front-end is a downstream consumer and will continue to render whatever the API returns; if it crashes against new payload shapes, a follow-up plan will address it.

---

## File Structure

**New files:**
- `cloud/src/LrmCloud.Api/Data/Migrations/<timestamp>_AddBaseNameToResourceKey.cs` — EF Core migration
- `cloud/src/LrmCloud.Api/Data/Migrations/<timestamp>_AddBaseNameToResourceKey.Designer.cs` — designer (auto-generated)
- `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupFileRegeneratorTests.cs`
- `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupKeyLevelMergerTests.cs`

**Modified files (cloud DB / server):**
- `cloud/src/LrmCloud.Shared/Entities/ResourceKey.cs` — add `BaseName`
- `cloud/src/LrmCloud.Api/Data/AppDbContext.cs:208` — change unique index
- `cloud/src/LrmCloud.Api/Data/Migrations/AppDbContextModelSnapshot.cs` — designer snapshot
- `cloud/src/LrmCloud.Api/Services/KeySyncService.cs` — accept/store/return BaseName
- `cloud/src/LrmCloud.Api/Services/ResourceService.cs` — disambiguate lookups by BaseName
- `cloud/src/LrmCloud.Api/Services/SyncHistoryService.cs` — record BaseName in change rows
- `cloud/src/LrmCloud.Api/Services/FileExportService.cs` — preserve BaseName in exports
- `cloud/src/LrmCloud.Api/Services/FileOperationsService.cs` — preserve BaseName on file imports
- `cloud/src/LrmCloud.Api/Controllers/SyncController.cs` — pass BaseName through endpoints

**Modified files (shared DTOs):**
- `cloud/src/LrmCloud.Shared/DTOs/Sync/PushRequest.cs`
- `cloud/src/LrmCloud.Shared/DTOs/Sync/PullResponse.cs`
- `cloud/src/LrmCloud.Shared/DTOs/Sync/KeySyncDtos.cs` — `EntryData`, `EntryChange`, `EntryDeletion`, `SyncChangeDto`
- `cloud/src/LrmCloud.Shared/DTOs/Resources/PushResourcesRequest.cs`
- `cloud/src/LrmCloud.Shared/DTOs/Resources/PushResourcesResponse.cs`
- `cloud/src/LrmCloud.Shared/Entities/SyncHistory.cs` — `SyncChangeEntry` carries BaseName

**Modified files (local sync core):**
- `LocalizationManager.Core/Cloud/KeyLevelMerger.cs` — `LocalEntry`, key tuples, `ComputePushChanges`, `MergeForPull`
- `LocalizationManager.Core/Cloud/Models/KeySyncDtos.cs` — `MergedEntry`
- `LocalizationManager.Core/Cloud/LocalEntryExtractor.cs` — populate BaseName
- `LocalizationManager.Core/Cloud/FileRegenerator.cs` — route writes per BaseName
- `LocalizationManager.Core/Cloud/Models/SyncState.cs` — entries keyed by `(BaseName, Key, Lang)`
- `LocalizationManager.Core/Cloud/SyncStateManager.cs` — read v3 schema with BaseName, migrate v2

**Modified files (CLI commands):**
- `Commands/Cloud/PushCommand.cs:212-218`
- `Commands/Cloud/PullCommand.cs:211-217`
- `Commands/Cloud/CloneCommand.cs:485-517`

**Modified tests:**
- `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupCloudSyncTests.cs` — flip assertions from "broken" to "fixed"
- `cloud/full-integration-test/tests/05-multi-group.sh` (new) — multi-group push/pull round-trip

---

## Phase A: Cloud DB schema

### Task A1: Add `BaseName` to the `ResourceKey` entity

**Files:**
- Modify: `cloud/src/LrmCloud.Shared/Entities/ResourceKey.cs`

- [ ] **Step 1: Add the property to the entity**

In `ResourceKey.cs`, between the `ProjectId` block and the `KeyName` block, add:

```csharp
/// <summary>
/// Base name of the resource group this key belongs to (e.g. "CustomerResources"
/// for files like CustomerResources.resx). Empty string means "no group" /
/// "default group" — single-group projects always use "".
/// </summary>
[Required]
[MaxLength(500)]
[Column("base_name")]
public string BaseName { get; set; } = string.Empty;
```

- [ ] **Step 2: Build the shared project**

Run: `dotnet build cloud/src/LrmCloud.Shared/LrmCloud.Shared.csproj`
Expected: `0 Error(s)`.

---

### Task A2: Update the `AppDbContext` unique index

**Files:**
- Modify: `cloud/src/LrmCloud.Api/Data/AppDbContext.cs:206-210`

- [ ] **Step 1: Change the unique-index expression**

Replace:

```csharp
modelBuilder.Entity<ResourceKey>(entity =>
{
    entity.HasIndex(e => new { e.ProjectId, e.KeyName }).IsUnique();
    entity.HasIndex(e => e.ProjectId);
});
```

with:

```csharp
modelBuilder.Entity<ResourceKey>(entity =>
{
    // Unique per project + base name + key name. BaseName="" means "default
    // group" (single-group projects); multi-group projects use the .resx /
    // .json base name to disambiguate keys that share a name across groups.
    entity.HasIndex(e => new { e.ProjectId, e.BaseName, e.KeyName }).IsUnique();
    entity.HasIndex(e => e.ProjectId);
});
```

- [ ] **Step 2: Build the API project**

Run: `dotnet build cloud/src/LrmCloud.Api/LrmCloud.Api.csproj`
Expected: `0 Error(s)`.

---

### Task A3: Generate and review the EF migration

**Files:**
- Create: `cloud/src/LrmCloud.Api/Data/Migrations/<timestamp>_AddBaseNameToResourceKey.cs`
- Create: `cloud/src/LrmCloud.Api/Data/Migrations/<timestamp>_AddBaseNameToResourceKey.Designer.cs`
- Modify: `cloud/src/LrmCloud.Api/Data/Migrations/AppDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate the migration**

Run from `cloud/src/LrmCloud.Api/`:

```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add AddBaseNameToResourceKey
```

Expected: three files created/updated under `Data/Migrations/`. If the tool isn't installed, run `dotnet tool install --global dotnet-ef` first.

- [ ] **Step 2: Verify the generated `Up` method**

Open the new `*_AddBaseNameToResourceKey.cs` and confirm the `Up` body resembles:

```csharp
migrationBuilder.AddColumn<string>(
    name: "base_name",
    table: "resource_keys",
    type: "character varying(500)",
    maxLength: 500,
    nullable: false,
    defaultValue: "");

migrationBuilder.DropIndex(
    name: "IX_resource_keys_ProjectId_KeyName",
    table: "resource_keys");

migrationBuilder.CreateIndex(
    name: "IX_resource_keys_ProjectId_BaseName_KeyName",
    table: "resource_keys",
    columns: new[] { "ProjectId", "base_name", "key_name" },
    unique: true);
```

If EF generated index names with different casing, leave them — what matters is the column list and uniqueness. If EF emitted `DropIndex` for an index name that doesn't exactly match what's deployed (because of legacy naming), edit it to drop the actual index name found via `psql -c "\d resource_keys" lrmcloud`.

- [ ] **Step 3: Apply the migration to a scratch DB and verify**

Bring up a scratch Postgres:

```bash
docker run --rm -d --name lrm-mig-test -e POSTGRES_PASSWORD=test -p 55433:5432 postgres:16
```

Then run the migration against it:

```bash
cd cloud/src/LrmCloud.Api
ConnectionStrings__Default="Host=localhost;Port=55433;Database=lrmcloud;Username=postgres;Password=test" \
  DOTNET_ROLL_FORWARD=Major dotnet ef database update
```

Verify the column exists:

```bash
docker exec lrm-mig-test psql -U postgres -d lrmcloud -c "\d resource_keys" | grep base_name
```

Expected: a row containing `base_name | character varying(500)| not null default ''::character varying`.

Then tear down: `docker stop lrm-mig-test`.

---

### Task A4: Verify existing cloud unit tests still compile and pass

**Files:**
- Test: `cloud/tests/LrmCloud.Tests/`

- [ ] **Step 1: Run the cloud test suite**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test cloud/tests/LrmCloud.Tests/LrmCloud.Tests.csproj`
Expected: all tests pass. Any test that constructs `new ResourceKey { ... }` with a required-property initializer is unaffected because `BaseName` has a default value.

If any test breaks because it asserts on the old unique-index name, update the assertion or remove it (index names aren't behavior, they're implementation detail).

---

## Phase B: Cloud server-side wire format and services

### Task B1: Add `BaseName` to push/pull DTOs

**Files:**
- Modify: `cloud/src/LrmCloud.Shared/DTOs/Sync/KeySyncDtos.cs` — `EntryData`, `EntryChange`, `EntryDeletion`, `SyncChangeDto`
- Modify: `cloud/src/LrmCloud.Shared/Entities/SyncHistory.cs` — `SyncChangeEntry`

- [ ] **Step 1: Locate the DTOs**

Run: `grep -n "class EntryChange\|class EntryDeletion\|class EntryData\|class SyncChangeDto" cloud/src/LrmCloud.Shared/DTOs/Sync/KeySyncDtos.cs`

For each, add a property:

```csharp
/// <summary>
/// Base name of the resource group this entry belongs to. Empty string means
/// "no group" / "default group" for single-group projects.
/// </summary>
public string BaseName { get; set; } = string.Empty;
```

Place it directly after the existing `Key` property in each class.

- [ ] **Step 2: Add the same property to `SyncHistory.SyncChangeEntry`**

In `cloud/src/LrmCloud.Shared/Entities/SyncHistory.cs`, find `class SyncChangeEntry` (around line 112) and add the `BaseName` property in the same position. This stores BaseName in the per-sync change log.

- [ ] **Step 3: Build the shared project**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build cloud/src/LrmCloud.Shared/LrmCloud.Shared.csproj`
Expected: `0 Error(s)`.

---

### Task B2: Update `KeySyncService` to thread `BaseName` through push handling

**Files:**
- Modify: `cloud/src/LrmCloud.Api/Services/KeySyncService.cs`

- [ ] **Step 1: Update push lookups to key by `(ProjectId, BaseName, KeyName)`**

Find every query in `KeySyncService` shaped like:

```csharp
.FirstOrDefaultAsync(rk => rk.ProjectId == projectId && rk.KeyName == change.Key, ct)
```

and update each to:

```csharp
.FirstOrDefaultAsync(rk => rk.ProjectId == projectId
                        && rk.BaseName == (change.BaseName ?? string.Empty)
                        && rk.KeyName == change.Key, ct)
```

Use a temporary local variable `var baseName = change.BaseName ?? string.Empty;` at the top of each method that needs it, so the LINQ expression is stable and EF doesn't choke on the null-coalesce.

- [ ] **Step 2: When inserting a new `ResourceKey`, populate `BaseName`**

Find every `new ResourceKey { ProjectId = ..., KeyName = ... }` initializer in `KeySyncService`, `ResourceService`, and `FileOperationsService` and add `BaseName = change.BaseName ?? string.Empty,` (or the equivalent local variable).

- [ ] **Step 3: When emitting `EntryData`/`EntryChange`/`SyncChangeDto` in responses, populate `BaseName` from the entity**

Find every place that constructs an outgoing DTO from a `ResourceKey` and add:

```csharp
BaseName = rk.BaseName,
```

- [ ] **Step 4: Build and run cloud tests**

Run:
```bash
DOTNET_ROLL_FORWARD=Major dotnet build cloud/src/LrmCloud.Api/LrmCloud.Api.csproj
DOTNET_ROLL_FORWARD=Major dotnet test cloud/tests/LrmCloud.Tests/LrmCloud.Tests.csproj
```
Expected: builds clean; all existing tests pass (they don't exercise multi-group yet, but the single-group path must still work because `BaseName` defaults to `""`).

---

### Task B3: Add `BaseName` round-trip test for the API

**Files:**
- Create or modify a test in `cloud/tests/LrmCloud.Tests/` under whichever folder hosts service-level integration tests (look for an existing `KeySyncServiceTests.cs` first)

- [ ] **Step 1: Find or create the test class**

Run: `find cloud/tests -name "*KeySync*" -o -name "*SyncController*"`. If a test class exists for sync push/pull, append to it; otherwise create `cloud/tests/LrmCloud.Tests/Services/MultiGroupKeySyncTests.cs`.

- [ ] **Step 2: Write the failing test**

```csharp
using LrmCloud.Api.Services;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LrmCloud.Tests.Services;

public class MultiGroupKeySyncTests : IClassFixture<TestDbFixture>
{
    private readonly TestDbFixture _fixture;
    public MultiGroupKeySyncTests(TestDbFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task Push_SameKeyNameInTwoGroups_StoresBothRows()
    {
        await using var db = _fixture.CreateContext();
        var project = await TestDataHelper.SeedProjectAsync(db);

        var changes = new List<EntryChange>
        {
            new() { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm" },
            new() { Key = "OK", BaseName = "SharedResources", Lang = "", Value = "OK" }
        };

        var sut = _fixture.CreateKeySyncService(db);
        await sut.ApplyPushAsync(project.Id, new PushRequest { Entries = changes }, default);

        var rows = await db.ResourceKeys
            .Where(rk => rk.ProjectId == project.Id && rk.KeyName == "OK")
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.BaseName == "CustomerResources");
        Assert.Contains(rows, r => r.BaseName == "SharedResources");
    }
}
```

Note: if `TestDbFixture` and `TestDataHelper` don't exist with those exact names, look at neighboring tests in `cloud/tests/` and copy their fixture-bootstrapping pattern. The names above are placeholders for "whatever the cloud test project's existing infrastructure for spinning up an EF in-memory or testcontainer DB is called."

- [ ] **Step 3: Run, expect FAIL** (because no client-side wiring yet provides BaseName — but server logic from B2 should handle it)

Run: `DOTNET_ROLL_FORWARD=Major dotnet test cloud/tests/LrmCloud.Tests --filter "FullyQualifiedName~MultiGroupKeySyncTests"`

If the test FAILS because `ApplyPushAsync` doesn't see BaseName, return to Task B2 and complete the threading. If it PASSES, move on.

---

## Phase C: Local sync types

### Task C1: Add `BaseName` to `LocalEntry`, `MergedEntry`, `EntryChange`, `EntryDeletion`

**Files:**
- Modify: `LocalizationManager.Core/Cloud/KeyLevelMerger.cs:478-487` (`LocalEntry`)
- Modify: `LocalizationManager.Core/Cloud/KeyLevelMerger.cs` (look for `class EntryChange` and `class EntryDeletion`)
- Modify: `LocalizationManager.Core/Cloud/Models/KeySyncDtos.cs:697` (`MergedEntry`)

- [ ] **Step 1: Update `LocalEntry`**

Replace the existing class body in `KeyLevelMerger.cs`:

```csharp
public class LocalEntry
{
    public required string Key { get; init; }
    /// <summary>
    /// Base name of the resource group this entry comes from. Empty string for
    /// single-group projects (preserves legacy behavior).
    /// </summary>
    public string BaseName { get; init; } = string.Empty;
    public required string Lang { get; init; }
    public required string Value { get; init; }
    public string? Comment { get; init; }
    public bool IsPlural { get; init; }
    public Dictionary<string, string>? PluralForms { get; init; }
    public required string Hash { get; init; }
}
```

- [ ] **Step 2: Add `BaseName` to `EntryChange` and `EntryDeletion`**

In the same file, find `class EntryChange` and `class EntryDeletion` (they're likely defined nearby). Add a `BaseName` property with the same XML doc and default value.

- [ ] **Step 3: Update `MergedEntry`**

In `Models/KeySyncDtos.cs`, in `class MergedEntry`, add after `Key`:

```csharp
/// <summary>
/// Base name of the resource group this entry belongs to. Routes pull writes
/// to the correct group file.
/// </summary>
public string BaseName { get; set; } = string.Empty;
```

- [ ] **Step 4: Build the Core project**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.Core/LocalizationManager.Core.csproj`
Expected: `0 Error(s)`. The default value means existing callers compile unchanged.

---

### Task C2: `LocalEntryExtractor` populates `BaseName`

**Files:**
- Modify: `LocalizationManager.Core/Cloud/LocalEntryExtractor.cs:51-60`

- [ ] **Step 1: Update the per-entry constructor**

Replace:

```csharp
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
```

with:

```csharp
entries.Add(new LocalEntry
{
    Key = entry.Key,
    BaseName = lang.BaseName ?? string.Empty,
    Lang = lang.Code,
    Value = entry.Value ?? string.Empty,
    Comment = entry.Comment,
    IsPlural = entry.IsPlural,
    PluralForms = entry.PluralForms,
    Hash = hash
});
```

- [ ] **Step 2: Update the dictionary lookup to use the BaseName-aware key**

Replace `ExtractEntriesAsDictionaryAsync`:

```csharp
public async Task<Dictionary<(string BaseName, string Key, string Lang), LocalEntry>> ExtractEntriesAsDictionaryAsync(
    IEnumerable<LanguageInfo> languages,
    CancellationToken cancellationToken = default)
{
    var entries = await ExtractEntriesAsync(languages, cancellationToken);
    return entries.ToDictionary(e => (e.BaseName, e.Key, e.Lang), e => e);
}
```

This is a breaking change to the return type. Find every caller via `grep -rn "ExtractEntriesAsDictionaryAsync" --include="*.cs"` and either update the caller or migrate them to call `ExtractEntriesAsync` and group themselves. If no callers exist in `cloud/` or `Commands/`, just update the signature and move on.

- [ ] **Step 3: Update the existing failing-by-design test to assert the FIXED behavior**

In `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupCloudSyncTests.cs`, replace the `LocalEntryExtractor_MultiBase_CrashesOnSharedKeyName` test with:

```csharp
[Fact]
public async Task LocalEntryExtractor_MultiBase_PreservesBaseNameOnDictionaryLookup()
{
    var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testDir);
    try
    {
        WriteResx(Path.Combine(testDir, "CustomerResources.resx"), ("OK", "Confirm"), ("Cancel", "Cancel"));
        WriteResx(Path.Combine(testDir, "SharedResources.resx"), ("OK", "OK"));

        IResourceDiscovery discovery = new ResxResourceDiscovery();
        IResourceBackend backend = new ResxResourceBackend();
        var languages = discovery.DiscoverLanguages(testDir);

        var extractor = new LocalEntryExtractor(backend);
        var entries = await extractor.ExtractEntriesAsync(languages);
        Assert.Equal(3, entries.Count);
        Assert.All(entries.Where(e => e.Key == "OK"),
            e => Assert.Contains(e.BaseName, new[] { "CustomerResources", "SharedResources" }));

        var dict = await extractor.ExtractEntriesAsDictionaryAsync(languages);
        Assert.Equal(3, dict.Count);
        Assert.True(dict.ContainsKey(("CustomerResources", "OK", "")));
        Assert.True(dict.ContainsKey(("SharedResources", "OK", "")));
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
}
```

- [ ] **Step 4: Run the test, expect PASS**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupCloudSyncTests.LocalEntryExtractor_MultiBase_PreservesBaseNameOnDictionaryLookup"`
Expected: PASS.

---

### Task C3: `KeyLevelMerger` keys by `(BaseName, Key, Lang)`

**Files:**
- Modify: `LocalizationManager.Core/Cloud/KeyLevelMerger.cs:20-91` (`ComputePushChanges`)
- Modify: `LocalizationManager.Core/Cloud/KeyLevelMerger.cs` (`MergeForPull`)

- [ ] **Step 1: Update `ComputePushChanges`**

Replace its body so that:
1. The internal lookup is `var localEntriesByKey = localEntries.GroupBy(e => (e.BaseName, e.Key, e.Lang)).ToDictionary(g => g.Key, g => g.First());`
2. `seenKeys` is `HashSet<(string BaseName, string Key, string Lang)>`
3. `syncState?.GetEntryHash(...)` is called with `(e.BaseName, e.Key, e.Lang)` instead of `(e.Key, e.Lang)`
4. Every `new EntryChange { Key = ..., Lang = ..., ... }` adds `BaseName = entry.BaseName,`
5. The deletion-detection loop iterates `syncState.Entries` keyed by `(BaseName, Key, Lang)` (this depends on the SyncState schema update in C5; if that hasn't shipped yet, key by `(string.Empty, Key, Lang)` so the single-group path still works)

The full method becomes (showing only the changed lines):

```csharp
var localEntriesByKey = localEntries
    .GroupBy(e => (e.BaseName, e.Key, e.Lang))
    .ToDictionary(g => g.Key, g => g.First());

var seenKeys = new HashSet<(string BaseName, string Key, string Lang)>();

foreach (var entry in localEntries)
{
    var keyTuple = (entry.BaseName, entry.Key, entry.Lang);
    seenKeys.Add(keyTuple);

    var baseHash = syncState?.GetEntryHash(entry.BaseName, entry.Key, entry.Lang);

    if (baseHash == null)
    {
        result.Additions.Add(new EntryChange
        {
            Key = entry.Key,
            BaseName = entry.BaseName,
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
        result.Modifications.Add(new EntryChange { /* same shape */ });
    }
}
```

`GetEntryHash` will get its third parameter in Task C5. Until then, this won't compile — that's fine: do C3 and C5 in sequence without committing.

- [ ] **Step 2: Update `MergeForPull` the same way**

Find every `(e.Key, e.Lang)` tuple in the method body and replace with `(e.BaseName, e.Key, e.Lang)`. Update `localUsesEmptyForDefault` etc. to remain correct.

When constructing each `MergedEntry`, add `BaseName = entry.BaseName,` (matching whichever side it came from).

- [ ] **Step 3: Defer the build check to after Task C5**

These changes will leave `KeyLevelMerger.cs` non-building because `SyncState.GetEntryHash` still has the old signature. Move on to Task C4/C5; we'll verify after both are done.

---

### Task C4: `FileRegenerator` routes writes per BaseName

**Files:**
- Modify: `LocalizationManager.Core/Cloud/FileRegenerator.cs:33-119` (`RegenerateFilesAsync`)
- Modify: `LocalizationManager.Core/Cloud/FileRegenerator.cs:125-218` (`UpdateExistingFileAsync`)
- Modify: `LocalizationManager.Core/Cloud/FileRegenerator.cs:223-285+` (`CreateNewLanguageFileAsync`)

- [ ] **Step 1: Replace the top of `RegenerateFilesAsync` to group by `(BaseName, Lang)`**

Find:

```csharp
var entriesByLang = mergedEntries
    .GroupBy(e => e.Lang)
    .ToDictionary(g => g.Key, g => g.ToList());

var existingLangFiles = existingLanguages.ToDictionary(l => l.Code, l => l);
```

Replace with:

```csharp
var entriesByGroupAndLang = mergedEntries
    .GroupBy(e => (e.BaseName, e.Lang))
    .ToDictionary(g => g.Key, g => g.ToList());

// Build lookup for existing language files keyed by (BaseName, Code). Multi-group
// directories have multiple files per code; this dictionary preserves both.
var existingLangFiles = existingLanguages
    .ToDictionary(l => (l.BaseName ?? string.Empty, l.Code), l => l);
```

- [ ] **Step 2: Replace the `foreach` that processes entries**

Replace:

```csharp
foreach (var (lang, entries) in entriesByLang)
{
    // ... resolve existingLang ...
    if (existingLang != null)
        await UpdateExistingFileAsync(existingLang, entries, tempDir, result, ct);
    else
        await CreateNewLanguageFileAsync(resolvedLang, entries, tempDir, result, ct);
}
```

with:

```csharp
foreach (var ((baseName, lang), entries) in entriesByGroupAndLang)
{
    cancellationToken.ThrowIfCancellationRequested();

    var resolvedLang = lang;
    LanguageInfo? existingLang = null;

    if (existingLangFiles.TryGetValue((baseName, lang), out existingLang))
    {
        // Direct match
    }
    else if (string.IsNullOrEmpty(lang) && backendUsesExplicitDefaultLang && defaultLangFile != null)
    {
        // Merged entries use "" for default but backend uses explicit code (XLIFF)
        existingLang = defaultLangFile;
        resolvedLang = defaultLangFile.Code;
    }

    if (existingLang != null)
    {
        await UpdateExistingFileAsync(existingLang, entries, tempDir, result, cancellationToken);
    }
    else
    {
        await CreateNewLanguageFileAsync(baseName, resolvedLang, entries, tempDir, result, cancellationToken);
    }
}
```

- [ ] **Step 3: Update `CreateNewLanguageFileAsync` to accept `baseName`**

Change its signature to `private async Task CreateNewLanguageFileAsync(string baseName, string lang, List<MergedEntry> entries, string tempDir, RegenerationResult result, CancellationToken ct)`. When constructing the new `LanguageInfo`, set `BaseName = baseName`. When computing `GetNewLanguageFilePath`, include the base name in the file naming so the new file lands as `{baseName}.{lang}.resx` (or whatever the backend's convention is — most backends already format this via their writer, but Resx specifically uses the BaseName to build the filename).

- [ ] **Step 4: Build and run the new failing test**

Run:
```bash
DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.csproj
```
Expected: `0 Error(s)` once Task C5 is also done; if `SyncState.GetEntryHash` errors here, finish Task C5 first.

---

### Task C5: `SyncState` schema v3 with BaseName

**Files:**
- Modify: `LocalizationManager.Core/Cloud/Models/SyncState.cs`
- Modify: `LocalizationManager.Core/Cloud/SyncStateManager.cs`

- [ ] **Step 1: Bump the schema version and expand the dictionary shape**

Replace the `Entries` shape and helpers in `SyncState.cs`. The new shape:

```csharp
/// <summary>
/// Schema version. v3 introduces per-(BaseName, Key, Lang) hashes.
/// </summary>
public int Version { get; set; } = 3;

/// <summary>
/// Entry-level tracking keyed by (BaseName, Key) → Lang → hash.
/// BaseName "" = default/no-group (legacy single-group projects).
/// </summary>
public Dictionary<string, Dictionary<string, Dictionary<string, string>>> EntriesV3 { get; set; } = new();
```

Keep the old `Entries` property under an obsolete attribute so a v2 file can be migrated.

```csharp
[Obsolete("Use EntriesV3. Retained for v2 → v3 migration.")]
public Dictionary<string, Dictionary<string, string>>? Entries { get; set; }
```

- [ ] **Step 2: Add the new lookup helpers**

```csharp
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

public void RemoveEntryHash(string baseName, string key, string? lang = null)
{
    if (!EntriesV3.TryGetValue(baseName, out var byKey)) return;
    if (lang == null) { byKey.Remove(key); }
    else if (byKey.TryGetValue(key, out var byLang))
    {
        byLang.Remove(lang);
        if (byLang.Count == 0) byKey.Remove(key);
    }
    if (byKey.Count == 0) EntriesV3.Remove(baseName);
}
```

- [ ] **Step 3: Update `NeedsMigration` and add a v2 → v3 migrator**

```csharp
public bool NeedsMigration => Version < 3 || (Entries != null && Entries.Count > 0 && EntriesV3.Count == 0);

public static SyncState MigrateToV3(SyncState old)
{
    var migrated = CreateNew();
    if (old.Entries != null)
    {
        foreach (var (key, byLang) in old.Entries)
        {
            foreach (var (lang, hash) in byLang)
            {
                migrated.SetEntryHash(string.Empty, key, lang, hash);
            }
        }
    }
    migrated.ConfigProperties = old.ConfigProperties;
    migrated.Timestamp = old.Timestamp;
    return migrated;
}

public static SyncState CreateNew() => new()
{
    Version = 3,
    Timestamp = DateTime.UtcNow,
    EntriesV3 = new(),
    ConfigProperties = new()
};
```

- [ ] **Step 4: Update `SyncStateManager` to trigger migration on load**

Find the load method (likely `LoadStateAsync` or similar). After deserializing, if `state.NeedsMigration`, replace it with `SyncState.MigrateToV3(state)`.

- [ ] **Step 5: Build the Core project**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.Core/LocalizationManager.Core.csproj`
Expected: `0 Error(s)`. C3/C4/C5 should now compile together.

- [ ] **Step 6: Run the local sync test suite**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~Cloud"`
Expected: existing single-group sync tests still pass; the new multi-group test from C2 passes.

---

### Task C6: Multi-group `FileRegenerator` unit test

**Files:**
- Create: `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupFileRegeneratorTests.cs`

- [ ] **Step 1: Write the test**

```csharp
// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class MultiGroupFileRegeneratorTests
{
    [Fact]
    public async Task RegenerateFilesAsync_RoutesKeyToCorrectGroupFile()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            WriteResx(Path.Combine(testDir, "CustomerResources.resx"), ("OK", "Confirm"));
            WriteResx(Path.Combine(testDir, "SharedResources.resx"), ("OK", "OK"));

            IResourceDiscovery discovery = new ResxResourceDiscovery();
            IResourceBackend backend = new ResxResourceBackend();
            var languages = discovery.DiscoverLanguages(testDir);

            var regenerator = new FileRegenerator(backend, testDir);
            var merged = new List<MergedEntry>
            {
                new() { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm NEW" },
                new() { Key = "OK", BaseName = "SharedResources",   Lang = "", Value = "OK NEW" }
            };

            var result = await regenerator.RegenerateFilesAsync(merged, languages);
            Assert.True(result.Success, result.Error);

            var customer = File.ReadAllText(Path.Combine(testDir, "CustomerResources.resx"));
            var shared   = File.ReadAllText(Path.Combine(testDir, "SharedResources.resx"));
            Assert.Contains("Confirm NEW", customer);
            Assert.DoesNotContain("OK NEW", customer);
            Assert.Contains("OK NEW", shared);
            Assert.DoesNotContain("Confirm NEW", shared);
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    private static void WriteResx(string path, params (string Key, string Value)[] entries)
    {
        var data = string.Concat(entries.Select(e =>
            $"  <data name=\"{e.Key}\"><value>{e.Value}</value></data>\n"));
        File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n" +
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
            "  <resheader name=\"version\"><value>2.0</value></resheader>\n" +
            "  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n" +
            "  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n" +
            data + "</root>\n");
    }
}
```

- [ ] **Step 2: Run, expect PASS**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupFileRegeneratorTests"`
Expected: PASS.

---

### Task C7: Multi-group `KeyLevelMerger` unit test

**Files:**
- Create: `LocalizationManager.Tests/UnitTests/Cloud/MultiGroupKeyLevelMergerTests.cs`

- [ ] **Step 1: Write the test**

```csharp
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
        // Also a hash for the same key in a different group: should not match.
        state.SetEntryHash("SharedResources", "OK", "", "h-other");

        var merger = new KeyLevelMerger();
        var changes = merger.ComputePushChanges(locals, state);

        Assert.Single(changes.Modifications);
        Assert.Empty(changes.Additions);
        Assert.Single(changes.Deletions); // SharedResources/OK was in state but not in local
    }
}
```

- [ ] **Step 2: Run, expect PASS**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupKeyLevelMergerTests"`
Expected: PASS.

---

## Phase D: CLI commands

### Task D1: `PushCommand` passes `BaseName` through

**Files:**
- Modify: `Commands/Cloud/PushCommand.cs:211-225`

- [ ] **Step 1: Switch from `DiscoverLanguages` to `DiscoverResourceGroups`**

Replace:

```csharp
var extractor = new LocalEntryExtractor(backend);
var languages = backend.Discovery.DiscoverLanguages(projectDirectory);
```

with:

```csharp
var extractor = new LocalEntryExtractor(backend);
var directory = backend.Discovery.DiscoverResourceGroups(projectDirectory);
var languages = directory.Groups.SelectMany(g => g.Files).ToList();
```

`LocalEntryExtractor` already reads `lang.BaseName`, and the discovery returns groups with each `LanguageInfo` carrying its `BaseName`. No further change here.

- [ ] **Step 2: Build the CLI project**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.csproj`
Expected: `0 Error(s)`.

---

### Task D2: `PullCommand` passes `BaseName` through

**Files:**
- Modify: `Commands/Cloud/PullCommand.cs:211-217`, `:360` (FileRegenerator usage)

- [ ] **Step 1: Switch local enumeration**

Replace each `backend.Discovery.DiscoverLanguages(projectDirectory)` in this file with the same pattern as PushCommand: get a `directory` via `DiscoverResourceGroups`, then flatten to `languages = directory.Groups.SelectMany(...).ToList()`. The downstream code that passes `languages` to `LocalEntryExtractor` and to `FileRegenerator` works unchanged because the BaseName flows on the `LanguageInfo` instances.

- [ ] **Step 2: Build**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.csproj`
Expected: `0 Error(s)`.

---

### Task D3: `CloneCommand` materializes BaseName from server payload

**Files:**
- Modify: `Commands/Cloud/CloneCommand.cs:485-517`

- [ ] **Step 1: When converting incoming server entries to `MergedEntry`, populate `BaseName`**

Find the `MergedEntry` construction (near line 485). The server response already carries `BaseName` (from Phase B). Add `BaseName = serverEntry.BaseName ?? string.Empty,` in the initializer.

- [ ] **Step 2: When `FileRegenerator` runs, it now routes per-group automatically**

No further change here; C4 already did the routing.

- [ ] **Step 3: Build**

Run: `DOTNET_ROLL_FORWARD=Major dotnet build LocalizationManager.csproj`
Expected: `0 Error(s)`.

---

### Task D4: Full local test suite

- [ ] **Step 1: Run every test**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test LocalizationManager.Tests --nologo`
Expected: every test passes; the multi-group cloud sync tests added in C2/C6/C7 pass; the existing single-group cloud sync tests still pass.

If any pre-existing test fails, diagnose: most likely a test that constructs `new LocalEntry { Key = ..., Lang = ... }` with all-required-init properties — the new `BaseName` defaults to `""` so they should compile, but verify they assert correctly.

---

## Phase E: Integration test against the real cloud stack

### Task E1: Add a multi-group integration test

**Files:**
- Create: `cloud/full-integration-test/tests/05-multi-group.sh`
- Modify: `cloud/full-integration-test/run-tests.sh` (register the new test)

- [ ] **Step 1: Copy the structure of `01-basic-push-pull.sh`**

Open `cloud/full-integration-test/tests/01-basic-push-pull.sh` and use it as a template. The new test should:

1. Set up two resx base names in the same directory:
   ```bash
   mkdir -p "$PROJECT_DIR/Resources"
   cat > "$PROJECT_DIR/Resources/CustomerResources.resx" <<'EOF'
   <?xml version="1.0" encoding="utf-8"?>
   <root>
     <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
     <resheader name="version"><value>2.0</value></resheader>
     <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
     <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
     <data name="OK"><value>Confirm</value></data>
   </root>
   EOF
   cat > "$PROJECT_DIR/Resources/SharedResources.resx" <<'EOF'
   <?xml version="1.0" encoding="utf-8"?>
   <root>
     <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
     <resheader name="version"><value>2.0</value></resheader>
     <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
     <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
     <data name="OK"><value>OK</value></data>
   </root>
   EOF
   ```

2. Run `$LRM cloud init` and `$LRM cloud push` against the running stack.

3. Verify via direct DB query that both rows exist:
   ```bash
   docker exec "${COMPOSE_PROJECT_NAME}-postgres-1" \
     psql -U postgres -d lrmcloud -tA \
     -c "SELECT base_name, key_name FROM resource_keys WHERE key_name = 'OK' ORDER BY base_name;"
   ```
   Expected output (two lines): `CustomerResources|OK` and `SharedResources|OK`.

4. Delete the local files, run `$LRM cloud pull`, and verify both files are recreated with the correct values:
   ```bash
   grep -q "Confirm" "$PROJECT_DIR/Resources/CustomerResources.resx" || fail "Confirm missing from CustomerResources"
   grep -q "OK" "$PROJECT_DIR/Resources/SharedResources.resx" || fail "OK missing from SharedResources"
   ```

- [ ] **Step 2: Register the test in the runner**

In `run-tests.sh`, find the `TESTS=(...)` array (or equivalent) and append `"05-multi-group"`.

- [ ] **Step 3: Run the test**

Run: `cd cloud/full-integration-test && ./run-tests.sh --test 05-multi-group`
Expected: PASS. Inspect the run log if it doesn't.

---

### Task E2: Final regression sweep

- [ ] **Step 1: Run the full integration suite**

Run: `cd cloud/full-integration-test && ./run-tests.sh`
Expected: all tests pass, including the original single-group tests (01–04). If any pre-existing test fails, diagnose: most likely an interaction with the new column or a missing BaseName-default in a server query.

---

### Task E3: Report findings and present commit slicing

- [ ] **Step 1: Stop the integration infrastructure**

Run: `cd cloud/full-integration-test && ./run-tests.sh --cleanup-only` (or `docker compose down` if there's no such flag — see the runner's help).

- [ ] **Step 2: Report to the operator**

Summarize:
- Phase A (DB schema + migration) — one logical commit.
- Phase B (server wire DTOs + KeySyncService threading + server tests) — one commit.
- Phase C (local sync types + tests for extractor/merger/regenerator + SyncState v3) — one commit.
- Phase D (CLI commands) — one commit.
- Phase E (integration test) — one commit.

Suggest five commits along these phase boundaries unless the operator wants finer slicing. Do not execute `git commit` — the operator drives commit decisions.

---

## Self-Review Notes

- **Spec coverage:**
  - DB schema → Phase A.
  - Wire DTOs → Phase B (server), Phase C (client).
  - KeyLevelMerger / FileRegenerator → Phase C (and unit tests in C6/C7).
  - Sync state local format → C5.
  - CLI Push/Pull/Clone → Phase D.
  - Integration test against real stack → Phase E.
- **Backward compatibility:**
  - Single-group projects keep sending `BaseName=""`. The new unique index `(ProjectId, "", KeyName)` matches every pre-migration row after the migration backfill.
  - Old clients (no BaseName in payload) are accepted: server reads `request.BaseName ?? string.Empty` everywhere.
  - SyncState v2 files on disk auto-migrate to v3 on first load.
- **Known follow-up:**
  - When a previously-single-group project grows a second group, existing rows have `BaseName=""` but new entries land under real BaseNames. Add a `lrm cloud migrate-groups` command in a follow-up plan.
  - GitHub sync (`GitHubSyncState` entity, `GitHubSyncService`) has the same architectural problem; not in scope here.
  - Cloud Web (Blazor WASM) UI may render unexpectedly when faced with multi-group keys (e.g. two rows with the same key name). A follow-up plan should add the same Resource Group column as the VS Code editor.
