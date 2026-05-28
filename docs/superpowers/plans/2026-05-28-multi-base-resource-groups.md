# Multi-Base Resource Groups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Resource Groups (BaseName) a first-class concept so directories with multiple base resource files (e.g. `CustomerResources.resx` + `GlassResources.resx`) are correctly modeled as multiple groups sharing the same set of cultures, fixing GitHub issue #6.

**Architecture:** Introduce a `ResourceGroup` model that bundles one `BaseName` with its per-culture files. `DiscoverResourceGroups()` returns the grouped view; `DiscoverLanguages()` is preserved (for backwards-compat with the many callsites) but documented as a flat per-file enumeration that must NOT be used to count or list languages. Controllers move to the grouped view: `/api/language` returns one entry per distinct culture (aggregated across groups); resource-key responses gain a `resourceGroup` field; key-edit endpoints take the group as part of the route. The VS Code editor renders one column per culture and one row per (Key, ResourceGroup), routing edits to the correct file.

**Tech Stack:** .NET 8 (LocalizationManager.Core, ASP.NET Core controllers), xUnit, TypeScript (VS Code extension).

**Scope notes:**
- This plan fixes `Resx` and `Json` backends (both have the multi-base bug).
- `Po`, `Android`, `iOS` are unaffected by this bug because they enforce one base per language, but they must still implement the new interface method (returning a single-group view).
- Frontend tasks (VS Code editor + dashboard) are included; they ship after the backend so the API contract is locked.

**Commits:** Do NOT commit between tasks during execution. Finish the plan in full (or in clearly bounded phase batches the operator approves), then create commits when the operator asks. Per-step "Commit" instructions are intentionally omitted below.

---

## File Structure

**New files (Core):**
- `LocalizationManager.Core/Models/ResourceGroup.cs` — model bundling BaseName + list of LanguageInfo files
- `LocalizationManager.Core/Models/ResourceDirectory.cs` — top-level discovery result (groups + distinct culture codes)

**Modified files (Core):**
- `LocalizationManager.Core/Abstractions/IResourceDiscovery.cs` — add `DiscoverResourceGroups`
- `LocalizationManager.Core/Backends/Resx/ResxResourceDiscovery.cs` — implement grouped discovery
- `LocalizationManager.Core/Backends/Json/JsonResourceDiscovery.cs` — implement grouped discovery
- `LocalizationManager.Core/Backends/Po/PoResourceDiscovery.cs` — implement (single-group adapter)
- `LocalizationManager.Core/Backends/Android/AndroidResourceDiscovery.cs` — implement (single-group adapter)
- `LocalizationManager.Core/Backends/iOS/IosResourceDiscovery.cs` — implement (single-group adapter)
- `LocalizationManager.Core/Backends/Xliff/XliffResourceDiscovery.cs` — implement (single-group adapter)

**New API DTOs:**
- `Models/Api/ResourceGroupInfo.cs` — per-group response shape

**Modified files (API):**
- `Models/Api/ResourceKeyInfo.cs` — add `ResourceGroup` field
- `Models/Api/ResourceKeyDetails.cs` — add `ResourceGroup` field
- `Models/Api/AddKeyRequest.cs` — add required `ResourceGroup`
- `Models/Api/AddLanguageRequest.cs` — add optional `ResourceGroup` (default: all)
- `Controllers/LanguageController.cs` — aggregate by Code; per-group AddLanguage
- `Controllers/ResourcesController.cs` — include ResourceGroup; route edits per-group
- `Controllers/StatsController.cs` — compute against distinct cultures across groups
- `Controllers/ValidationController.cs` — per-group validation rolled up
- `Controllers/ExportController.cs` — preserve group boundaries
- `Controllers/ImportController.cs` — accept group hint

**Modified files (VS Code extension):**
- `vscode-extension/src/backend/apiClient.ts` — update types for new shape
- `vscode-extension/src/views/dashboard.ts` — render distinct-culture stats
- `vscode-extension/src/views/resourceEditor.ts` — show ResourceGroup column; route edits per-group
- `vscode-extension/src/backend/cacheService.ts` — cache keys with group identity

