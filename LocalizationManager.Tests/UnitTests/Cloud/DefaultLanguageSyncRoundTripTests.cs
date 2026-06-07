// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

/// <summary>
/// Regression tests for BUG #1 (issue #6): default-language mapping must be
/// preserved across the cloud sync push/pull round-trip when the project
/// configures an explicit DefaultLanguageCode (e.g. "it") for a suffix-less
/// default file (e.g. Res.resx).
/// </summary>
public class DefaultLanguageSyncRoundTripTests
{
    // Test 1 — Extract labels the default file with the configured code.
    [Fact]
    public async Task LocalEntryExtractor_DefaultFileWithConfiguredCode_PushesUnderThatCode()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            // Default (suffix-less) file + an explicit .it culture file.
            WriteResx(Path.Combine(testDir, "Res.resx"), ("Hi", "Ciao"));
            WriteResx(Path.Combine(testDir, "Res.it.resx"), ("Hi", "CiaoCulture"));

            var discovery = new ResxResourceDiscovery("it");
            var languages = discovery.DiscoverLanguages(testDir);

            var backend = new ResxResourceBackend("it");
            var extractor = new LocalEntryExtractor(backend);
            var entries = await extractor.ExtractEntriesAsync(languages);

            // The default file's value IS pushed under the configured code "it"
            // (not under "").
            Assert.True(
                entries.Any(e => e.Key == "Hi" && e.Lang == "it" && e.Value == "Ciao"),
                "Expected the default file's 'Ciao' to be pushed under Lang=\"it\".");

            // No entry should be pushed under empty lang once a default code is configured.
            Assert.DoesNotContain(entries, e => e.Key == "Hi" && e.Lang == "");

            // NOTABLE FINDING (documented, not a failure): because the suffix-less
            // default file is relabeled to code "it" AND an explicit Res.it.resx also
            // exists, the extract produces TWO entries occupying the same
            // (BaseName, Key, Lang) = ("Res", "Hi", "it") slot with DIFFERING values
            // ("Ciao" from the default file, "CiaoCulture" from the culture file).
            // This collision is asserted here so it is visible; the downstream
            // KeyLevelMerger / cloud dedupes by (BaseName, Key, Lang), so only one of
            // these survives the push (last-writer-wins in the dictionary projection).
            var itHiEntries = entries.Where(e => e.Key == "Hi" && e.Lang == "it").ToList();
            Assert.Equal(2, itHiEntries.Count);
            Assert.Contains(itHiEntries, e => e.Value == "Ciao");
            Assert.Contains(itHiEntries, e => e.Value == "CiaoCulture");
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    // Test 2 — Pull routes an empty-lang cloud entry to the explicit-default file.
    [Fact]
    public async Task FileRegenerator_EmptyLangEntry_RoutedToExplicitDefaultFile()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            WriteResx(Path.Combine(testDir, "Res.resx"), ("Hi", "Old"));

            // Discovery labels the suffix-less default file with Code == "it".
            var languages = new ResxResourceDiscovery("it").DiscoverLanguages(testDir);
            var defaultLang = languages.Single();
            Assert.True(defaultLang.IsDefault);
            Assert.Equal("it", defaultLang.Code);

            // Cloud stored the default under "" (empty lang).
            var merged = new List<MergedEntry>
            {
                new() { Key = "Hi", BaseName = "Res", Lang = "", Value = "Ciao NEW", Hash = "h" }
            };

            var regenerator = new FileRegenerator(new ResxResourceBackend("it"), testDir);
            var result = await regenerator.RegenerateFilesAsync(merged, languages);

            Assert.True(result.Success, result.Error);

            // The suffix-less default file received the value.
            var defaultContent = File.ReadAllText(Path.Combine(testDir, "Res.resx"));
            Assert.Contains("Ciao NEW", defaultContent);

            // No spurious file was created (e.g. Res..resx or Res.it.resx).
            var resxFiles = Directory.GetFiles(testDir, "*.resx");
            Assert.Single(resxFiles);
            Assert.False(File.Exists(Path.Combine(testDir, "Res..resx")));
            Assert.False(File.Exists(Path.Combine(testDir, "Res.it.resx")));
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    // Test 3 — Pull with an explicit code matching the default file writes the default file.
    [Fact]
    public async Task FileRegenerator_ExplicitDefaultCodeEntry_WritesDefaultFile()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            WriteResx(Path.Combine(testDir, "Res.resx"), ("Hi", "Old"));

            var languages = new ResxResourceDiscovery("it").DiscoverLanguages(testDir);

            // Cloud carries the explicit code "it" matching the default file's Code.
            var merged = new List<MergedEntry>
            {
                new() { Key = "Hi", BaseName = "Res", Lang = "it", Value = "Ciao IT", Hash = "h" }
            };

            var regenerator = new FileRegenerator(new ResxResourceBackend("it"), testDir);
            var result = await regenerator.RegenerateFilesAsync(merged, languages);

            Assert.True(result.Success, result.Error);

            // The (BaseName, Code) = ("Res", "it") lookup matches the default file directly.
            var defaultContent = File.ReadAllText(Path.Combine(testDir, "Res.resx"));
            Assert.Contains("Ciao IT", defaultContent);

            var resxFiles = Directory.GetFiles(testDir, "*.resx");
            Assert.Single(resxFiles);
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
