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

/// <summary>
/// Verifies that <see cref="ResourcesController.GetAllKeys"/> merges the suffix-less
/// default file and an explicit culture file that share the configured
/// DefaultLanguageCode into a single column (default-wins), and surfaces the
/// collision via <see cref="ResourceKeyInfo.HasLanguageConflict"/>.
/// </summary>
public class DefaultLanguageMergeTests
{
    private const string ResxHeader =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<root>\n" +
        "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
        "  <resheader name=\"version\"><value>2.0</value></resheader>\n" +
        "  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n" +
        "  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n";

    private static string ResxWith(string key, string value) =>
        ResxHeader +
        $"  <data name=\"{key}\"><value>{value}</value></data>\n" +
        "</root>\n";

    private static ResourcesController BuildController(string path, string? defaultLanguageCode)
    {
        IResourceBackend backend = new ResxResourceBackend(defaultLanguageCode);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResourcePath"] = path })
            .Build();
        return new ResourcesController(config, backend);
    }

    private static List<ResourceKeyInfo> GetRows(ResourcesController controller)
    {
        var result = controller.GetAllKeys();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IEnumerable<ResourceKeyInfo>>(ok.Value).ToList();
    }

    [Fact]
    public void GetAllKeys_DefaultLanguageMatchesCultureFile_MergesIntoOneColumnDefaultWins()
    {
        using var sandbox = new TempDir();
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.resx"), ResxWith("Hi", "Ciao"));
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.it.resx"), ResxWith("Hi", "CiaoCulture"));

        var controller = BuildController(sandbox.Path, "it");
        var rows = GetRows(controller);

        var row = rows.Single(r => r.Key == "Hi");

        Assert.True(row.Values.ContainsKey("it"));
        Assert.Equal("Ciao", row.Values["it"]);           // default file wins
        Assert.False(row.Values.ContainsKey("default"));  // no separate (empty) column
        Assert.True(row.HasLanguageConflict);
        Assert.Contains("it", row.ConflictingLanguages);
    }

    [Fact]
    public void GetAllKeys_KeyMissingFromDefault_GapFillsFromCultureFile()
    {
        using var sandbox = new TempDir();
        // Default file lacks "New"; only carries an unrelated key.
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.resx"), ResxWith("Hello", "Ciao"));
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.it.resx"), ResxWith("New", "Nuovo"));

        var controller = BuildController(sandbox.Path, "it");
        var rows = GetRows(controller);

        var row = rows.Single(r => r.Key == "New");

        Assert.True(row.Values.ContainsKey("it"));
        Assert.Equal("Nuovo", row.Values["it"]); // gap-filled from culture file
        Assert.True(row.HasLanguageConflict);
        Assert.Contains("it", row.ConflictingLanguages);
    }

    [Fact]
    public void GetAllKeys_NoDefaultCodeConfigured_DistinctCodesYieldNoConflict()
    {
        using var sandbox = new TempDir();
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.resx"), ResxWith("Hi", "Ciao"));
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.it.resx"), ResxWith("Hi", "CiaoCulture"));

        var controller = BuildController(sandbox.Path, null);
        var rows = GetRows(controller);

        var row = rows.Single(r => r.Key == "Hi");

        Assert.True(row.Values.ContainsKey("default"));
        Assert.True(row.Values.ContainsKey("it"));
        Assert.Equal("Ciao", row.Values["default"]);
        Assert.Equal("CiaoCulture", row.Values["it"]);
        Assert.False(row.HasLanguageConflict);
        Assert.Empty(row.ConflictingLanguages);
    }

    private static List<ResourceFileInfo> GetResources(ResourcesController controller)
    {
        var result = controller.GetResources();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IEnumerable<ResourceFileInfo>>(ok.Value).ToList();
    }

    [Fact]
    public void GetResources_DefaultLabeledSameAsCulture_ReturnsSingleColumn()
    {
        using var sandbox = new TempDir();
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.resx"), ResxWith("Hi", "Ciao"));
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.it.resx"), ResxWith("Hi", "CiaoCulture"));

        var controller = BuildController(sandbox.Path, "it");
        var resources = GetResources(controller);

        // Exactly one "it" column; no duplicate "it" and no separate default/"" column.
        var itColumn = Assert.Single(resources);
        Assert.Equal("it", itColumn.Code);
        Assert.True(itColumn.HasLanguageConflict);
    }

    [Fact]
    public void GetResources_NoDefaultCode_ReturnsDefaultAndCulture()
    {
        using var sandbox = new TempDir();
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.resx"), ResxWith("Hi", "Ciao"));
        File.WriteAllText(Path.Combine(sandbox.Path, "Res.it.resx"), ResxWith("Hi", "CiaoCulture"));

        var controller = BuildController(sandbox.Path, null);
        var resources = GetResources(controller);

        Assert.Equal(2, resources.Count);
        var defaultColumn = Assert.Single(resources, r => r.Code == "default");
        var itColumn = Assert.Single(resources, r => r.Code == "it");
        Assert.False(defaultColumn.HasLanguageConflict);
        Assert.False(itColumn.HasLanguageConflict);
    }

    [Fact]
    public void GetResources_MultiGroupSameCode_NoFalseConflict()
    {
        var multiGroupPath = Path.Combine(AppContext.BaseDirectory, "TestData", "MultiGroupResx");

        // No DefaultLanguageCode configured: CustomerResources.it + GlassResources.it
        // share code "it" across DIFFERENT groups, which is legitimate, not a conflict.
        var controller = BuildController(multiGroupPath, null);
        var resources = GetResources(controller);

        var itColumn = Assert.Single(resources, r => r.Code == "it");
        Assert.False(itColumn.HasLanguageConflict);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
