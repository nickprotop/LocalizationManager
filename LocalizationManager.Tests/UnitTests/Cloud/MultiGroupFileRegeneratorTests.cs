// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class MultiGroupFileRegeneratorTests
{
    [Fact]
    public async Task RegenerateFilesAsync_RoutesKeyToCorrectGroupFile()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            WriteResx(Path.Combine(testDir, "CustomerResources.resx"), ("OK", "Confirm"));
            WriteResx(Path.Combine(testDir, "SharedResources.resx"), ("OK", "OK"));

            IResourceDiscovery discovery = new ResxResourceDiscovery();
            IResourceBackend backend = new ResxResourceBackend();
            var languages = discovery.DiscoverLanguages(testDir);

            var regenerator = new FileRegenerator(backend, testDir);
            var merged = new List<MergedEntry>
            {
                new() { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm NEW", Hash = "h1" },
                new() { Key = "OK", BaseName = "SharedResources",   Lang = "", Value = "OK NEW",      Hash = "h2" }
            };

            var result = await regenerator.RegenerateFilesAsync(merged, languages);
            Assert.True(result.Success, result.Error);

            var customer = File.ReadAllText(Path.Combine(testDir, "CustomerResources.resx"));
            var shared   = File.ReadAllText(Path.Combine(testDir, "SharedResources.resx"));
            Assert.Contains("Confirm NEW", customer);
            Assert.DoesNotContain("OK NEW", customer);
            Assert.Contains("OK NEW", shared);
            Assert.DoesNotContain("Confirm NEW", shared);
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