**Test files:**
- `LocalizationManager.Tests/UnitTests/ResourceDiscoveryTests.cs` — multi-base resx tests
- `LocalizationManager.Tests/UnitTests/JsonResourceDiscoveryTests.cs` — multi-base json tests (create if missing)
- `LocalizationManager.Tests/IntegrationTests/MultiGroupControllerTests.cs` — new file
- `LocalizationManager.Tests/TestData/MultiGroupResx/` — new fixtures: `CustomerResources.resx`, `CustomerResources.it.resx`, `GlassResources.resx`, `GlassResources.it.resx`
- `LocalizationManager.Tests/TestData/MultiGroupJson/` — new fixtures

---

## Phase A: Core Model + Discovery

### Task A1: Add `ResourceGroup` and `ResourceDirectory` models

**Files:**
- Create: `LocalizationManager.Core/Models/ResourceGroup.cs`
- Create: `LocalizationManager.Core/Models/ResourceDirectory.cs`

- [ ] **Step 1: Write `ResourceGroup.cs`**

```csharp
// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Models;

/// <summary>
/// A resource group bundles one base resource name (e.g. "SharedResource") with
/// its per-culture files. Each <see cref="LanguageInfo"/> in <see cref="Files"/>
/// shares the same <see cref="BaseName"/>.
/// </summary>
public class ResourceGroup
{
    /// <summary>Base resource name shared by all files in this group.</summary>
    public required string BaseName { get; init; }

    /// <summary>One entry per culture for this base name. At least one entry; one will be marked IsDefault.</summary>
    public required IReadOnlyList<LanguageInfo> Files { get; init; }
}
```

- [ ] **Step 2: Write `ResourceDirectory.cs`**

```csharp
// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Models;

/// <summary>
/// The output of <c>IResourceDiscovery.DiscoverResourceGroups</c>: zero or
/// more groups in a directory, plus the distinct culture codes present
/// across all groups (the empty string represents the invariant/default culture).
/// </summary>
public class ResourceDirectory
{
    public required IReadOnlyList<ResourceGroup> Groups { get; init; }

    /// <summary>Distinct culture codes across all groups. Includes "" for the default/invariant culture if any group has a default file.</summary>
    public required IReadOnlyList<string> CultureCodes { get; init; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build LocalizationManager.Core/LocalizationManager.Core.csproj`
Expected: BUILD SUCCESS.

---

### Task A2: Extend `IResourceDiscovery` with `DiscoverResourceGroups`

**Files:**
- Modify: `LocalizationManager.Core/Abstractions/IResourceDiscovery.cs`

- [ ] **Step 1: Add interface method (default implementation deriving from `DiscoverLanguages`)**

Replace the body of the interface with:

```csharp
public interface IResourceDiscovery
{
    Task<List<LanguageInfo>> DiscoverLanguagesAsync(
        string searchPath,
        CancellationToken ct = default);

    List<LanguageInfo> DiscoverLanguages(string searchPath);

    /// <summary>
    /// Discover all resource groups in the specified path. A resource group is a
    /// set of files sharing the same base name (e.g. SharedResource.resx,
    /// SharedResource.el.resx). Directories may contain multiple unrelated
    /// groups (e.g. CustomerResources, GlassResources).
    /// </summary>
    ResourceDirectory DiscoverResourceGroups(string searchPath)
    {
        // Default implementation: group the per-file enumeration by BaseName.
        var files = DiscoverLanguages(searchPath);
        var groups = files
            .GroupBy(f => f.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ResourceGroup { BaseName = g.Key, Files = g.ToList() })
            .ToList();
        var codes = files
            .Select(f => f.Code ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ResourceDirectory { Groups = groups, CultureCodes = codes };
    }

    Task<ResourceDirectory> DiscoverResourceGroupsAsync(string searchPath, CancellationToken ct = default)
        => Task.Run(() => DiscoverResourceGroups(searchPath), ct);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build LocalizationManager.Core/LocalizationManager.Core.csproj`
