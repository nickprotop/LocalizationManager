using LocalizationManager.Core.Scanning.Scanners;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Scanning;

public class InjectedLocalizerExtractorTests
{
    [Theory]
    [InlineData("@inject IStringLocalizer<QuoteResources> Q", "Q")]
    [InlineData("@inject IStringLocalizer<SharedResources> Loc", "Loc")]
    [InlineData("@inject IHtmlLocalizer<App> H", "H")]
    [InlineData("@inject  IStringLocalizer<A.B.C>   MyLoc  ", "MyLoc")]
    public void Extract_FindsInjectedLocalizerVariable(string line, string expected)
    {
        var names = InjectedLocalizerExtractor.Extract(line);
        Assert.Contains(expected, names);
    }

    [Fact]
    public void Extract_IgnoresNonLocalizerInjects()
    {
        var names = InjectedLocalizerExtractor.Extract("@inject NavigationManager Nav");
        Assert.Empty(names);
    }

    [Fact]
    public void Extract_FindsMultipleAcrossLines()
    {
        var content = "@inject IStringLocalizer<A> Q\n@inject IStringLocalizer<B> Loc\n<p>hi</p>";
        var names = InjectedLocalizerExtractor.Extract(content);
        Assert.Equal(new[] { "Q", "Loc" }, names);
    }
}
