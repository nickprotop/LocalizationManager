// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests;

public class MergedLanguageColumnsTests
{
    private static LanguageInfo F(string code, bool isDefault, string path) =>
        new() { BaseName = "Res", Code = code, Name = code, IsDefault = isDefault, FilePath = path };

    [Fact]
    public void Merge_DefaultLabeledSameAsCulture_CollapsesToOneColumn_DefaultWins()
    {
        var files = new[] { F("it", true, "Res.resx"), F("it", false, "Res.it.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Single(cols);
        Assert.Equal("it", cols[0].Code);
        Assert.Equal("Res.resx", cols[0].WinningFilePath);
        Assert.True(cols[0].HasConflict);
        Assert.Contains("Res.it.resx", cols[0].ConflictingFilePaths);
    }

    [Fact]
    public void Merge_DistinctCodes_KeepsSeparateColumns_NoConflict()
    {
        var files = new[] { F("it", true, "Res.resx"), F("en", false, "Res.en.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Equal(2, cols.Count);
        Assert.All(cols, c => Assert.False(c.HasConflict));
    }

    [Fact]
    public void Merge_EmptyDefaultCode_UsesDefaultBucket()
    {
        var files = new[] { F("", true, "Res.resx"), F("fr", false, "Res.fr.resx") };
        var cols = MergedLanguageColumns.Build(files);

        Assert.Equal(2, cols.Count);
        Assert.Contains(cols, c => c.Code == "default");
        Assert.Contains(cols, c => c.Code == "fr");
    }

    [Fact]
    public void Merge_DefaultColumnSortsFirst()
    {
        var files = new[] { F("zz", false, "Res.zz.resx"), F("it", true, "Res.resx"), F("ar", false, "Res.ar.resx") };
        var cols = MergedLanguageColumns.Build(files);
        Assert.True(cols[0].IsDefault);
        Assert.Equal("it", cols[0].Code);
    }

    [Fact]
    public void Merge_EffectiveCode_BlankIsDefault()
    {
        Assert.Equal("default", MergedLanguageColumns.EffectiveCode(F("", true, "x")));
        Assert.Equal("it", MergedLanguageColumns.EffectiveCode(F("it", false, "x")));
    }
}