Expected: BUILD SUCCESS (default interface methods are C# 8+ and the project targets net8).

---

### Task A3: Multi-base Resx discovery — failing tests first

**Files:**
- Modify: `LocalizationManager.Tests/UnitTests/ResourceDiscoveryTests.cs`
- Create: `LocalizationManager.Tests/TestData/MultiGroupResx/CustomerResources.resx`
- Create: `LocalizationManager.Tests/TestData/MultiGroupResx/CustomerResources.it.resx`
- Create: `LocalizationManager.Tests/TestData/MultiGroupResx/GlassResources.resx`
- Create: `LocalizationManager.Tests/TestData/MultiGroupResx/GlassResources.it.resx`

- [ ] **Step 1: Create test fixture `CustomerResources.resx`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
  <data name="CustomerTitle"><value>Customer</value></data>
  <data name="CustomerEmail"><value>Email</value></data>
</root>
```

- [ ] **Step 2: Create `CustomerResources.it.resx`** with the same keys translated:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
  <data name="CustomerTitle"><value>Cliente</value></data>
  <data name="CustomerEmail"><value>Email</value></data>
</root>
```

- [ ] **Step 3: Create `GlassResources.resx`**:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
  <data name="GlassThickness"><value>Thickness</value></data>
  <data name="GlassColor"><value>Color</value></data>
</root>
```

- [ ] **Step 4: Create `GlassResources.it.resx`**:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>
  <data name="GlassThickness"><value>Spessore</value></data>
  <data name="GlassColor"><value>Colore</value></data>
</root>
```

- [ ] **Step 5: Wire fixtures into csproj copy-on-build**

In `LocalizationManager.Tests/LocalizationManager.Tests.csproj`, ensure the `TestData/MultiGroupResx/*.resx` files are copied to output. If existing `TestData` is already handled by a glob (e.g. `<None Include="TestData\**\*" />`), no change is needed. Otherwise add:

```xml
<ItemGroup>
  <None Update="TestData\MultiGroupResx\*.resx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 6: Add failing tests to `ResourceDiscoveryTests.cs`**

Append to the existing class:

```csharp
[Fact]
public void DiscoverResourceGroups_MultiBaseDirectory_ReturnsOneGroupPerBaseName()
{
    var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

    var directory = _discovery.DiscoverResourceGroups(path);

    Assert.Equal(2, directory.Groups.Count);
    Assert.Contains(directory.Groups, g => g.BaseName == "CustomerResources");
    Assert.Contains(directory.Groups, g => g.BaseName == "GlassResources");
}

[Fact]
public void DiscoverResourceGroups_MultiBaseDirectory_ReturnsOneCultureCodePerLanguage()
{
    var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

    var directory = _discovery.DiscoverResourceGroups(path);

    // Two cultures: invariant ("") and Italian ("it")
    Assert.Equal(2, directory.CultureCodes.Count);
    Assert.Contains("", directory.CultureCodes);
    Assert.Contains("it", directory.CultureCodes);
}

[Fact]
public void DiscoverResourceGroups_MultiBaseDirectory_EachGroupHasAllCultures()
{
    var path = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

    var directory = _discovery.DiscoverResourceGroups(path);

    foreach (var group in directory.Groups)
    {
        Assert.Equal(2, group.Files.Count);
        Assert.Contains(group.Files, f => f.IsDefault);
        Assert.Contains(group.Files, f => f.Code == "it");
    }
}
```

- [ ] **Step 7: Run tests — these should already PASS via the default interface impl**

Run: `dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~ResourceDiscoveryTests.DiscoverResourceGroups"`
Expected: 3 tests PASS (the default interface implementation correctly groups, since `ResxResourceDiscovery` already sets BaseName correctly per file).

If any fail, fix the default-interface implementation in Task A2 — do not move on.

---

### Task A4: Multi-base Json discovery tests

**Files:**
- Create: `LocalizationManager.Tests/UnitTests/JsonResourceDiscoveryTests.cs` (if missing — check first with `ls LocalizationManager.Tests/UnitTests/ | grep -i json`)
- Create: `LocalizationManager.Tests/TestData/MultiGroupJson/customers.json`
- Create: `LocalizationManager.Tests/TestData/MultiGroupJson/customers.it.json`
- Create: `LocalizationManager.Tests/TestData/MultiGroupJson/glass.json`
- Create: `LocalizationManager.Tests/TestData/MultiGroupJson/glass.it.json`

- [ ] **Step 1: Check whether a JsonResourceDiscoveryTests file exists**

Run: `ls LocalizationManager.Tests/UnitTests/ | grep -i json`
If a Json discovery test file exists, append tests there. If not, create it with the standard test-class skeleton (using `JsonResourceDiscovery`).

- [ ] **Step 2: Create the four JSON fixtures**

`customers.json`:
```json
{ "CustomerTitle": "Customer", "CustomerEmail": "Email" }
```

`customers.it.json`:
```json
{ "CustomerTitle": "Cliente", "CustomerEmail": "Email" }
```

`glass.json`:
```json
{ "GlassThickness": "Thickness", "GlassColor": "Color" }
```

`glass.it.json`:
```json
{ "GlassThickness": "Spessore", "GlassColor": "Colore" }
```

- [ ] **Step 3: Add the same three tests as Task A3 (groups, culture codes, per-group cultures), but targeting `MultiGroupJson` and `JsonResourceDiscovery`**

(Repeat the test code from Task A3 Step 6, with paths and discovery type substituted.)

- [ ] **Step 4: Run tests**

Run: `dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~JsonResourceDiscoveryTests.DiscoverResourceGroups"`
Expected: 3 tests PASS (default interface impl handles JSON the same way).

---

## Phase B: API Layer — Aggregate by Culture

### Task B1: `LanguageController.GetLanguages` returns distinct cultures

**Files:**
- Modify: `Controllers/LanguageController.cs:32-72`

- [ ] **Step 1: Write failing integration test**

Create `LocalizationManager.Tests/IntegrationTests/MultiGroupControllerTests.cs`:

```csharp
// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Controllers;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class MultiGroupControllerTests
{
    private readonly string _testDataPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

    private LanguageController BuildLanguageController()
    {
        var backend = new ResxResourceBackend();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = _testDataPath })
            .Build();
        return new LanguageController(config, backend);
    }

    [Fact]
    public void GetLanguages_MultiBaseDirectory_ReturnsOneEntryPerCulture()
    {
        var controller = BuildLanguageController();

        var result = controller.GetLanguages();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LanguagesResponse>(ok.Value);
        Assert.Equal(2, response.Languages.Count); // invariant + it
        Assert.Single(response.Languages, l => l.IsDefault);
        Assert.Single(response.Languages, l => l.Code == "it");
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupControllerTests.GetLanguages_MultiBaseDirectory"`
Expected: FAIL — controller currently returns 4 entries.

- [ ] **Step 3: Refactor `LanguageController.GetLanguages`**

Replace the body of the `GetLanguages` method with:

```csharp
[HttpGet]
public ActionResult<LanguagesResponse> GetLanguages()
{
    try
    {
        var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
        var groupedFiles = directory.Groups
            .SelectMany(g => g.Files.Select(f => new { Group = g, File = f, Resource = _backend.Reader.Read(f) }))
            .ToList();

        // Distinct cultures across all groups; each becomes one returned LanguageInfo.
        var cultureGroups = groupedFiles
            .GroupBy(x => new { x.File.Code, x.File.IsDefault })
            .OrderByDescending(g => g.Key.IsDefault)
            .ThenBy(g => g.Key.Code)
            .ToList();

        // Total keys: distinct keys across all groups' default files.
        var totalKeys = groupedFiles
            .Where(x => x.File.IsDefault)
            .SelectMany(x => x.Resource.Entries.Select(e => $"{x.Group.BaseName}::{e.Key}"))
            .Distinct()
            .Count();

        var result = cultureGroups.Select(cultureGroup =>
        {
            var sample = cultureGroup.First();
            var translatedCount = cultureGroup
                .SelectMany(x => x.Resource.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => $"{x.Group.BaseName}::{e.Key}"))
                .Distinct()
                .Count();

            var coverage = totalKeys > 0 ? (double)translatedCount / totalKeys * 100 : 0;

            return new Models.Api.LanguageInfo
            {
                Code = sample.File.Code,
                IsDefault = sample.File.IsDefault,
                Name = sample.File.Name,
                FilePath = string.Join(";", cultureGroup.Select(x => x.File.FilePath)),
                DisplayName = _languageManager.GetCultureDisplayName(sample.File.Code ?? "default"),
                TotalKeys = totalKeys,
                TranslatedKeys = translatedCount,
                Coverage = Math.Round(coverage, 2)
            };
        }).ToList();

        return Ok(new LanguagesResponse { Languages = result });
    }
    catch (Exception)
    {
        return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupControllerTests.GetLanguages_MultiBaseDirectory"`
Expected: PASS.

- [ ] **Step 5: Run the full existing test suite — fix any regressions before continuing**

Run: `dotnet test LocalizationManager.Tests`
Expected: ALL PASS. If something fails (likely the single-base `LanguageController` tests if any), reconcile — the single-base case should still produce one entry per culture, so existing tests should pass unchanged.

---

### Task B2: Add `ResourceGroup` to `ResourceKeyInfo`

**Files:**
- Modify: `Models/Api/ResourceKeyInfo.cs` (locate via `grep -rn "class ResourceKeyInfo" Models/`)
- Modify: `Models/Api/ResourceKeyDetails.cs`
- Modify: `vscode-extension/src/backend/apiClient.ts` (the matching TS interface)

- [ ] **Step 1: Locate the DTOs**

Run: `grep -rn "class ResourceKeyInfo\|class ResourceKeyDetails" Models/ LocalizationManager.Shared/ 2>/dev/null`
Note the file paths — they may live in `LocalizationManager.Shared` rather than `Models/Api`.

- [ ] **Step 2: Add `ResourceGroup` to `ResourceKeyInfo`**

In the file containing `ResourceKeyInfo`, add a new property:

```csharp
/// <summary>
/// Base name of the resource group this key belongs to (e.g. "CustomerResources").
/// Used to disambiguate when the same key exists in multiple groups within a directory.
/// </summary>
public string ResourceGroup { get; set; } = string.Empty;
```

Do the same for `ResourceKeyDetails`.

- [ ] **Step 3: Update the matching TypeScript interface**

In `vscode-extension/src/backend/apiClient.ts`, find the `ResourceKeyInfo` interface (or wherever the per-key shape is declared) and add:

```ts
resourceGroup: string;
```

- [ ] **Step 4: Build both projects**

Run: `dotnet build LocalizationManager.csproj && (cd vscode-extension && npm run compile)`
Expected: both BUILD SUCCESS.

---

### Task B3: `GetAllKeys` returns (Key, Group) tuples instead of collapsing

**Files:**
- Modify: `Controllers/ResourcesController.cs:52-103`

- [ ] **Step 1: Write failing test in `MultiGroupControllerTests.cs`**

Add:

```csharp
private ResourcesController BuildResourcesController()
{
    var backend = new ResxResourceBackend();
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = _testDataPath })
        .Build();
    return new ResourcesController(config, backend);
}

[Fact]
public void GetAllKeys_MultiBaseDirectory_ReturnsOneRowPerKeyPerGroup()
{
    var controller = BuildResourcesController();

    var result = controller.GetAllKeys();

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var rows = Assert.IsAssignableFrom<IEnumerable<ResourceKeyInfo>>(ok.Value).ToList();

    // 4 distinct keys total: CustomerTitle, CustomerEmail, GlassThickness, GlassColor
    Assert.Equal(4, rows.Count);
    Assert.Contains(rows, r => r.Key == "CustomerTitle" && r.ResourceGroup == "CustomerResources");
    Assert.Contains(rows, r => r.Key == "GlassThickness" && r.ResourceGroup == "GlassResources");

    // Italian value for CustomerTitle should be "Cliente"
    var customerTitle = rows.Single(r => r.Key == "CustomerTitle" && r.ResourceGroup == "CustomerResources");
    Assert.Equal("Cliente", customerTitle.Values["it"]);
}
```

- [ ] **Step 2: Run test — expect FAIL** (`ResourceGroup` is empty + only some keys returned)

Run: `dotnet test LocalizationManager.Tests --filter "FullyQualifiedName~MultiGroupControllerTests.GetAllKeys_MultiBaseDirectory"`

- [ ] **Step 3: Refactor `GetAllKeys`**

Replace the body with:

```csharp
[HttpGet("keys")]
public ActionResult<IEnumerable<ResourceKeyInfo>> GetAllKeys()
{
    try
    {
        var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
        var rows = new List<ResourceKeyInfo>();

        foreach (var group in directory.Groups)
        {
            var resources = group.Files.ToDictionary(f => f, f => _backend.Reader.Read(f));
            var defaultFile = group.Files.FirstOrDefault(f => f.IsDefault);
            var defaultResource = defaultFile != null ? resources[defaultFile] : null;

            // Keys for this group: union across all its files.
            var keys = resources.Values
                .SelectMany(r => r.Entries.Select(e => e.Key))
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            foreach (var key in keys)
            {
                var values = new Dictionary<string, string?>();
                var isPlural = false;

                foreach (var (file, resource) in resources)
                {
                    var entry = resource.Entries.FirstOrDefault(e => e.Key == key);
                    values[string.IsNullOrEmpty(file.Code) ? "default" : file.Code] = entry?.Value;
                    if (entry?.IsPlural == true) isPlural = true;
                }

                var occurrenceCount = defaultResource?.Entries.Count(e => e.Key == key) ?? 1;

                rows.Add(new ResourceKeyInfo
                {
                    Key = key,
                    ResourceGroup = group.BaseName,
                    Values = values,
                    OccurrenceCount = occurrenceCount,
                    HasDuplicates = occurrenceCount > 1,
                    IsPlural = isPlural
                });
            }
        }

        return Ok(rows);
    }
    catch (Exception)
    {
        return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Run full suite — expect no regressions**

Run: `dotnet test LocalizationManager.Tests`
Expected: PASS. Single-base cases unaffected because each row's `ResourceGroup` is just populated; existing tests that don't inspect it still pass.

---

### Task B4: Route key edits per-group

**Files:**
- Modify: `Controllers/ResourcesController.cs` — `AddKey`, `UpdateKey`, `DeleteKey`, `GetKey`
- Modify: `Models/Api/AddKeyRequest.cs`, `UpdateKeyRequest.cs`

- [ ] **Step 1: Decide route shape**

Add `ResourceGroup` as a property on `AddKeyRequest` and `UpdateKeyRequest` (required string). For `GetKey` / `DeleteKey` (path-based routes), accept it as a query parameter `?resourceGroup=...`. Default: if only one group exists in the directory, use it; otherwise require the caller to specify.

- [ ] **Step 2: Write failing test**

Add to `MultiGroupControllerTests.cs`:

```csharp
[Fact]
public void UpdateKey_MultiBase_WritesOnlyToSpecifiedGroup()
{
    using var sandbox = TempDirectory.CopyOf(_testDataPath);
    var controller = BuildResourcesController(sandbox.Path);

    var request = new UpdateKeyRequest
    {
        ResourceGroup = "CustomerResources",
        Values = new Dictionary<string, ResourceValue>
        {
            ["it"] = new ResourceValue { Value = "Cliente NEW" }
        }
    };

    var result = controller.UpdateKey("CustomerTitle", request);
    Assert.IsType<OkObjectResult>(result.Result);

    // CustomerResources.it.resx updated
    var customerIt = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.it.resx"));
    Assert.Contains("Cliente NEW", customerIt);

    // GlassResources.it.resx untouched (no CustomerTitle key there)
    var glassIt = File.ReadAllText(Path.Combine(sandbox.Path, "GlassResources.it.resx"));
    Assert.DoesNotContain("Cliente NEW", glassIt);
}
```

You'll need a `TempDirectory` helper that copies the fixtures to a writable temp dir. Implement it inline in the test class if it doesn't exist:

```csharp
private sealed class TempDirectory : IDisposable
{
    public string Path { get; }
    private TempDirectory(string path) { Path = path; }
    public static TempDirectory CopyOf(string source)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, System.IO.Path.Combine(dir, System.IO.Path.GetFileName(file)));
        return new TempDirectory(dir);
    }
    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}
