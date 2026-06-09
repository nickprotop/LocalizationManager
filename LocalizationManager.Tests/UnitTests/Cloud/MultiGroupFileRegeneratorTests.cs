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

    /// <summary>
    /// Issue #6 (subfolder groups): pulling a NEW language for a group whose files live
    /// in a subfolder must create the file in THAT subfolder, not at the project root.
    /// </summary>
    [Fact]
    public async Task RegenerateFilesAsync_NewLanguageForSubfolderGroup_CreatedInSubfolder()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            var subDir = Path.Combine(testDir, "Components", "Account", "Pages");
            Directory.CreateDirectory(subDir);
            WriteResx(Path.Combine(subDir, "Login.resx"), ("Login_Title", "Sign in"));

            IResourceDiscovery discovery = new ResxResourceDiscovery();
            IResourceBackend backend = new ResxResourceBackend();
            var languages = discovery.DiscoverLanguages(testDir);

            var regenerator = new FileRegenerator(backend, testDir);
            var merged = new List<MergedEntry>
            {
                new() { Key = "Login_Title", BaseName = "Login", Lang = "fr", Value = "Connexion", Hash = "h1" }
            };

            var result = await regenerator.RegenerateFilesAsync(merged, languages);
            Assert.True(result.Success, result.Error);

            // New French file must be alongside the existing Login.resx in the subfolder.
            var expected = Path.Combine(subDir, "Login.fr.resx");
            Assert.True(File.Exists(expected), $"Expected new file at {expected}");
            Assert.Contains("Connexion", File.ReadAllText(expected));

            // And NOT at the project root.
            Assert.False(File.Exists(Path.Combine(testDir, "Login.fr.resx")),
                "New language file must not be created at the project root");
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// Issue #6 (push side, nested multi-group + default collision): each subfolder group
    /// with a configured default code "it" plus an explicit .it.resx pushes exactly one
    /// "it" entry per group, default-wins, with no cross-group leakage.
    /// </summary>
    [Fact]
    public async Task LocalEntryExtractor_NestedGroupsWithDefaultCollision_PushesOnePerGroup()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            var customersDir = Path.Combine(testDir, "Customers", "Pages");
            var accountDir = Path.Combine(testDir, "Account");
            Directory.CreateDirectory(customersDir);
            Directory.CreateDirectory(accountDir);

            WriteResx(Path.Combine(customersDir, "CustomerResources.resx"), ("Hi", "Ciao"));
            WriteResx(Path.Combine(customersDir, "CustomerResources.it.resx"), ("Hi", "CiaoCultura"));
            WriteResx(Path.Combine(accountDir, "LoginResources.resx"), ("Welcome", "Benvenuto"));
            WriteResx(Path.Combine(accountDir, "LoginResources.it.resx"), ("Welcome", "BenvenutoCultura"));

            var languages = new ResxResourceDiscovery("it").DiscoverLanguages(testDir);
            var extractor = new LocalEntryExtractor(new ResxResourceBackend("it"));
            var entries = await extractor.ExtractEntriesAsync(languages);

            var customerIt = entries.Where(e => e.BaseName == "CustomerResources" && e.Lang == "it").ToList();
            var loginIt = entries.Where(e => e.BaseName == "LoginResources" && e.Lang == "it").ToList();

            Assert.Single(customerIt);
            Assert.Equal("Ciao", customerIt[0].Value);       // default wins
            Assert.Single(loginIt);
            Assert.Equal("Benvenuto", loginIt[0].Value);     // default wins
            Assert.DoesNotContain(entries, e => e.Value == "CiaoCultura" || e.Value == "BenvenutoCultura");
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// Issue #6: pulling a new language for a subfolder group must create the file even
    /// when the target subfolder does not exist locally yet (e.g. a freshly cloned repo
    /// missing the culture file). The regenerator must create the intermediate folders.
    /// </summary>
    [Fact]
    public async Task RegenerateFilesAsync_NewLanguageInNonexistentSubfolder_CreatesDirectories()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            var subDir = Path.Combine(testDir, "Components", "Account", "Pages");
            Directory.CreateDirectory(subDir);
            WriteResx(Path.Combine(subDir, "Login.resx"), ("Login_Title", "Sign in"));

            IResourceDiscovery discovery = new ResxResourceDiscovery();
            IResourceBackend backend = new ResxResourceBackend();
            var languages = discovery.DiscoverLanguages(testDir);

            var regenerator = new FileRegenerator(backend, testDir);
            // Two brand-new languages for the same subfolder group.
            var merged = new List<MergedEntry>
            {
                new() { Key = "Login_Title", BaseName = "Login", Lang = "fr", Value = "Connexion", Hash = "h1" },
                new() { Key = "Login_Title", BaseName = "Login", Lang = "de", Value = "Anmelden",  Hash = "h2" }
            };

            var result = await regenerator.RegenerateFilesAsync(merged, languages);
            Assert.True(result.Success, result.Error);

            var fr = Path.Combine(subDir, "Login.fr.resx");
            var de = Path.Combine(subDir, "Login.de.resx");
            Assert.True(File.Exists(fr), $"Expected {fr}");
            Assert.True(File.Exists(de), $"Expected {de}");
            Assert.Contains("Connexion", File.ReadAllText(fr));
            Assert.Contains("Anmelden", File.ReadAllText(de));

            // Nothing leaked to the project root.
            Assert.False(File.Exists(Path.Combine(testDir, "Login.fr.resx")));
            Assert.False(File.Exists(Path.Combine(testDir, "Login.de.resx")));
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// Issue #6 — full push→pull round-trip over nested multi-group with a default-code
    /// collision. Extract (push) collapses each group's default+culture into one "it"
    /// entry (default wins); regenerating those entries into a fresh checkout that has
    /// only the default files must (a) write the "it" value to the existing .it.resx in
    /// each group's own subfolder and (b) never leak across groups or to the root.
    /// </summary>
    [Fact]
    public async Task PushPullRoundTrip_NestedGroupsWithDefaultCollision_RoutesToCorrectSubfolderFiles()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var targetDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);
        try
        {
            // --- SOURCE (push side): nested groups, each default("it")+culture collision ---
            var srcCustomers = Path.Combine(sourceDir, "Customers", "Pages");
            var srcAccount = Path.Combine(sourceDir, "Account");
            Directory.CreateDirectory(srcCustomers);
            Directory.CreateDirectory(srcAccount);
            WriteResx(Path.Combine(srcCustomers, "CustomerResources.resx"), ("Hi", "Ciao"));
            WriteResx(Path.Combine(srcCustomers, "CustomerResources.it.resx"), ("Hi", "CiaoCultura"));
            WriteResx(Path.Combine(srcAccount, "LoginResources.resx"), ("Welcome", "Benvenuto"));
            WriteResx(Path.Combine(srcAccount, "LoginResources.it.resx"), ("Welcome", "BenvenutoCultura"));

            var srcLanguages = new ResxResourceDiscovery("it").DiscoverLanguages(sourceDir);
            var extractor = new LocalEntryExtractor(new ResxResourceBackend("it"));
            var pushed = await extractor.ExtractEntriesAsync(srcLanguages);

            // Simulate the cloud returning exactly what was pushed (pull side input).
            var merged = pushed.Select(e => new MergedEntry
            {
                Key = e.Key,
                BaseName = e.BaseName,
                Lang = e.Lang,
                Value = e.Value,
                Comment = e.Comment,
                IsPlural = e.IsPlural,
                PluralForms = e.PluralForms,
                Hash = e.Hash
            }).ToList();

            // --- TARGET (pull side): a fresh checkout with only the default files in place ---
            var tgtCustomers = Path.Combine(targetDir, "Customers", "Pages");
            var tgtAccount = Path.Combine(targetDir, "Account");
            Directory.CreateDirectory(tgtCustomers);
            Directory.CreateDirectory(tgtAccount);
            WriteResx(Path.Combine(tgtCustomers, "CustomerResources.resx"), ("Hi", "Ciao"));
            WriteResx(Path.Combine(tgtCustomers, "CustomerResources.it.resx"), ("Hi", ""));
            WriteResx(Path.Combine(tgtAccount, "LoginResources.resx"), ("Welcome", "Benvenuto"));
            WriteResx(Path.Combine(tgtAccount, "LoginResources.it.resx"), ("Welcome", ""));

            var tgtLanguages = new ResxResourceDiscovery("it").DiscoverLanguages(targetDir);
            var regenerator = new FileRegenerator(new ResxResourceBackend("it"), targetDir);

            var result = await regenerator.RegenerateFilesAsync(merged, tgtLanguages);
            Assert.True(result.Success, result.Error);

            // The default-wins "it" value lands in each group's culture file, in its subfolder.
            var customerIt = File.ReadAllText(Path.Combine(tgtCustomers, "CustomerResources.it.resx"));
            var loginIt = File.ReadAllText(Path.Combine(tgtAccount, "LoginResources.it.resx"));
            Assert.Contains("Ciao", customerIt);
            Assert.Contains("Benvenuto", loginIt);

            // No cross-group leakage and no stray files at the project root.
            Assert.DoesNotContain("Benvenuto", customerIt);
            Assert.DoesNotContain("Ciao", loginIt);
            Assert.False(File.Exists(Path.Combine(targetDir, "CustomerResources.it.resx")));
            Assert.False(File.Exists(Path.Combine(targetDir, "LoginResources.it.resx")));
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
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
