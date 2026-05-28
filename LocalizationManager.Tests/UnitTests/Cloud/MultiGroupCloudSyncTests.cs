// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Cloud;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

/// <summary>
/// Verifies the multi-group sync pipeline preserves BaseName end-to-end so
/// directories with multiple resource groups (e.g. CustomerResources.resx +
/// SharedResources.resx) can sync without colliding on shared key names.
/// </summary>
public class MultiGroupCloudSyncTests
{
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
            Assert.Equal(2, languages.Count);

            var extractor = new LocalEntryExtractor(backend);

            var entries = await extractor.ExtractEntriesAsync(languages);
            Assert.Equal(3, entries.Count);
            Assert.All(entries.Where(e => e.Key == "OK"),
                e => Assert.Contains(e.BaseName, new[] { "CustomerResources", "SharedResources" }));

            // Dictionary now keys by (BaseName, Key, Lang) — both "OK" entries
            // are preserved instead of colliding.
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

    [Fact]
    public async Task KeyLevelMerger_MultiBase_PushChanges_PreservesBaseNamePerAddition()
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

            var merger = new KeyLevelMerger();
            var changes = merger.ComputePushChanges(entries, syncState: null);

            var okAdditions = changes.Additions.Where(a => a.Key == "OK").ToList();
            Assert.Equal(2, okAdditions.Count);
            Assert.Contains(okAdditions, a => a.BaseName == "CustomerResources" && a.Value == "Confirm");
            Assert.Contains(okAdditions, a => a.BaseName == "SharedResources" && a.Value == "OK");
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
        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                  "<root>\n" +
                  "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
                  "  <resheader name=\"version\"><value>2.0</value></resheader>\n" +
                  "  <resheader name=\"reader\"><value>System.Resources.ResXResourceReader</value></resheader>\n" +
                  "  <resheader name=\"writer\"><value>System.Resources.ResXResourceWriter</value></resheader>\n" +
                  data +
                  "</root>\n";
        File.WriteAllText(path, xml);
    }
}