```

- [ ] **Step 3: Run test — expect FAIL**

`dotnet test --filter "FullyQualifiedName~UpdateKey_MultiBase_WritesOnlyToSpecifiedGroup"`

- [ ] **Step 4: Refactor `UpdateKey` in `ResourcesController`**

The new flow:
1. Look up the group by `request.ResourceGroup`.
2. Read only that group's files.
3. Apply updates as before, scoped to those files.
4. Reject if `ResourceGroup` is empty AND directory has >1 group (return `BadRequest("ResourceGroup is required when multiple groups exist.")`). If only one group exists, default to it for backwards compat with existing single-base callers.

Pseudocode (write out fully in the actual implementation):

```csharp
var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
var group = directory.Groups.Count == 1 && string.IsNullOrEmpty(request.ResourceGroup)
    ? directory.Groups[0]
    : directory.Groups.FirstOrDefault(g => g.BaseName == request.ResourceGroup);
if (group is null)
{
    if (directory.Groups.Count > 1 && string.IsNullOrEmpty(request.ResourceGroup))
        return BadRequest(new ErrorResponse { Error = "ResourceGroup is required when multiple groups exist." });
    return NotFound(new ErrorResponse { Error = $"Resource group '{request.ResourceGroup}' not found." });
}
var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
// ... existing per-file update loop, unchanged ...
```

Apply the same pattern to `AddKey`, `DeleteKey`, and `GetKey`.

- [ ] **Step 5: Run all tests — fix regressions**

Run: `dotnet test LocalizationManager.Tests`
Expected: PASS. Single-base tests still pass (default-to-the-only-group behavior preserves them).

---

### Task B5: `StatsController` against distinct cultures

**Files:**
- Modify: `Controllers/StatsController.cs`

- [ ] **Step 1: Write failing test**

Add to `MultiGroupControllerTests.cs` a test that asserts:
- `Languages` count == 2 (invariant + it)
- `AvgCoverage` between 0 and 100 (no overcounting)
- `MissingTranslations` >= 0

- [ ] **Step 2: Refactor `StatsController` to use `DiscoverResourceGroups`**

Compute totals/translated against the (BaseName::Key, Culture) cartesian — read once, group by culture. Reuse the pattern from B1.

- [ ] **Step 3: Run tests**

Run: `dotnet test LocalizationManager.Tests`
Expected: PASS.

---

### Task B6: Sweep remaining controllers

**Files:**
- Modify: `Controllers/ValidationController.cs`, `Controllers/ExportController.cs`, `Controllers/ImportController.cs`, `Controllers/TranslationController.cs`, `Controllers/MergeDuplicatesController.cs`, `Controllers/BackupController.cs`, `Controllers/ScanController.cs`, `Controllers/SearchController.cs`, `Controllers/LanguageController.cs` (AddLanguage/RemoveLanguage)

For each controller, find every `DiscoverLanguages(_resourcePath)` callsite and decide:
- **Iterate per group**: validations, exports, imports, merges that operate at the file level → wrap in `foreach (var group in directory.Groups)`.
- **Iterate per culture**: anything that wants "how many languages are configured" → use `directory.CultureCodes`.

- [ ] **Step 1: List every callsite**

Run: `grep -rn "DiscoverLanguages(_resourcePath)" Controllers/`

- [ ] **Step 2: For each callsite, switch to `DiscoverResourceGroups` and adjust iteration. Add at least one multi-group test per controller** (in `MultiGroupControllerTests.cs` or a per-controller file).

- [ ] **Step 3: Run full suite after every controller switch**

Run: `dotnet test LocalizationManager.Tests`
Expected: PASS.

---

## Phase C: VS Code Extension

### Task C1: Update API client types

**Files:**
- Modify: `vscode-extension/src/backend/apiClient.ts`

- [ ] **Step 1: Add `resourceGroup` to the key shape** (already done in Task B2 step 3 if you followed it through; otherwise add now).

- [ ] **Step 2: Update `updateKey` and `addKey` signatures** to accept `resourceGroup`:

```ts
async updateKey(key: string, body: { resourceGroup: string; values: { [language: string]: { value: string; comment?: string } } }): Promise<void> {
    await this.client.put(`/api/resources/keys/${encodeURIComponent(key)}`, body);
}

