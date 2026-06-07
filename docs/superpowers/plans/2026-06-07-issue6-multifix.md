# Issue #6 Round-3 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the four issues reported against v0.7.13 (default-language display showing an `(empty)` column, namespace segments flagged as missing keys, "Add Key" silently failing in multi-group projects, and `@inject IStringLocalizer<T>` variable names not being auto-detected) across every surface: Core, local web API, TUI, VS Code extension, cloud, and cloud↔lrm sync.

**Architecture:** Core (`LocalizationManager.Core`) is shared by the CLI, the local web API (`Controllers/`, `Pages/`), the TUI (`UI/`), and the cloud API (`cloud/src/LrmCloud.Api`, which has a `ProjectReference` to Core). Fixes pushed into Core therefore propagate to most surfaces automatically; the per-surface tasks below wire config through and fix surface-specific UI. The VS Code extension is a TypeScript webview that talks to the local web API over HTTP — its bugs are front-end only.

**Tech Stack:** .NET 9 / C# / xUnit (Core, web API, cloud), TypeScript / Jest (VS Code extension), Terminal.Gui (TUI), Spectre.Console (CLI).

**Effective-code definition (used throughout):** a file's *effective language code* is `Code` if non-empty, else `"default"`. Two files in the same group "collide" when they have the same effective code (e.g. the suffix-less default file labeled `it` and an explicit `.it.resx`).

**Merge precedence (decided with user):** when files in a group collide, the **default file wins** for display, and the collision is **surfaced as a conflict** (not silently dropped).

---

## File Structure

**Core (shared):**
- Modify: `LocalizationManager.Core/Backends/Resx/ResxResourceDiscovery.cs` — already adopts `defaultLanguageCode`; add a unit-tested merge helper is NOT here (merge happens at read path), but add discovery dedup note.
- Modify: `LocalizationManager.Core/Scanning/Scanners/CSharpScanner.cs` — strip `namespace`/`using` lines before regex; honor injected localizer names.
- Modify: `LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs` — strip directives; parse `@inject IStringLocalizer<T> Var`; accept imported localizer names.
- Create: `LocalizationManager.Core/Scanning/Scanners/InjectedLocalizerExtractor.cs` — parses `@inject IStringLocalizer<T> Var` from razor content.
- Modify: `LocalizationManager.Core/Scanning/CodeScanner.cs` — collect `_Imports.razor` injected names and pass them down.
- Modify: `LocalizationManager.Core/Scanning/PatternMatcher.cs` — add optional `injectedLocalizerVariables` parameter to `ScanFile`/`ScanContent`.
- Create: `LocalizationManager.Core/Models/MergedLanguageColumns.cs` — helper that merges group files into display columns with default-wins + conflict detection.

**Local web API:**
- Modify: `Commands/WebCommand.cs` — pass `config` to `factory.GetBackend`/`ResolveFromPath`.
- Modify: `Controllers/ResourcesController.cs` — `GetAllKeys` uses the merge helper; expose group list endpoint already exists? add `Conflicts` to `ResourceKeyInfo`.
- Modify: `Models/ResourceKeyInfo.cs` (or wherever it lives) — add `HasLanguageConflict`/`ConflictingLanguages`.

**TUI:**
- Modify: `UI/ResourceEditorWindow.cs` — build columns via the same merge helper so default+culture collisions collapse.

**VS Code extension:**
- Modify: `vscode-extension/src/views/resourceEditor.ts` — add resource-group `<select>` to Add-Key modal, send `resourceGroup`, surface API errors.
- Modify: `vscode-extension/src/backend/apiClient.ts` — ensure `addKey` surfaces non-2xx as thrown errors with the server message.
- Create: `vscode-extension/src/test/unit/addKeyMessage.test.ts` — unit test for the message payload builder (extract a pure function).

**Cloud:**
- Verify/Modify: `cloud/src/LrmCloud.Api` resource-column building — ensure it uses the Core merge helper (or replicate default-wins) and passes `DefaultLanguageCode`.

**Tests:**
- `LocalizationManager.Tests/UnitTests/Scanning/CSharpScannerTests.cs`
- `LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs`
- `LocalizationManager.Tests/UnitTests/Scanning/InjectedLocalizerExtractorTests.cs` (new)
- `LocalizationManager.Tests/UnitTests/MergedLanguageColumnsTests.cs` (new)
- `LocalizationManager.Tests/UnitTests/ResourceDiscoveryTests.cs`
- `LocalizationManager.Tests/IntegrationTests/` web API add-key + get-keys tests
- `cloud/tests/LrmCloud.Tests` default-language column test
- `vscode-extension/src/test/unit/addKeyMessage.test.ts`

---

## Task 1: Bug #2 — C# scanner ignores `namespace`/`using` declarations

