// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Commands;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Tests for the CLI `lrm add --lang code:value` language resolution (issue #6).
/// The "default" alias must target the suffix-less default file whether its code is
/// blank (no DefaultLanguageCode configured) or a configured code such as "it".
/// Driven from real discovery over a temp resource directory.
/// </summary>
public class AddCommandLanguageResolutionTests : IDisposable
{
    private readonly string _dir;

    public AddCommandLanguageResolutionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"LrmAddLang_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private void WriteResx(string fileName, params (string Key, string Value)[] entries)
    {
        var body = string.Join("\n", entries.Select(e =>
            $"  <data name=\"{e.Key}\"><value>{e.Value}</value></data>"));
        File.WriteAllText(Path.Combine(_dir, fileName),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n" +
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n" +
            body + "\n</root>\n");
    }

    [Fact]
    public void Default_NoConfiguredCode_ResolvesToBlankCodeDefaultFile()
    {
        WriteResx("Res.resx", ("Hi", "Hi"));
        WriteResx("Res.it.resx", ("Hi", "Ciao"));
        var languages = new ResxResourceDiscovery().DiscoverLanguages(_dir);

        var match = AddCommand.ResolveLanguageForCode(languages, "default");

        Assert.NotNull(match);
        Assert.True(match!.IsDefault);
        Assert.Equal("", match.Code);
    }

    [Fact]
    public void Default_ConfiguredItCode_ResolvesToDefaultFileLabeledIt()
    {
        // DefaultLanguageCode = "it": the default file is labeled "it" and there is also
        // an explicit Res.it.resx. "default" must still resolve to the default file.
        WriteResx("Res.resx", ("Hi", "Ciao"));
        WriteResx("Res.it.resx", ("Hi", "CiaoCulture"));
        var languages = new ResxResourceDiscovery("it").DiscoverLanguages(_dir);

        var match = AddCommand.ResolveLanguageForCode(languages, "default");

        Assert.NotNull(match);
        Assert.True(match!.IsDefault, "'default' must map to the default file");
        Assert.EndsWith("Res.resx", match.FilePath);
        Assert.DoesNotContain(".it.resx", match.FilePath);
    }

    [Fact]
    public void ExplicitCultureCode_ResolvesToThatCultureFile()
    {
        WriteResx("Res.resx", ("Hi", "Ciao"));
        WriteResx("Res.el.resx", ("Hi", "Γεια"));
        var languages = new ResxResourceDiscovery("it").DiscoverLanguages(_dir);

        var match = AddCommand.ResolveLanguageForCode(languages, "el");

        Assert.NotNull(match);
        Assert.False(match!.IsDefault);
        Assert.Equal("el", match.Code);
    }

    [Fact]
    public void UnknownCode_ReturnsNull()
    {
        WriteResx("Res.resx", ("Hi", "Ciao"));
        var languages = new ResxResourceDiscovery("it").DiscoverLanguages(_dir);

        Assert.Null(AddCommand.ResolveLanguageForCode(languages, "zz"));
    }
}
