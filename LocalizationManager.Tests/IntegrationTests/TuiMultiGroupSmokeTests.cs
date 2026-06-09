// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using LocalizationManager.UI;
using Terminal.Gui;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Smoke tests for the TUI editor against multi-base resource group directories.
/// Before multi-group support, constructing the editor with two groups that share a
/// culture code crashed (duplicate DataTable column names). These tests build a real
/// multi-group directory and confirm the window constructs without throwing.
/// </summary>
[Collection("Tui")]
public class TuiMultiGroupSmokeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ResxResourceDiscovery _discovery = new("it");
    private readonly ResxResourceReader _reader = new();

    public TuiMultiGroupSmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TuiMultiGroup_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Application.Shutdown(); } catch { /* not initialized */ }
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private void WriteResx(string fileName, params (string Key, string Value)[] entries)
    {
        var body = string.Join("\n", entries.Select(e =>
            $"  <data name=\"{e.Key}\" xml:space=\"preserve\"><value>{e.Value}</value></data>"));
        var content =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n" +
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
            "  <resheader name=\"version\"><value>2.0</value></resheader>\n" +
            "  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n" +
            "  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n" +
            body + "\n</root>\n";
        File.WriteAllText(Path.Combine(_tempDir, fileName), content);
    }

    private List<ResourceFile> DiscoverAndRead()
    {
        var languages = _discovery.DiscoverLanguages(_tempDir);
        return languages.Select(l => _reader.Read(l)).ToList();
    }

    [Fact]
    public void Constructor_MultipleGroupsSharingCulture_DoesNotThrow()
    {
        // Two groups, each with a default + Italian file → same culture code "it" appears twice.
        WriteResx("CustomerResources.resx", ("Customer_Name", "Name"));
        WriteResx("CustomerResources.it.resx", ("Customer_Name", "Nome"));
        WriteResx("GlassResources.resx", ("Glass_Width", "Width"));
        WriteResx("GlassResources.it.resx", ("Glass_Width", "Larghezza"));

        var resourceFiles = DiscoverAndRead();
        var backend = new ResxResourceBackend("it");

        Application.Init(new FakeDriver());
        try
        {
            // The window construction is what used to crash with a DuplicateNameException.
            var window = new ResourceEditorWindow(resourceFiles, backend, "it", _tempDir);
            Assert.NotNull(window);
        }
        finally
        {
            Application.Shutdown();
        }
    }

    [Fact]
    public void Constructor_SingleGroup_DoesNotThrow()
    {
        WriteResx("Resources.resx", ("Hello", "Hello"));
        WriteResx("Resources.it.resx", ("Hello", "Ciao"));

        var resourceFiles = DiscoverAndRead();
        var backend = new ResxResourceBackend("it");

        Application.Init(new FakeDriver());
        try
        {
            var window = new ResourceEditorWindow(resourceFiles, backend, "it", _tempDir);
            Assert.NotNull(window);
        }
        finally
        {
            Application.Shutdown();
        }
    }

    /// <summary>
    /// BUG #1 (issue #6): with DefaultLanguageCode=it, the suffix-less default file
    /// (Res.resx) and an explicit Res.it.resx both carry effective code "it" within one
    /// group. The "it" column must show the DEFAULT file's value, not the culture file's.
    /// </summary>
    [Fact]
    public void GetEntryForCell_DefaultAndCultureShareCode_DefaultWins()
    {
        WriteResx("Res.resx", ("Hi", "Ciao"));
        WriteResx("Res.it.resx", ("Hi", "CiaoCulture"));

        var resourceFiles = DiscoverAndRead();
        var backend = new ResxResourceBackend("it");

        Application.Init(new FakeDriver());
        try
        {
            var window = new ResourceEditorWindow(resourceFiles, backend, "it", _tempDir);

            var entry = InvokeGetEntryForCell(window, "Res", "Hi", 1, "it");

            Assert.NotNull(entry);
            // Default file (Res.resx) must win over the colliding culture file (Res.it.resx).
            Assert.Equal("Ciao", entry!.Value);
        }
        finally
        {
            Application.Shutdown();
        }
    }

    /// <summary>
    /// Gap-fill: when the winning (default) file has no value for a key but a colliding
    /// culture file does, the culture value is surfaced so the cell is not blank.
    /// </summary>
    [Fact]
    public void GetEntryForCell_DefaultMissingKey_GapFillsFromCultureFile()
    {
        // Default has only "Hi"; the colliding culture file additionally has "Only".
        WriteResx("Res.resx", ("Hi", "Ciao"));
        WriteResx("Res.it.resx", ("Hi", "CiaoCulture"), ("Only", "SoloIt"));

        var resourceFiles = DiscoverAndRead();
        var backend = new ResxResourceBackend("it");

        Application.Init(new FakeDriver());
        try
        {
            var window = new ResourceEditorWindow(resourceFiles, backend, "it", _tempDir);

            var entry = InvokeGetEntryForCell(window, "Res", "Only", 1, "it");

            Assert.NotNull(entry);
            Assert.Equal("SoloIt", entry!.Value);
        }
        finally
        {
            Application.Shutdown();
        }
    }

    /// <summary>
    /// BUG (issue #6, write side): adding a key in a group where the default file and an
    /// explicit culture file share the effective code "it" must route the value to the
    /// DEFAULT file (the merge winner), not to whichever same-code file is enumerated
    /// first. Pins the AddNewKey routing helper directly.
    /// </summary>
    [Fact]
    public void TargetFileForColumn_DefaultAndCultureShareCode_ResolvesToDefaultFile()
    {
        WriteResx("Res.resx", ("Hi", "Ciao"));
        WriteResx("Res.it.resx", ("Hi", "CiaoCulture"));

        var groupFiles = DiscoverAndRead();
        var columns = MergedLanguageColumns.Build(groupFiles.Select(f => f.Language));

        // The dialog column code for the merged default column is the raw file code "it".
        var target = InvokeTargetFileForColumn(groupFiles, columns, "it");

        Assert.NotNull(target);
        Assert.True(target!.Language.IsDefault, "value must route to the default file");
        Assert.EndsWith("Res.resx", target.Language.FilePath);
        Assert.DoesNotContain(".it.resx", target.Language.FilePath);
    }

    [Fact]
    public void TargetFileForColumn_BlankColumnCode_ResolvesToDefaultFile()
    {
        // No DefaultLanguageCode configured: default file code is "" → column code "".
        var discovery = new ResxResourceDiscovery();
        WriteResx("Res.resx", ("Hi", "Hi"));
        WriteResx("Res.it.resx", ("Hi", "Ciao"));
        var groupFiles = discovery.DiscoverLanguages(_tempDir).Select(l => _reader.Read(l)).ToList();
        var columns = MergedLanguageColumns.Build(groupFiles.Select(f => f.Language));

        var target = InvokeTargetFileForColumn(groupFiles, columns, "");

        Assert.NotNull(target);
        Assert.True(target!.Language.IsDefault);
        Assert.EndsWith("Res.resx", target.Language.FilePath);
    }

    private static ResourceEntry? InvokeGetEntryForCell(
        ResourceEditorWindow window, string baseName, string key, int occurrenceNumber, string code)
    {
        var method = typeof(ResourceEditorWindow).GetMethod(
            "GetEntryForCell",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        return (ResourceEntry?)method!.Invoke(window, new object[] { baseName, key, occurrenceNumber, code });
    }

    private static ResourceFile? InvokeTargetFileForColumn(
        List<ResourceFile> groupFiles, IReadOnlyList<LanguageColumn> columns, string columnCode)
    {
        var method = typeof(ResourceEditorWindow).GetMethod(
            "TargetFileForColumn",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (ResourceFile?)method!.Invoke(null, new object[] { groupFiles, columns, columnCode });
    }
}
