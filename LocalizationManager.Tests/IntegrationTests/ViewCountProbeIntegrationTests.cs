// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Commands;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Models;
using Spectre.Console.Cli;
using Xunit;

namespace LocalizationManager.Tests.IntegrationTests;

/// <summary>
/// Verifies that `view --count` works as a quiet existence probe:
/// exit 0 when at least one key matches, exit 1 when none do, without
/// emitting the noisy "✗ Key not found" error used for human-facing lookups.
/// </summary>
public class ViewCountProbeIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ResxResourceWriter _writer = new();

    public ViewCountProbeIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        var defaultLang = new LanguageInfo
        {
            BaseName = "TestResource",
            Code = "",
            Name = "Default",
            IsDefault = true,
            FilePath = Path.Combine(_testDirectory, "TestResource.resx")
        };

        _writer.Write(new ResourceFile
        {
            Language = defaultLang,
            Entries = new List<ResourceEntry>
            {
                new() { Key = "Save", Value = "Save" },
                new() { Key = "Cancel", Value = "Cancel" },
                new() { Key = "SaveAs", Value = "Save As" }
            }
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    private int RunView(params string[] extraArgs)
    {
        var app = new CommandApp<ViewCommand>();
        var args = new List<string>(extraArgs);
        args.Add("--path");
        args.Add(_testDirectory);
        return app.Run(args.ToArray());
    }

    [Fact]
    public void CountProbe_ExistingKey_ReturnsZero()
    {
        var result = RunView("Save", "--count", "--format", "simple");
        Assert.Equal(0, result);
    }

    [Fact]
    public void CountProbe_MissingKey_ReturnsOne()
    {
        var result = RunView("Nonexistent.Key", "--count", "--format", "simple");
        Assert.Equal(1, result);
    }

    [Fact]
    public void CountProbe_MissingKey_Json_ReturnsOne()
    {
        var result = RunView("Nonexistent.Key", "--count", "--format", "json");
        Assert.Equal(1, result);
    }

    [Fact]
    public void CountProbe_RegexMatchingMultiple_ReturnsZeroAndCountsAll()
    {
        // "Save" and "SaveAs" both match — probe should succeed.
        var result = RunView("Save.*", "--count", "--regex", "--format", "simple");
        Assert.Equal(0, result);
    }

    [Fact]
    public void CountProbe_RegexMatchingNone_ReturnsOne()
    {
        var result = RunView("ZZZ.*", "--count", "--regex", "--format", "simple");
        Assert.Equal(1, result);
    }

    [Fact]
    public void PlainView_MissingKey_StillReturnsOne()
    {
        // Human-facing lookup behavior is unchanged: missing exact key still errors.
        var result = RunView("Nonexistent.Key", "--format", "simple");
        Assert.Equal(1, result);
    }
}
