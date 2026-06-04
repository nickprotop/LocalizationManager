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
}
