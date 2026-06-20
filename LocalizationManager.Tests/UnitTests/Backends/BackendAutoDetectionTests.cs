// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Json;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backends;

/// <summary>
/// Regression tests for format auto-detection (<see cref="ResourceBackendFactory.ResolveFromPath(string)"/>).
/// </summary>
public class BackendAutoDetectionTests : IDisposable
{
    private readonly string _tempDirectory;

    public BackendAutoDetectionTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"BackendDetect_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    /// <summary>
    /// A resx-only directory that also contains lrm's own backup metadata
    /// (.lrm/backups/.../manifest.json) must still auto-detect as RESX. Before the
    /// fix, the JSON backend's recursive CanHandle matched those manifest files and
    /// won the priority race, so detection wrongly reported "No JSON resource files".
    /// </summary>
    [Fact]
    public void ResolveFromPath_ResxWithLrmJsonBackups_DetectsResx()
    {
        // Arrange: a normal resx project
        File.WriteAllText(Path.Combine(_tempDirectory, "SharedResources.resx"), MinimalResx());
        File.WriteAllText(Path.Combine(_tempDirectory, "SharedResources.el.resx"), MinimalResx());

        // ...with lrm's own backup metadata living under .lrm/
        var backupDir = Path.Combine(_tempDirectory, ".lrm", "backups", "SharedResources.resx");
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "manifest.json"), "{\"version\":1}");
        File.WriteAllText(Path.Combine(backupDir, "v1_2026-01-01.resx"), MinimalResx());

        // Act
        var backend = new ResourceBackendFactory().ResolveFromPath(_tempDirectory);

        // Assert
        Assert.Equal("resx", backend.Name);
    }

    /// <summary>
    /// CanHandle must ignore .json files that live only under excluded folders.
    /// </summary>
    [Fact]
    public void JsonCanHandle_OnlyJsonIsUnderLrmFolder_ReturnsFalse()
    {
        var backupDir = Path.Combine(_tempDirectory, ".lrm", "backups");
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "manifest.json"), "{}");

        var canHandle = new JsonResourceBackend().CanHandle(_tempDirectory);

        Assert.False(canHandle);
    }

    /// <summary>
    /// A genuine JSON resource directory is still detected as a JSON-family backend
    /// (no regression). en.json/el.json is the i18next convention, so the factory
    /// resolves it to the i18next variant — the point is that resx does NOT win here.
    /// </summary>
    [Fact]
    public void ResolveFromPath_RealJsonResources_DetectsJsonFamily()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "en.json"), "{\"Save\":\"Save\"}");
        File.WriteAllText(Path.Combine(_tempDirectory, "el.json"), "{\"Save\":\"Αποθήκευση\"}");

        var backend = new ResourceBackendFactory().ResolveFromPath(_tempDirectory);

        Assert.Contains(backend.Name, new[] { "json", "i18next" });
    }

    private static string MinimalResx() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n" +
        "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
        "  <resheader name=\"version\"><value>2.0</value></resheader>\n" +
        "  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n" +
        "  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n" +
        "  <data name=\"Save\" xml:space=\"preserve\"><value>Save</value></data>\n</root>\n";
}
