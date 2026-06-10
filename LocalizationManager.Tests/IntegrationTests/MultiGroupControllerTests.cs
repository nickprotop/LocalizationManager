// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Controllers;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

public class MultiGroupControllerTests
{
    private readonly string _testDataPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

    private LanguageController BuildLanguageController(string? path = null)
    {
        IResourceBackend backend = new ResxResourceBackend();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = path ?? _testDataPath })
            .Build();
        return new LanguageController(config, backend);
    }

    private ResourcesController BuildResourcesController(string? path = null)
    {
        IResourceBackend backend = new ResxResourceBackend();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = path ?? _testDataPath })
            .Build();
        return new ResourcesController(config, backend);
    }

    private StatsController BuildStatsController(string? path = null)
    {
        IResourceBackend backend = new ResxResourceBackend();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = path ?? _testDataPath })
            .Build();
        return new StatsController(config, backend);
    }

    [Fact]
    public void GetStats_MultiBaseDirectory_ReturnsOneEntryPerCulture()
    {
        var controller = BuildStatsController();

        var result = controller.GetStats();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<StatsResponse>(ok.Value);
        Assert.Equal(2, response.Languages.Count); // invariant + it
        Assert.Equal(4, response.TotalKeys); // 2 groups × 2 keys each
        Assert.InRange(response.OverallCoverage, 0, 100);
    }

    [Fact]
    public void GetAllKeys_MultiBaseDirectory_ReturnsOneRowPerKeyPerGroup()
    {
        var controller = BuildResourcesController();

        var result = controller.GetAllKeys();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IEnumerable<ResourceKeyInfo>>(ok.Value).ToList();

        Assert.Equal(4, rows.Count); // CustomerTitle, CustomerEmail, GlassThickness, GlassColor
        Assert.Contains(rows, r => r.Key == "CustomerTitle" && r.ResourceGroup == "CustomerResources");
        Assert.Contains(rows, r => r.Key == "GlassThickness" && r.ResourceGroup == "GlassResources");

        var customerTitle = rows.Single(r => r.Key == "CustomerTitle" && r.ResourceGroup == "CustomerResources");
        Assert.Equal("Cliente", customerTitle.Values["it"]);
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

    [Fact]
    public void GetLanguages_MultiBaseDirectory_CoverageIsBoundedByOneHundred()
    {
        var controller = BuildLanguageController();

        var result = controller.GetLanguages();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LanguagesResponse>(ok.Value);
        foreach (var lang in response.Languages)
        {
            Assert.InRange(lang.Coverage, 0, 100);
            Assert.True(lang.TotalKeys > 0, "TotalKeys should be > 0 for multi-group directory");
        }
    }

    [Fact]
    public void UpdateKey_MultiBaseWithGroupSpecified_WritesOnlyToTargetGroup()
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

        var customerIt = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.it.resx"));
        Assert.Contains("Cliente NEW", customerIt);

        var glassIt = File.ReadAllText(Path.Combine(sandbox.Path, "GlassResources.it.resx"));
        Assert.DoesNotContain("Cliente NEW", glassIt);
    }

    [Fact]
    public void UpdateKey_MultiBaseWithoutGroup_ReturnsBadRequest()
    {
        var controller = BuildResourcesController();

        var request = new UpdateKeyRequest
        {
            Values = new Dictionary<string, ResourceValue>
            {
                ["it"] = new ResourceValue { Value = "x" }
            }
        };

        var result = controller.UpdateKey("CustomerTitle", request);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void GetKey_MultiBaseWithGroupSpecified_ReturnsThatGroupsDetails()
    {
        // The edit modal fetches key details by group; a key name present in two groups
        // must return the requested group's values (issue #6 multi-group edit).
        var controller = BuildResourcesController();

        var result = controller.GetKey("CustomerTitle", resourceGroup: "CustomerResources");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var details = Assert.IsType<ResourceKeyDetails>(ok.Value);
        Assert.Equal("CustomerTitle", details.Key);
    }

    [Fact]
    public void GetKey_MultiBaseWithoutGroup_FallsBackToFirstGroupContainingKey()
    {
        // GetKey (unlike Update/Delete) keeps a "first group containing the key"
        // fallback so existing no-group callers (CodeLens hover, quick-fix) still work.
        // The edit modal avoids the wrong-group load by always passing the group now.
        var controller = BuildResourcesController();

        var result = controller.GetKey("CustomerTitle", resourceGroup: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var details = Assert.IsType<ResourceKeyDetails>(ok.Value);
        Assert.Equal("CustomerTitle", details.Key);
    }

    [Fact]
    public void DeleteKey_MultiBaseWithGroupSpecified_RemovesFromTargetGroupOnlyAndPersists()
    {
        // issue #6: VS Code delete must pass resourceGroup so the right group is hit.
        using var sandbox = TempDirectory.CopyOf(_testDataPath);
        var controller = BuildResourcesController(sandbox.Path);

        var result = controller.DeleteKey("CustomerTitle", occurrence: null, resourceGroup: "CustomerResources");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeleteKeyResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.True(response.DeletedCount > 0);

        // Removed from CustomerResources (default + it), untouched in GlassResources.
        var customerDefault = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.resx"));
        var customerIt = File.ReadAllText(Path.Combine(sandbox.Path, "CustomerResources.it.resx"));
        Assert.DoesNotContain("name=\"CustomerTitle\"", customerDefault);
        Assert.DoesNotContain("name=\"CustomerTitle\"", customerIt);

        var glassIt = File.ReadAllText(Path.Combine(sandbox.Path, "GlassResources.it.resx"));
        Assert.Contains("name=\"GlassThickness\"", glassIt);
    }

    [Fact]
    public void DeleteKey_MultiBaseWithoutGroup_ReturnsBadRequest()
    {
        // This is the root cause of the silent VS Code delete: a multi-group directory
        // with no resourceGroup must 400 rather than guess (issue #6).
        var controller = BuildResourcesController();

        var result = controller.DeleteKey("CustomerTitle", occurrence: null, resourceGroup: null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void DeleteKey_UnknownKey_ReturnsNotFound()
    {
        using var sandbox = TempDirectory.CopyOf(_testDataPath);
        var controller = BuildResourcesController(sandbox.Path);

        var result = controller.DeleteKey("DoesNotExist", occurrence: null, resourceGroup: "CustomerResources");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>
    /// End-to-end scenario mirroring issue #6: a directory with multiple base
    /// resx files (CustomerResources, GlassResources) should report one
    /// language column per culture, not per file; row count = N keys × M
    /// groups; coverage bounded by 100.
    /// </summary>
    [Fact]
    public void Scenario_Issue6_TwoBaseFilesOneNonDefaultCulture_ReportsOneLanguageColumnPerCulture()
    {
        var langController = BuildLanguageController();
        var resourcesController = BuildResourcesController();
        var statsController = BuildStatsController();

        var langs = ((LanguagesResponse)((OkObjectResult)langController.GetLanguages().Result!).Value!).Languages;
        Assert.Equal(2, langs.Count); // invariant + it
        Assert.All(langs, l => Assert.InRange(l.Coverage, 0, 100));
        Assert.All(langs, l => Assert.True(l.TotalKeys > 0));

        var rows = ((IEnumerable<ResourceKeyInfo>)((OkObjectResult)resourcesController.GetAllKeys().Result!).Value!).ToList();
        Assert.Equal(4, rows.Count); // 2 keys per group × 2 groups
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.ResourceGroup), "Every row should carry a ResourceGroup"));
        Assert.All(rows, r => Assert.Contains("it", r.Values.Keys));

        var stats = (StatsResponse)((OkObjectResult)statsController.GetStats().Result!).Value!;
        Assert.Equal(2, stats.Languages.Count);
        Assert.Equal(4, stats.TotalKeys);
        Assert.InRange(stats.OverallCoverage, 0, 100);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }
        private TempDirectory(string path) { Path = path; }
        public static TempDirectory CopyOf(string source)
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
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
