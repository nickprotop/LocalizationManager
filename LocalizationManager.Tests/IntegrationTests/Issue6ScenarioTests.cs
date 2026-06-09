// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Controllers;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning;
using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// End-to-end tests built from a realistic fixture project mirroring the reporter's
/// setup in issue #6 (Vitrum/Glass quote manager): multiple base .resx groups in one
/// directory, a default file labeled with the configured DefaultLanguageCode that
/// collides with an explicit .it.resx, a resource file in a deep subfolder, a
/// nested-namespace C# class, and a Razor page that uses @inject'd localizer
/// variables (Q, G) plus a folder-wide one from _Imports.razor (Loc).
///
/// These pin the "uncommon" combinations that broke across v0.7.12–v0.7.14 so they
/// can't silently regress.
/// </summary>
public class Issue6ScenarioTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Issue6Scenario");
    private static readonly string ResourcesPath = Path.Combine(FixtureRoot, "Resources");
    private static readonly string SourcePath = Path.Combine(FixtureRoot, "Source");

    // ---- Discovery: subfolders + multi-group + default-code collision -------------

    [Fact]
    public void Discovery_FindsResourcesInSubfolders_AndAllGroups()
    {
        var backend = new ResxResourceBackend("it");
        var languages = backend.Discovery.DiscoverLanguages(ResourcesPath);

        var baseNames = languages.Select(l => l.BaseName).Distinct().ToList();
        Assert.Contains("CustomerResources", baseNames);
        Assert.Contains("SharedResources", baseNames);
        Assert.Contains("GlassResources", baseNames);
        // Issue #6 Bug 4: a resource file in a deep subfolder must be discovered.
        Assert.Contains("Login", baseNames);
    }

    [Fact]
    public void GetAllKeys_DefaultCodeMatchesCultureFile_CollapsesToSingleColumnWithConflict()
    {
        var controller = BuildResourcesController("it");

        var rows = OkValue<IEnumerable<ResourceKeyInfo>>(controller.GetAllKeys()).ToList();

        var businessName = rows.Single(r =>
            r.Key == "Customer_BusinessName_Label" && r.ResourceGroup == "CustomerResources");

        // CustomerResources.resx (Code="it") and CustomerResources.it.resx collapse
        // into a single "it" column — not two columns, and not an "(empty)" default.
        Assert.Contains("it", businessName.Values.Keys);
        Assert.DoesNotContain("default", businessName.Values.Keys);
        Assert.DoesNotContain("", businessName.Values.Keys);

        // Default file wins the collision.
        Assert.Equal("Ragione sociale", businessName.Values["it"]);
        Assert.True(businessName.HasLanguageConflict);
        Assert.Contains("it", businessName.ConflictingLanguages);
    }

    // ---- Scanner: nested namespace + @inject localizers --------------------------

    [Fact]
    public void Scan_NestedNamespace_DoesNotProducePhantomKeys()
    {
        var (result, _) = ScanFile(Path.Combine(
            SourcePath, "Components", "Account", "Pages", "Login.razor.cs"));

        // Bug 2: namespace segments must never become keys/missing keys.
        foreach (var phantom in new[] { "Components", "Account", "Pages", "Resources", "Vitrum" })
        {
            Assert.DoesNotContain(result.MissingKeys, k => k.Key == phantom);
            Assert.DoesNotContain(result.AllKeyUsages, u => u.Key == phantom);
        }

        // The real usage in the same file is still detected and resolves.
        Assert.Contains(result.AllKeyUsages, u => u.Key == "Login_Title");
        Assert.DoesNotContain(result.MissingKeys, k => k.Key == "Login_Title");
    }

    [Fact]
    public void Scan_InjectedLocalizers_ResolveKeysAcrossGroups()
    {
        var (result, _) = ScanFile(Path.Combine(
            SourcePath, "Components", "Account", "Pages", "Login.razor"));

        // Q (per-file, CustomerResources), G (per-file, GlassResources) and
        // Loc (folder-wide via _Imports.razor, SharedResources) are all recognized,
        // and their keys resolve against the unioned multi-group key set.
        foreach (var present in new[]
                 {
                     "Customer_BusinessName_Label",
                     "Customer_Email_Label",
                     "Glass_Thickness_Label",
                     "Shared_Save_Button",
                     "Shared_Cancel_Button"
                 })
        {
            Assert.Contains(result.AllKeyUsages, u => u.Key == present);
            Assert.DoesNotContain(result.MissingKeys, k => k.Key == present);
        }

        // The genuinely undefined key is still reported missing.
        Assert.Contains(result.MissingKeys, k => k.Key == "Customer_DoesNotExist_Label");
    }

    // ---- AddKey: per-language values in a multi-group project ---------------------

    [Fact]
    public void AddKey_MultiGroupPerLanguageValues_WritesEachValueToItsFile()
    {
        using var sandbox = TempDirectory.CopyOf(ResourcesPath);
        var controller = BuildResourcesController("it", sandbox.Path);

        // The webview now sends one value per language column. "default" must land in
        // the suffix-less default file; "it" in the explicit culture file.
        var request = new AddKeyRequest
        {
            Key = "Customer_Phone_Label",
            ResourceGroup = "CustomerResources",
            Values = new Dictionary<string, string>
            {
                ["default"] = "Telefono",
                ["it"] = "Telefono (it)"
            }
        };

        Assert.IsType<OkObjectResult>(controller.AddKey(request).Result);

        var defaultText = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.resx"));
        var cultureText = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.it.resx"));
        Assert.Contains("Telefono", defaultText);
        Assert.Contains("Telefono (it)", cultureText);

        // It must not leak into another group.
        var glassText = File.ReadAllText(Path.Combine(sandbox.Path, "GlassResources.resx"));
        Assert.DoesNotContain("Telefono", glassText);
    }

    [Fact]
    public void AddKey_MultiGroupWithoutGroup_ReturnsBadRequest()
    {
        var controller = BuildResourcesController("it");
        var request = new AddKeyRequest
        {
            Key = "Orphan_Key",
            Values = new Dictionary<string, string> { ["default"] = "x" }
        };

        Assert.IsType<BadRequestObjectResult>(controller.AddKey(request).Result);
    }

    // ---- helpers -----------------------------------------------------------------

    private static (ScanResult result, List<ResourceFile> files) ScanFile(string filePath)
    {
        var backend = new ResxResourceBackend("it");
        var files = backend.Discovery.DiscoverLanguages(ResourcesPath)
            .Select(l => backend.Reader.Read(l))
            .ToList();
        var scanner = new CodeScanner();
        var result = scanner.ScanSingleFile(filePath, files);
        return (result, files);
    }

    private static ResourcesController BuildResourcesController(string? defaultCode, string? path = null)
    {
        IResourceBackend backend = new ResxResourceBackend(defaultCode);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ResourcePath"] = path ?? ResourcesPath,
                ["DefaultLanguageCode"] = defaultCode
            })
            .Build();
        return new ResourcesController(config, backend);
    }

    private static T OkValue<T>(ActionResult<IEnumerable<ResourceKeyInfo>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }
        private TempDirectory(string path) { Path = path; }

        public static TempDirectory CopyOf(string source)
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            // Copy only the top-level files of the group directory (enough for AddKey).
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, System.IO.Path.Combine(dir, System.IO.Path.GetFileName(file)));
            }
            return new TempDirectory(dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
