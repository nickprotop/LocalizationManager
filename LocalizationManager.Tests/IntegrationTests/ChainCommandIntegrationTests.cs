// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Spectre.Console.Cli;
using Xunit;
using LocalizationManager.Commands;

namespace LocalizationManager.Tests.IntegrationTests;

public class ChainCommandIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public ChainCommandIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void ChainCommand_DryRun_DisplaysCommandsWithoutExecuting()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<ChainCommand>("chain");
        });

        // Act
        var result = app.Run(new[] { "chain", "validate -- translate --only-missing", "--dry-run" });

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ChainCommand_EmptyChain_ReturnsError()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<ChainCommand>("chain");
        });

        // Act
        var result = app.Run(new[] { "chain", "" });

        // Assert
        Assert.Equal(1, result); // Empty chain should return error code 1
    }

    [Fact]
    public void ChainCommand_ValidSingleCommand_DryRun_Succeeds()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<ChainCommand>("chain");
        });

        // Act
        var result = app.Run(new[] { "chain", "validate", "--dry-run" });

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ChainCommand_MultipleCommands_DryRun_Succeeds()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<ChainCommand>("chain");
        });

        // Act
        var result = app.Run(new[] { "chain", "validate -- scan -- export", "--dry-run" });

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ChainCommand_WithQuotedArguments_DryRun_Succeeds()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<ChainCommand>("chain");
        });

        // Act
        var result = app.Run(new[] { "chain", "add \"New Key\" --lang \"default:Value\" -- validate", "--dry-run" });

        // Assert
        Assert.Equal(0, result);
    }
}