**Files:**
- Modify: `LocalizationManager.Core/Scanning/Scanners/CSharpScanner.cs:78-95` (extend `RemoveComments` → add directive stripping)
- Test: `LocalizationManager.Tests/UnitTests/Scanning/CSharpScannerTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `CSharpScannerTests.cs`:

```csharp
[Fact]
public void ScanContent_NamespaceWithResourcesSegment_IsNotFlagged()
{
    var scanner = new CSharpScanner();
    var code = @"
namespace Vitrum.Resources.Components.Account.Pages
{
    using Vitrum.Resources.Shared;
    public class Login
    {
        public string M() => Resources.WelcomeMessage;
    }
}";
    var refs = scanner.ScanContent("Login.cs", code);

    Assert.Contains(refs, r => r.Key == "WelcomeMessage");
    Assert.DoesNotContain(refs, r => r.Key == "Components");
    Assert.DoesNotContain(refs, r => r.Key == "Shared");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ScanContent_NamespaceWithResourcesSegment_IsNotFlagged"`
Expected: FAIL — `Components` (and `Shared`) are currently detected.

- [ ] **Step 3: Strip namespace/using lines before scanning**

In `CSharpScanner.cs`, change `ScanContent` so the cleaned content also has directive lines blanked. Replace the body around line 50:

```csharp
        // Remove comments to avoid false positives
        var cleanedContent = RemoveComments(originalContent);

        // Blank out namespace/using declaration lines so dotted names like
        // `Vitrum.Resources.Components` are not mistaken for `Resources.Components`
        // property access. Preserve line count by replacing with spaces.
        cleanedContent = RemoveNamespaceAndUsingLines(cleanedContent);
```

Add this method to `CSharpScanner` (after `RemoveComments`):

```csharp
    /// <summary>
    /// Blanks out C# <c>namespace</c> and <c>using</c> declaration lines while
    /// preserving line numbers, so namespace-qualified type names are not mistaken
    /// for resource property access (e.g. <c>namespace A.Resources.Components</c>).
    /// </summary>
    private static string RemoveNamespaceAndUsingLines(string content)
    {
        // Matches whole lines that are namespace or using declarations:
        //   namespace Foo.Bar.Baz   (block or file-scoped, with optional trailing { or ;)
        //   using Foo.Bar;          (incl. `using static`, `global using`, aliases)
        // It deliberately does NOT match `using (...)` statements or `using var`.
        var pattern = @"^[ \t]*(?:global[ \t]+)?(?:namespace[ \t]+[\w.]+|using[ \t]+(?:static[ \t]+)?[\w.]+(?:[ \t]*=[ \t]*[\w.<>,? ]+)?[ \t]*;?)[ \t]*\{?[ \t]*$";
        return Regex.Replace(content, pattern, m => new string(' ', m.Value.Length),
            RegexOptions.Multiline);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ScanContent_NamespaceWithResourcesSegment_IsNotFlagged"`
Expected: PASS

- [ ] **Step 5: Add extensive edge-case tests (user asked for extensive testing)**

Add to `CSharpScannerTests.cs`:

```csharp
[Theory]
[InlineData("namespace A.Resources.Components.Pages;")]            // file-scoped
[InlineData("namespace A.Resources.Components.Pages {")]           // block, brace same line
[InlineData("    namespace A.Resources.Components {")]             // indented
[InlineData("using A.Resources.Components;")]                       // using
[InlineData("using static A.Resources.Components;")]               // using static
[InlineData("global using A.Resources.Components;")]               // global using
[InlineData("using Res = A.Resources.Components;")]                // alias
public void ScanContent_DirectiveLines_ProduceNoKeys(string line)
{
    var scanner = new CSharpScanner();
    var refs = scanner.ScanContent("F.cs", line + "\npublic class C {}");
    Assert.Empty(refs);
}

[Fact]
public void ScanContent_RealUsageAfterDirectives_StillDetected()
{
    var scanner = new CSharpScanner();
    var code = "using A.Resources.Components;\nnamespace X.Resources.Y;\nclass C { string s = Resources.Hello; }";
    var refs = scanner.ScanContent("F.cs", code);
    Assert.Single(refs);
    Assert.Equal("Hello", refs[0].Key);
}

[Fact]
public void ScanContent_UsingStatement_NotStripped()
{
    // `using (var x = ...)` and `using var` are statements, not directives;
    // they must not be blanked (they could contain Resources.X access).
    var scanner = new CSharpScanner();
    var code = "class C { void M() { using var d = Open(); var t = Resources.Title; } }";
    var refs = scanner.ScanContent("F.cs", code);
    Assert.Contains(refs, r => r.Key == "Title");
}
```

- [ ] **Step 6: Run the full scanner test class**

Run: `dotnet test --filter "FullyQualifiedName~CSharpScannerTests"`
Expected: PASS (all)

- [ ] **Step 7: Commit**

```bash
git add LocalizationManager.Core/Scanning/Scanners/CSharpScanner.cs LocalizationManager.Tests/UnitTests/Scanning/CSharpScannerTests.cs
git commit -m "fix(scan): ignore namespace/using declarations in C# scanner (issue #6)"
```

---

## Task 2: Bug #2 — Razor scanner ignores directive lines too

The Razor `ScanResourceProperties` requires an `@` prefix so top-level `namespace` lines are less likely, but `_Imports.razor` and code blocks can still contain `@using`/qualified names. Apply the same defense for consistency.

**Files:**
- Modify: `LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs:40-72`
- Test: `LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void ScanContent_RazorUsingDirective_NotFlagged()
{
    var scanner = new RazorScanner();
    var code = "@using Vitrum.Resources.Components\n<p>@Resources.Hello</p>";
    var refs = scanner.ScanContent("Page.razor", code);

    Assert.Contains(refs, r => r.Key == "Hello");
    Assert.DoesNotContain(refs, r => r.Key == "Components");
}
```

- [ ] **Step 2: Run test to verify it fails or passes**

Run: `dotnet test --filter "FullyQualifiedName~ScanContent_RazorUsingDirective_NotFlagged"`
Expected: It may already PASS (the `@Resources.` pattern requires `@` immediately before `Resources`, and `@using Vitrum.Resources.Components` has `Resources` mid-token). If it PASSES, keep the test as a regression guard and skip to Step 4. If it FAILS, do Step 3.

- [ ] **Step 3: Strip `@using`/`@namespace` directive lines in ScanContent**

In `RazorScanner.ScanContent`, after computing `content` is used directly; introduce a cleaned copy at the top of the method (line 47 area):

```csharp
        var references = new List<KeyReference>();

        if (string.IsNullOrEmpty(content))
            return references;

        // Blank Razor directive lines (@using / @namespace) so qualified type
        // names are not mistaken for @Resources.Member access. Preserve line count.
        content = Regex.Replace(content,
            @"^[ \t]*@(?:using|namespace)[ \t]+[\w.]+[ \t]*$",
            m => new string(' ', m.Value.Length),
            RegexOptions.Multiline);
```

Add `using System.Text.RegularExpressions;` (already present).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~RazorScannerTests"`
Expected: PASS (all)

- [ ] **Step 5: Commit**

```bash
git add LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs
git commit -m "fix(scan): ignore @using/@namespace directives in Razor scanner (issue #6)"
```

---

## Task 3: Feature — extract injected localizer variable names from Razor

**Files:**
- Create: `LocalizationManager.Core/Scanning/Scanners/InjectedLocalizerExtractor.cs`
- Test: `LocalizationManager.Tests/UnitTests/Scanning/InjectedLocalizerExtractorTests.cs`

- [ ] **Step 1: Write the failing test (new file)**

```csharp
using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class InjectedLocalizerExtractorTests
{
    [Theory]
    [InlineData("@inject IStringLocalizer<QuoteResources> Q", "Q")]
    [InlineData("@inject IStringLocalizer<SharedResources> Loc", "Loc")]
    [InlineData("@inject IHtmlLocalizer<App> H", "H")]
    [InlineData("@inject  IStringLocalizer<A.B.C>   MyLoc  ", "MyLoc")]
    public void Extract_FindsInjectedLocalizerVariable(string line, string expected)
    {
        var names = InjectedLocalizerExtractor.Extract(line);
        Assert.Contains(expected, names);
    }

    [Fact]
    public void Extract_IgnoresNonLocalizerInjects()
    {
        var names = InjectedLocalizerExtractor.Extract("@inject NavigationManager Nav");
        Assert.Empty(names);
    }

    [Fact]
    public void Extract_FindsMultipleAcrossLines()
    {
        var content = "@inject IStringLocalizer<A> Q\n@inject IStringLocalizer<B> Loc\n<p>hi</p>";
        var names = InjectedLocalizerExtractor.Extract(content);
        Assert.Equal(new[] { "Q", "Loc" }, names);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~InjectedLocalizerExtractorTests"`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the extractor**

Create `LocalizationManager.Core/Scanning/Scanners/InjectedLocalizerExtractor.cs`:

```csharp
using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// Extracts variable names that are declared as injected localizers in Razor
/// content, e.g. <c>@inject IStringLocalizer&lt;T&gt; VariableName</c> (also
/// IHtmlLocalizer / IStringLocalizerFactory). These names are then treated as
/// localizer indexers regardless of the configured method list, so that
/// <c>@Q["Key"]</c> resolves even when the variable name is project-specific.
/// </summary>
public static class InjectedLocalizerExtractor
{
    // @inject IStringLocalizer<Foo.Bar> VarName
    // @inject IHtmlLocalizer<Foo> VarName
    private static readonly Regex InjectPattern = new(
        @"@inject\s+I(?:String|Html)Localizer(?:<[^>]+>)?\s+(\w+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns the distinct injected-localizer variable names found in
    /// <paramref name="content"/>, in first-seen order.
    /// </summary>
    public static IReadOnlyList<string> Extract(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<string>();

        var seen = new List<string>();
        foreach (Match m in InjectPattern.Matches(content))
        {
            var name = m.Groups[1].Value;
            if (!seen.Contains(name))
                seen.Add(name);
        }
        return seen;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~InjectedLocalizerExtractorTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalizationManager.Core/Scanning/Scanners/InjectedLocalizerExtractor.cs LocalizationManager.Tests/UnitTests/Scanning/InjectedLocalizerExtractorTests.cs
git commit -m "feat(scan): parse @inject IStringLocalizer<T> variable names (issue #6)"
```

---

## Task 4: Feature — Razor scanner honors injected localizer names (per-file)

**Files:**
- Modify: `LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs` (`ScanContent`, `ScanLocalizerIndexers`, `IsLikelyLocalizerVariable`)
- Test: `LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void ScanContent_InjectedLocalizerVariable_DetectsIndexerUsage()
{
    var scanner = new RazorScanner();
    var code = "@inject IStringLocalizer<QuoteResources> Q\n<p>@Q[\"Customer_BusinessName_Label\"]</p>";
    var refs = scanner.ScanContent("Page.razor", code);

    Assert.Contains(refs, r => r.Key == "Customer_BusinessName_Label");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ScanContent_InjectedLocalizerVariable_DetectsIndexerUsage"`
Expected: FAIL — `Q` is not in the heuristic name list.

- [ ] **Step 3: Thread injected names through ScanContent → indexer scan**

In `RazorScanner.cs`:

(a) At the top of `ScanContent` (after the directive-stripping from Task 2, before the scan calls), extract per-file injected names and combine with any externally supplied ones. Change the indexer scan call to pass them:

```csharp
        // Variable names declared via @inject IStringLocalizer<T> Var in THIS file.
        var injectedNames = InjectedLocalizerExtractor.Extract(content).ToList();

        // Scan for @Resources.KeyName patterns
        ScanResourceProperties(content, filePath, references, classNames);

        // Scan for @Localizer["KeyName"] patterns (now honoring injected names)
        ScanLocalizerIndexers(content, filePath, references, injectedNames);
```

(b) Change `ScanLocalizerIndexers` signature and the guard:

```csharp
    private void ScanLocalizerIndexers(string content, string filePath, List<KeyReference> references, List<string> injectedNames)
    {
        var matches = LocalizerIndexerPattern.Matches(content);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            var keyName = match.Groups[2].Value;

            // Detect if the variable is a known localizer: either an injected
            // localizer variable in this file, or matches the name heuristic.
            if (injectedNames.Contains(variableName) || IsLikelyLocalizerVariable(variableName))
            {
                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(content, match.Index),
                    Pattern = match.Value,
                    Context = GetContext(content, match.Index),
                    Confidence = ConfidenceLevel.High
                });
            }
        }
    }
```

(c) Add `using LocalizationManager.Core.Scanning.Scanners;` is unnecessary (same namespace). Ensure `InjectedLocalizerExtractor` is reachable (same namespace `LocalizationManager.Core.Scanning.Scanners`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~RazorScannerTests"`
Expected: PASS (all)

- [ ] **Step 5: Commit**

```bash
git add LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs
git commit -m "feat(scan): detect indexer usage of per-file injected localizers (issue #6)"
```

---

## Task 5: Feature — `_Imports.razor` injected names apply project/folder-wide

**Files:**
- Modify: `LocalizationManager.Core/Scanning/PatternMatcher.cs` — add optional `injectedLocalizerVariables` param to `ScanFile`/`ScanContent` signatures (default null).
- Modify: `LocalizationManager.Core/Scanning/Scanners/RazorScanner.cs` — accept & union external injected names.
- Modify: `LocalizationManager.Core/Scanning/Scanners/CSharpScanner.cs` — accept external names for `IsLikelyLocalizerVariable` (so C# indexer usage of injected names works too if relevant) OR ignore (Razor-focused). Implement to accept and union for indexer detection.
- Modify: `LocalizationManager.Core/Scanning/CodeScanner.cs` — discover `_Imports.razor` files (project root + each folder), build a map of folder → applicable injected names, and pass them when scanning each file.
- Test: `LocalizationManager.Tests/UnitTests/Scanning/RazorScannerTests.cs` + a CodeScanner integration test.

> **Read first:** Open `PatternMatcher.cs` and `CodeScanner.cs` fully before editing — the exact abstract signatures and the file-iteration loop must be matched. The signatures shown below are the target; adjust call sites to compile.

- [ ] **Step 1: Write the failing CodeScanner test**

`CodeScanner.Scan(sourcePath, resourceFiles, strictMode, excludePatterns, resourceClassNames, localizationMethods, excludeFromMissing)` builds `ResourcePath` from `resourceFiles.First()`, so the test MUST pass a non-empty `resourceFiles` list. `ScanResult` has no `AllReferences`; assert via `MissingKeys` (a `List<KeyUsage>`, each with `.Key`) — the injected-localizer key must NOT be reported missing when it exists in resources, and `TotalReferences` must be > 0.

Create `LocalizationManager.Tests/UnitTests/Scanning/CodeScannerImportsTests.cs`:

```csharp
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class CodeScannerImportsTests
{
    [Fact]
    public void Scan_ImportsRazorInjectedLocalizer_AppliesToSiblingFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lrm_imports_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "_Imports.razor"),
                "@inject IStringLocalizer<SharedResources> Loc\n");
            File.WriteAllText(Path.Combine(dir, "Page.razor"),
                "<p>@Loc[\"Welcome_Title\"]</p>\n");

            // Resource file (in the same dir) that DEFINES the key, so a resolved
            // reference is NOT counted as missing.
            var resxPath = Path.Combine(dir, "SharedResources.resx");
            File.WriteAllText(resxPath, "<root></root>"); // path only needs to exist for ResourcePath
            var resourceFiles = new List<ResourceFile>
            {
                new ResourceFile
                {
                    Language = new LanguageInfo { Code = "", Name = "Default", IsDefault = true, FilePath = resxPath },
                    Entries = { new ResourceEntry { Key = "Welcome_Title", Value = "Welcome" } }
                }
            };

            var scanner = new CodeScanner();
            var result = scanner.Scan(dir, resourceFiles, false, null, null, null);

            // The @Loc["Welcome_Title"] usage was detected (so it resolved against
            // the resource) → it is NOT in MissingKeys, and references were found.
            Assert.True(result.TotalReferences > 0);
            Assert.DoesNotContain(result.MissingKeys, m => m.Key == "Welcome_Title");
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

> Confirm the real `ResourceFile`/`ResourceEntry`/`LanguageInfo` constructors and collection initializers compile (e.g. whether `Entries` is settable or init-only with an initializer). Adjust to the actual model API while keeping the assertions.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~CodeScannerImportsTests"`
Expected: FAIL — `Loc` from `_Imports.razor` is not applied to `Page.razor`.

- [ ] **Step 3: Add optional param to PatternMatcher abstract API**

In `PatternMatcher.cs`, extend the abstract/virtual methods to accept injected names (keep existing overloads working by adding a defaulted parameter):

```csharp
    public abstract List<KeyReference> ScanFile(
        string filePath,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null,
        List<string>? injectedLocalizerVariables = null);

    public abstract List<KeyReference> ScanContent(
        string filePath,
        string content,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null,
        List<string>? injectedLocalizerVariables = null);
```

Update **all** overriding scanners (`CSharpScanner`, `RazorScanner`, `XamlScanner`, and any others) to match the new signature. For `XamlScanner` just accept and ignore the new param.

- [ ] **Step 4: Union external names in RazorScanner**

In `RazorScanner.ScanContent`, change the injected-names line:

```csharp
        var injectedNames = InjectedLocalizerExtractor.Extract(content).ToList();
        if (injectedLocalizerVariables != null)
            injectedNames.AddRange(injectedLocalizerVariables.Where(n => !injectedNames.Contains(n)));
```

And make `ScanFile` forward the param to `ScanContent`.

- [ ] **Step 5: Wire `_Imports.razor` discovery in CodeScanner**

In `CodeScanner.cs`, before scanning each source file, build the set of injected names that apply to it: names from every `_Imports.razor` at or above the file's folder (up to the scan root), plus the project-root `_Imports.razor`. Pass them to the scanner call.

Implementation sketch (adapt to the real loop/structure):

```csharp
    // Collect _Imports.razor injected localizers keyed by their containing directory.
    private Dictionary<string, List<string>> BuildImportsMap(string rootPath)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var imp in Directory.EnumerateFiles(rootPath, "_Imports.razor", SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(imp)!;
            map[dir] = InjectedLocalizerExtractor.Extract(File.ReadAllText(imp)).ToList();
        }
        return map;
    }

    // Names that apply to a file = union of all _Imports.razor at or above its folder.
    private List<string> InjectedNamesFor(string filePath, string rootPath, Dictionary<string, List<string>> importsMap)
    {
        var result = new List<string>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        var root = Path.GetFullPath(rootPath);
        while (dir != null && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (importsMap.TryGetValue(dir, out var names))
                foreach (var n in names) if (!result.Contains(n)) result.Add(n);
            if (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase)) break;
            dir = Path.GetDirectoryName(dir);
        }
        return result;
    }
```

Then in the per-file scan call, pass `InjectedNamesFor(file, rootPath, importsMap)` as the new `injectedLocalizerVariables` argument.

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~CodeScannerImportsTests"` then `dotnet test --filter "FullyQualifiedName~Scanning"`
Expected: PASS (all)

- [ ] **Step 7: Commit**

```bash
git add LocalizationManager.Core/Scanning/ LocalizationManager.Tests/UnitTests/Scanning/
git commit -m "feat(scan): apply _Imports.razor injected localizers folder-wide (issue #6)"
```

---

## Task 5b: Web/VS Code scan path — apply configured methods, union multi-group keys, thread injected names

The web API `ScanController` (which fuels VS Code diagnostics) currently (a) passes `null, null` for `resourceClassNames`/`localizationMethods` so configured methods are ignored, and (b) builds the resource-key set from **only** the single `defaultFile` (`ScanController.cs:47`), so keys that live in other resource groups are still flagged `missing-key`. Both must be fixed for Bug #2's missing-key half and the @inject feature to reach VS Code.

**Files:**
- Modify: `Controllers/ScanController.cs` (the full-scan `Scan` action ~line 58, and `ScanFile`/single-file actions ~130-131)
- Modify: `LocalizationManager.Core/Scanning/CodeScanner.cs` — `ScanSingleFile`/`ScanSingleFileContent` must accept/forward `injectedLocalizerVariables` (added in Task 5) and the full `Scan` already unions keys via `GetAllResourceKeys(resourceFiles)`; ensure ScanController passes ALL group files, not just one.
- Test: `LocalizationManager.Tests/IntegrationTests/` (ScanController multi-group + configured-method test)

> **Read first:** `Controllers/ScanController.cs` in full, and `CodeScanner.GetAllResourceKeys` — confirm whether `Scan` already unions across the passed `resourceFiles`. The fix is to pass every group's files and the configured methods, not to change union logic if it already unions.

- [ ] **Step 1: Write the failing integration test** — a project with `CustomerResources.resx` (key `Customer_BusinessName_Label`) and `SharedResources.resx` (other keys), plus a `.razor` using `@L["Customer_BusinessName_Label"]`. POST `/api/scan` and assert `Customer_BusinessName_Label` is NOT in `Missing`. Then a second case: configure `localizationMethods: ["Q"]`, use `@Q["X"]` where `X` exists, assert not missing.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ScanController"`
Expected: FAIL — key flagged missing (single-group key set) and/or `Q` ignored.

- [ ] **Step 3: Pass all group files + configured methods in ScanController.** Build `resourceFiles` from every group/language (not just `defaultFile`), compute the resource-key union from all of them, and pass `config.Scanning?.ResourceClassNames` / `config.Scanning?.LocalizationMethods` (inject `ConfigurationService`/`LoadedConfiguration` into the controller) into `_scanner.Scan(...)`. For single-file actions, forward the same config and the injected-localizer names.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ScanController"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Controllers/ScanController.cs LocalizationManager.Core/Scanning/CodeScanner.cs LocalizationManager.Tests/IntegrationTests/
git commit -m "fix(web): scan applies configured methods, unions multi-group keys, honors @inject (issue #6)"
```

---

## Task 6: Bug #1 — merge helper (default-wins + conflict detection) in Core

**Files:**
- Create: `LocalizationManager.Core/Models/MergedLanguageColumns.cs`
- Test: `LocalizationManager.Tests/UnitTests/MergedLanguageColumnsTests.cs`

This is the single source of truth used by the web API, TUI, and cloud so display logic stays consistent.

- [ ] **Step 1: Write the failing test (new file)**

```csharp
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class MergedLanguageColumnsTests
{
    private static LanguageInfo F(string code, bool isDefault, string path) =>
        new() { BaseName = "Res", Code = code, Name = code, IsDefault = isDefault, FilePath = path };

    [Fact]
    public void Merge_DefaultLabeledSameAsCulture_CollapsesToOneColumn_DefaultWins()
    {
        // Default file labeled "it" (because DefaultLanguageCode=it) AND a real .it.resx
        var files = new[] { F("it", true, "Res.resx"), F("it", false, "Res.it.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Single(cols);                              // one "it" column
        Assert.Equal("it", cols[0].Code);
        Assert.Equal("Res.resx", cols[0].WinningFilePath); // default wins
        Assert.True(cols[0].HasConflict);
        Assert.Contains("Res.it.resx", cols[0].ConflictingFilePaths);
    }

    [Fact]
    public void Merge_DistinctCodes_KeepsSeparateColumns_NoConflict()
    {
        var files = new[] { F("it", true, "Res.resx"), F("en", false, "Res.en.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Equal(2, cols.Count);
        Assert.All(cols, c => Assert.False(c.HasConflict));
    }

    [Fact]
    public void Merge_EmptyDefaultCode_UsesDefaultBucket()
    {
        var files = new[] { F("", true, "Res.resx"), F("fr", false, "Res.fr.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Equal(2, cols.Count);
        Assert.Contains(cols, c => c.Code == "default");
        Assert.Contains(cols, c => c.Code == "fr");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~MergedLanguageColumnsTests"`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the merge helper**

Create `LocalizationManager.Core/Models/MergedLanguageColumns.cs`:

```csharp
namespace LocalizationManager.Core.Models;

/// <summary>
/// A single display column after merging the files of one resource group by
/// effective language code. When two files map to the same code (e.g. the
/// suffix-less default file labeled with the configured DefaultLanguageCode and
/// an explicit culture file with the same code), they collapse into one column;
/// the default file's value wins and the collision is surfaced via
/// <see cref="HasConflict"/>.
/// </summary>
public sealed class LanguageColumn
{
    /// <summary>Effective code: the file Code, or "default" when blank.</summary>
    public required string Code { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>Path of the file whose value is shown for this column (default wins).</summary>
    public required string WinningFilePath { get; init; }

    /// <summary>True when more than one file mapped to this code.</summary>
    public bool HasConflict { get; init; }

    /// <summary>Paths of the other files that collided with the winner (excludes the winner).</summary>
    public IReadOnlyList<string> ConflictingFilePaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Merges a resource group's files into display columns. Shared by the web API,
/// TUI and cloud so column semantics are identical everywhere.
/// </summary>
public static class MergedLanguageColumns
{
    public static string EffectiveCode(LanguageInfo f) =>
        string.IsNullOrEmpty(f.Code) ? "default" : f.Code;

    public static IReadOnlyList<LanguageColumn> Build(IEnumerable<LanguageInfo> files)
    {
        var columns = new List<LanguageColumn>();

        foreach (var grp in files.GroupBy(EffectiveCode, StringComparer.OrdinalIgnoreCase))
        {
            // Default file wins; otherwise first file in the group.
            var ordered = grp.OrderByDescending(f => f.IsDefault).ToList();
            var winner = ordered[0];
            var losers = ordered.Skip(1).Select(f => f.FilePath ?? string.Empty).ToList();

            columns.Add(new LanguageColumn
            {
                Code = grp.Key,
                Name = winner.Name,
                IsDefault = ordered.Any(f => f.IsDefault),
                WinningFilePath = winner.FilePath ?? string.Empty,
                HasConflict = ordered.Count > 1,
                ConflictingFilePaths = losers
            });
        }

        // default column first, then alphabetical
        return columns
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~MergedLanguageColumnsTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalizationManager.Core/Models/MergedLanguageColumns.cs LocalizationManager.Tests/UnitTests/MergedLanguageColumnsTests.cs
git commit -m "feat(core): add language-column merge helper with default-wins + conflict (issue #6)"
```

---

## Task 7: Bug #1 — local web API: pass config to backend factory

**Files:**
- Modify: `Commands/WebCommand.cs:199-203`
- Test: covered indirectly by Task 8 integration test; add a focused note.

- [ ] **Step 1: Make the edit**

In `WebCommand.cs`, replace lines 199-203:

```csharp
            if (!string.IsNullOrEmpty(format))
                return factory.GetBackend(format, config);

            // Auto-detect from path (pass config so DefaultLanguageCode is honored)
            return factory.ResolveFromPath(absoluteResourcePath, config);
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build LocalizationManager.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Commands/WebCommand.cs
git commit -m "fix(web): pass config (DefaultLanguageCode) to backend factory (issue #6)"
```

---

## Task 8: Bug #1 — local web API `GetAllKeys` merges collided columns

**Files:**
- Modify: `Controllers/ResourcesController.cs:55-107` (`GetAllKeys`)
- Modify: `ResourceKeyInfo` model (find with `grep -rn "class ResourceKeyInfo" --include=*.cs`) — add conflict fields.
- Test: `LocalizationManager.Tests/IntegrationTests/` (add `ResourcesControllerGetKeysTests` or extend existing controller test).

- [ ] **Step 1: Add conflict fields to ResourceKeyInfo**

Locate the model and add:

```csharp
    /// <summary>True when, for this row, two files map to the same language code.</summary>
    public bool HasLanguageConflict { get; set; }

    /// <summary>Codes that had a default-vs-culture collision for this group.</summary>
    public List<string> ConflictingLanguages { get; set; } = new();
```

- [ ] **Step 2: Write the failing integration test**

Create a temp directory with `Res.resx` (key `Hi` = "Ciao") and `Res.it.resx` (key `Hi` = "CiaoCulture"), set `DefaultLanguageCode=it` in `lrm.json`, start the API/controller, GET `/api/resources/keys`, and assert:

```csharp
// Expect ONE column "it" whose value is the DEFAULT file's value ("Ciao"),
// HasLanguageConflict == true, and no "(empty)"/"default" column.
Assert.True(row.Values.ContainsKey("it"));
Assert.Equal("Ciao", row.Values["it"]);          // default wins
Assert.False(row.Values.ContainsKey("default"));
Assert.True(row.HasLanguageConflict);
```

> Match the existing integration-test harness in `LocalizationManager.Tests/IntegrationTests/` (look for how a controller is constructed with a backend + resource path). If controllers are exercised via `CommandTestHelper`/WebApplicationFactory, follow that pattern.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ResourcesControllerGetKeys"`
Expected: FAIL — currently two columns (`default` empty/label + `it`), or non-deterministic.

- [ ] **Step 4: Rewrite the cell-building loop using the merge helper**

In `GetAllKeys`, replace the inner `values` construction (lines 77-97) so it merges by column instead of keying on raw `file.Code`:

```csharp
                // Build display columns: collapse files that share an effective
                // language code (default-wins) and record conflicts.
                var columns = MergedLanguageColumns.Build(group.Files);

                foreach (var key in keys)
                {
                    var values = new Dictionary<string, string?>();
                    var isPlural = false;
                    var conflictCodes = new List<string>();

                    foreach (var col in columns)
                    {
                        // Winner value first; fall back to a colliding file if the
                        // winner doesn't define this key (default-wins, culture fills gaps).
                        var winnerFile = group.Files.First(f => (f.FilePath ?? "") == col.WinningFilePath);
                        var winnerEntry = resources[winnerFile].Entries.FirstOrDefault(e => e.Key == key);

                        string? value = winnerEntry?.Value;
                        if (string.IsNullOrEmpty(value))
                        {
                            foreach (var lp in col.ConflictingFilePaths)
                            {
                                var lf = group.Files.First(f => (f.FilePath ?? "") == lp);
                                var le = resources[lf].Entries.FirstOrDefault(e => e.Key == key);
                                if (!string.IsNullOrEmpty(le?.Value)) { value = le.Value; break; }
                            }
                        }

                        values[col.Code] = value;
                        if (winnerEntry?.IsPlural == true) isPlural = true;
                        if (col.HasConflict) conflictCodes.Add(col.Code);
                    }

                    var occurrenceCount = defaultResource?.Entries.Count(e => e.Key == key) ?? 1;

                    rows.Add(new ResourceKeyInfo
                    {
                        Key = key,
                        ResourceGroup = group.BaseName,
                        Values = values,
                        OccurrenceCount = occurrenceCount,
                        HasDuplicates = occurrenceCount > 1,
                        IsPlural = isPlural,
                        HasLanguageConflict = conflictCodes.Count > 0,
                        ConflictingLanguages = conflictCodes
                    });
                }
```

Add `using LocalizationManager.Core.Models;` if not already imported.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ResourcesControllerGetKeys"`
Expected: PASS

- [ ] **Step 6: Run the full web/integration suite**

Run: `dotnet test --filter "FullyQualifiedName~IntegrationTests"`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Controllers/ResourcesController.cs Models/ LocalizationManager.Tests/IntegrationTests/
git commit -m "fix(web): merge default+culture columns with default-wins in GetAllKeys (issue #6)"
```

---

## Task 9: Bug #1 — surface the conflict in the Editor.razor grid

**Files:**
- Modify: `Pages/Editor.razor` (column template around lines 227-250)

- [ ] **Step 1: Read Editor.razor** to confirm the language-column loop and how `ResourceKeyInfo` rows are bound.

- [ ] **Step 2: Add a conflict indicator** to each language cell when `item.HasLanguageConflict && item.ConflictingLanguages.Contains(langCode)`:

```razor
                @if (item.HasLanguageConflict && item.ConflictingLanguages.Contains(langCode))
                {
                    <span class="rz-color-warning" title="Both the default file and a .@(langCode).resx define this language; showing the default file's value.">⚠</span>
                }
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build LocalizationManager.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Pages/Editor.razor
git commit -m "feat(web): show language-collision warning in editor grid (issue #6)"
```

---

## Task 10: Bug #1 — TUI uses the same merge so columns collapse

**Files:**
- Modify: `UI/ResourceEditorWindow.cs` (column construction from discovered languages)
- Test: headless TUI test per `[[tui-was-flat-single-group]]` memory note (see `LocalizationManager.Tests` for the existing TUI test harness).

- [ ] **Step 1: Locate column building** — `grep -n "DiscoverLanguages\|Code\|columns\|AddColumn" UI/ResourceEditorWindow.cs` to find where per-language columns are created.

- [ ] **Step 2: Write/extend a headless test** asserting that with a default file labeled `it` plus a `.it.resx`, the editor exposes ONE `it` column (follow the existing headless Terminal.Gui test pattern referenced in memory `tui-was-flat-single-group`).

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ResourceEditorWindow"` (or the actual TUI test class name)
Expected: FAIL — two `it` columns.

- [ ] **Step 4: Replace column building with `MergedLanguageColumns.Build(group.Files)`** and read each cell from the column's `WinningFilePath` (falling back to colliding files for empty values), mirroring Task 8.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ResourceEditorWindow"`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add UI/ResourceEditorWindow.cs LocalizationManager.Tests/
git commit -m "fix(tui): collapse default+culture columns via shared merge helper (issue #6)"
```

---

## Task 11: Bug #1 — cloud honors DefaultLanguageCode + merge

**Files:**
- Verify/Modify: `cloud/src/LrmCloud.Api` resource-reading services (search: `grep -rn "DiscoverLanguages\|DefaultLanguageCode\|file.Code\|\.Code\b" cloud/src/LrmCloud.Api`)
- Test: `cloud/tests/LrmCloud.Tests`

- [ ] **Step 1: Find where cloud builds language columns / reads resources** for the dashboard and OTA/push paths. Determine whether it calls Core discovery (then it inherits the fix once config flows) or builds columns itself.

- [ ] **Step 2: Ensure DefaultLanguageCode flows.** `GitHubFormatResolver` already deserializes `DefaultLanguageCode` but only returns format. Where cloud constructs a Core backend or discovery for a project, pass the project's default language code (from `lrm.json` via the resolver, or the `Project` entity) so the suffix-less file is labeled.

- [ ] **Step 3: Use the merge helper** anywhere cloud projects files into per-language columns; if cloud builds DTOs directly from `LanguageInfo`, call `MergedLanguageColumns.Build(...)` (Core is referenced) and map to the cloud DTO, carrying `HasConflict`.

- [ ] **Step 4: Write a cloud test** asserting a project with a default file + same-culture file yields one column with default-wins value. Match the existing `cloud/tests/LrmCloud.Tests` harness.

- [ ] **Step 5: Run cloud tests**

Run: `dotnet test cloud/tests/LrmCloud.Tests/LrmCloud.Tests.csproj`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add cloud/
git commit -m "fix(cloud): honor DefaultLanguageCode and merge collided columns (issue #6)"
```

---

## Task 12: Bug #1 — cloud↔lrm sync preserves default-language mapping

**Files:**
- Verify: `LocalizationManager.Core/Cloud/` (push/pull/regenerate) — `LocalEntryExtractor.cs`, `FileRegenerator.cs`, `ConfigMerger.cs`, `KeyLevelMerger.cs`.
- Test: `LocalizationManager.Tests/UnitTests/Cloud/`

- [ ] **Step 1: Audit push/pull for code assumptions.** `grep -n "\.Code\|IsDefault\|default" LocalizationManager.Core/Cloud/LocalEntryExtractor.cs LocalizationManager.Core/Cloud/FileRegenerator.cs` — confirm the suffix-less default file is extracted/regenerated under its configured code (`it`), not as `""`/`en`, and that a same-code collision is not pushed as two languages.

- [ ] **Step 2: Write a round-trip test** in `LocalizationManager.Tests/UnitTests/Cloud/`: extract local entries from a project (default file labeled `it` + `.it.resx`), assert the `it` language appears once with default-wins values, and a pull regenerates the suffix-less default file correctly.

- [ ] **Step 3: Run test to verify it fails (if buggy) / passes (if already correct)**

Run: `dotnet test --filter "FullyQualifiedName~Cloud"`
Expected: define behavior; fix `LocalEntryExtractor`/`FileRegenerator` if the default file is mis-mapped.

- [ ] **Step 4: Commit**

```bash
git add LocalizationManager.Core/Cloud/ LocalizationManager.Tests/UnitTests/Cloud/
git commit -m "fix(sync): preserve default-language mapping across push/pull (issue #6)"
```

---

## Task 13: Bug #3 — VS Code Add-Key: group selector + send resourceGroup + surface errors

**Files:**
- Modify: `vscode-extension/src/views/resourceEditor.ts` (modal HTML ~829-847; `openAddKeyModal`/`submitNewKey` ~1325-1351; population of groups)
- Modify: `vscode-extension/src/backend/apiClient.ts` (`addKey` error propagation, if needed)
- Test: `vscode-extension/src/test/unit/addKeyMessage.test.ts` (new)

- [ ] **Step 1: Read the webview message handlers and error display.** Confirm how `command: 'error'` from `handleAddKey` is rendered in the webview (`window.addEventListener('message', ...)`), and how the key list / groups are available client-side (so the `<select>` can be populated). Confirm `apiClient.addKey` throws on non-2xx with the server's `Error` message (the controller returns `BadRequest` with `{ Error: "ResourceGroup is required..." }`). If it swallows errors, fix it to `throw new Error(serverMessage)`.

- [ ] **Step 2: Write the failing unit test (pure payload builder)**

Create `vscode-extension/src/test/unit/addKeyMessage.test.ts`:

```ts
import { buildAddKeyMessage } from '../../views/addKeyMessage';

describe('buildAddKeyMessage', () => {
  it('includes resourceGroup when multiple groups exist', () => {
    const msg = buildAddKeyMessage('MyKey', 'Hello', 'CustomerResources');
    expect(msg).toEqual({
      command: 'addKey',
      key: 'MyKey',
      resourceGroup: 'CustomerResources',
      values: { default: 'Hello' },
    });
  });

  it('omits resourceGroup when not provided (single-group projects)', () => {
    const msg = buildAddKeyMessage('K', 'V', undefined);
    expect(msg).toEqual({ command: 'addKey', key: 'K', values: { default: 'V' } });
  });

  it('throws when key name is empty', () => {
    expect(() => buildAddKeyMessage('  ', 'V', 'G')).toThrow();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd vscode-extension && npx jest addKeyMessage`
Expected: FAIL — module not found.

- [ ] **Step 4: Extract the pure payload builder**

Create `vscode-extension/src/views/addKeyMessage.ts`:

```ts
export interface AddKeyMessage {
  command: 'addKey';
  key: string;
  resourceGroup?: string;
  values: { [language: string]: string };
}

/**
 * Builds the webview→extension Add-Key message. resourceGroup is included only
 * when provided (multi-group projects require it; single-group projects omit it).
 * Throws if the key name is blank so the UI can show a validation message.
 */
export function buildAddKeyMessage(
  keyName: string,
  keyValue: string,
  resourceGroup: string | undefined
): AddKeyMessage {
  const key = (keyName ?? '').trim();
  if (!key) {
    throw new Error('Key name is required');
  }
  const msg: AddKeyMessage = { command: 'addKey', key, values: { default: keyValue ?? '' } };
  if (resourceGroup) {
    msg.resourceGroup = resourceGroup;
  }
  return msg;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd vscode-extension && npx jest addKeyMessage`
Expected: PASS

- [ ] **Step 6: Wire the modal to use it + add the group `<select>`**

In `resourceEditor.ts`:

(a) Add a group selector to the modal HTML (after the key-value form-group, ~line 840), shown only when there are ≥2 groups:

```html
                <div class="form-group" id="newKeyGroupRow" style="display:none;">
                    <label for="newKeyGroup">Resource Group:</label>
                    <select id="newKeyGroup"></select>
                </div>
```

(b) When opening the modal (`openAddKeyModal`), populate `#newKeyGroup` from the known resource groups (the editor already loads keys with `resourceGroup` per row — derive the distinct group list, or fetch `/api/resources/groups` if that endpoint exists; otherwise distinct `ResourceGroup` from loaded rows). Show `#newKeyGroupRow` only when more than one group.

(c) Rewrite `submitNewKey` to use the builder and require a group when multiple exist:

```js
        function submitNewKey() {
            const keyName = document.getElementById('newKeyName').value;
            const keyValue = document.getElementById('newKeyValue').value;
            const groupRow = document.getElementById('newKeyGroupRow');
            const groupSel = document.getElementById('newKeyGroup');
            const multiGroup = groupRow.style.display !== 'none';
            const resourceGroup = multiGroup ? groupSel.value : undefined;

            if (!keyName.trim()) { setStatus('Key name is required', 3000); return; }
            if (multiGroup && !resourceGroup) { setStatus('Select a resource group', 3000); return; }

            vscode.postMessage(buildAddKeyMessageInline(keyName, keyValue, resourceGroup));
            closeAddKeyModal();
        }
```

> The webview script is an inline string, so it can't `import` the TS module directly. Mirror the builder logic inline as `buildAddKeyMessageInline` (kept in sync with the unit-tested `addKeyMessage.ts`), OR bundle the script. Given the existing inline-script pattern, inline mirror is acceptable; the unit test guards the canonical logic.

(d) Ensure the `message` listener renders `command: 'error'` (from `handleAddKey`) visibly (e.g. `setStatus(message, 5000)` and/or `vscode.window.showErrorMessage` on the extension side). If `handleAddKey` already posts `error`, confirm the webview surfaces it; if not, add handling.

- [ ] **Step 7: Build the extension**

Run: `cd vscode-extension && npm run compile` (or `npm run build` — check `package.json` scripts)
Expected: Build succeeded.

- [ ] **Step 8: Run extension unit tests**

Run: `cd vscode-extension && npx jest`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add vscode-extension/src/views/ vscode-extension/src/backend/apiClient.ts vscode-extension/src/test/
git commit -m "fix(vscode): Add-Key group selector, send resourceGroup, surface errors (issue #6)"
```

---

## Task 14: Full verification across all surfaces

- [ ] **Step 1: Full .NET test suite (Core + CLI + web + TUI)**

Run: `dotnet test LocalizationManager.sln`
Expected: PASS (all)

- [ ] **Step 2: Cloud test suite**

Run: `dotnet test cloud/LrmCloud.sln`
Expected: PASS (all)

- [ ] **Step 3: VS Code extension tests**

Run: `cd vscode-extension && npx jest`
Expected: PASS (all)

- [ ] **Step 4: Build everything**

Run: `dotnet build LocalizationManager.sln && dotnet build cloud/LrmCloud.sln && (cd vscode-extension && npm run compile)`
Expected: Build succeeded everywhere.

- [ ] **Step 5: Manual smoke (optional but recommended)** — per the `verify`/`run` skills, launch the web editor against a fixture with `Res.resx` + `Res.it.resx` and `DefaultLanguageCode=it`; confirm a single `it` column with values and a ⚠ conflict marker; confirm Add-Key with a group selector creates the key; run `scan` against a project with nested `namespace ...Resources...` and `@inject IStringLocalizer<T> Q` to confirm no false `missing-key` and that `@Q["..."]` resolves.

---

## Self-Review Notes

- **Spec coverage:** Bug #1 → Tasks 6–12 (Core helper, web factory, web read path, web UI, TUI, cloud, sync). Bug #2 → Tasks 1–2. Bug #3 → Task 13. Feature (@inject) → Tasks 3–5.
- **Type consistency:** `MergedLanguageColumns.Build` / `LanguageColumn` (with `WinningFilePath`, `HasConflict`, `ConflictingFilePaths`) used identically in Tasks 6, 8, 10, 11. `ResourceKeyInfo.HasLanguageConflict` / `ConflictingLanguages` used in Tasks 8 and 9. `buildAddKeyMessage` shape matches the controller's `AddKeyRequest`.
- **Open verification points (resolve while implementing, do not assume):** (a) exact `CodeScanner.Scan` signature and `ScanResult` shape (Task 5/Task 1 tests); (b) `ResourceKeyInfo` file location; (c) whether the web API exposes a groups endpoint for the VS Code selector (Task 13b); (d) how cloud constructs Core discovery (Task 11); (e) TUI test harness name (Task 10).