async addKey(body: { key: string; resourceGroup: string; values: { [language: string]: string } }): Promise<void> {
    await this.client.post('/api/resources/keys', body);
}

async deleteKey(key: string, resourceGroup: string): Promise<void> {
    await this.client.delete(`/api/resources/keys/${encodeURIComponent(key)}?resourceGroup=${encodeURIComponent(resourceGroup)}`);
}
```

- [ ] **Step 3: Build extension**

Run: `cd vscode-extension && npm run compile`
Expected: callsites that pass `addKey/updateKey/deleteKey` will now fail compilation — that's intentional. Move to C2 to fix them. Do NOT leave the build broken across a long pause — continue straight into Task C2.

---

### Task C2: Wire ResourceGroup through `resourceEditor.ts`

**Files:**
- Modify: `vscode-extension/src/views/resourceEditor.ts`

- [ ] **Step 1: Add a "Source" / "Resource Group" column to the rendered table**

Find the column-construction code (search for the column header rendering near `languageCode`/`columns =`). Add a leading column whose value is `key.resourceGroup`.

- [ ] **Step 2: Pass `resourceGroup` through the `handleUpdateKey`, `handleUpdateKeyMultiple`, `handleAddKey`, `handleDeleteKey` handlers**

The webview already passes the row's data on key edit — extend the message payload with `resourceGroup` and forward it to `apiClient.updateKey`. For example, `handleUpdateKey` becomes:

```ts
private async handleUpdateKey(key: string, resourceGroup: string, language: string, value: string, comment?: string) {
    const langCode = language || 'default';
    await this.apiClient.updateKey(key, {
        resourceGroup,
        values: { [langCode]: { value, comment: comment ?? undefined } }
    });
    // ...
}
```

Update the message dispatcher (around line 84-125) to extract `message.resourceGroup` and forward it.

- [ ] **Step 3: Update the webview HTML/JS** (look for `vscode.postMessage({ command: 'updateKey', ... })` inside the inlined webview script) to send `resourceGroup` along with `key`.

- [ ] **Step 4: Build and manual test**

Run: `cd vscode-extension && npm run compile`
Then `F5` to launch the extension host, open a project with two `.resx` files in the same folder, open the LRM editor. The "Resource Group" column should be visible and edits should write to the correct file.

---

### Task C3: Dashboard

**Files:**
- Modify: `vscode-extension/src/views/dashboard.ts`

- [ ] **Step 1: With the backend now returning one entry per culture from `/api/language`, the dashboard's "languages" count should already be correct.** Open it in the running extension host and verify. If anything looks wrong (e.g. coverage percentages), the fix likely lives in stats rendering — check whether the dashboard is computing anything locally vs trusting the API.

- [ ] **Step 2: If any local computation exists, replace it with API-provided numbers.**

---

## Phase D: Close the loop

### Task D1: Add an end-to-end integration test covering issue #6's scenario

**Files:**
- Modify: `LocalizationManager.Tests/IntegrationTests/MultiGroupControllerTests.cs`

- [ ] **Step 1: Write a test that mirrors the user's report**

```csharp
[Fact]
public void Scenario_FourBaseFilesOneCulture_ReportsOneLanguageWithCorrectCoverage()
{
    // Fixture: MultiGroupResx has 2 groups × 2 cultures.
    var langController = BuildLanguageController();
    var resourcesController = BuildResourcesController();

    var langs = ((LanguagesResponse)((OkObjectResult)langController.GetLanguages().Result!).Value!).Languages;
    Assert.Equal(2, langs.Count); // invariant + it
    Assert.All(langs, l => Assert.InRange(l.Coverage, 0, 100));
    Assert.All(langs, l => Assert.True(l.TotalKeys > 0));

    var rows = (IEnumerable<ResourceKeyInfo>)((OkObjectResult)resourcesController.GetAllKeys().Result!).Value!;
    Assert.Equal(4, rows.Count()); // 2 keys per group × 2 groups
}
```

- [ ] **Step 2: Run; expect PASS**

Run: `dotnet test LocalizationManager.Tests`
Expected: PASS.

---

### Task D2: Final verification (no commits yet — operator will decide commit grouping)

- [ ] **Step 1: Manually verify against the screenshot scenario** by opening a sample multi-base project in the extension host and confirming:
  - Dashboard shows the actual number of cultures (not files).
  - Resource editor shows one column per culture, plus a Resource Group column.
  - Editing a value writes only to that group's file.

- [ ] **Step 2: Report findings to the operator.** They will decide how to slice the diff into commits and when to open the PR (with `Closes #6`).

---

## Self-Review Notes

- **Spec coverage:** Issue #6's three complaints (N columns instead of 1 per language, `(empty)` cells, inflated stats) map to Tasks B1, B3, B5. The "Add language adds a new column instead of extending the existing language" complaint is addressed via the per-group AddLanguage refactor in Task B6.
- **Backwards compat:** Single-base directories unaffected because `DiscoverResourceGroups` returns one group; `request.ResourceGroup` defaults to it when omitted.
- **Po backend's silent dedupe (PoResourceDiscovery.cs:82-89):** Left in place for now — Po already collapses to one file per culture, so this fix surfaces no regressions there. A follow-up issue should track making Po multi-group-aware too, but it's out of scope here.
