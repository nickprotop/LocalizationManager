// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using LocalizationManager.Core;
using LocalizationManager.Core.Enums;
using LocalizationManager.Core.Output;
using LocalizationManager.Core.Scanning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands;

/// <summary>
/// Combined command to run both validate and scan
/// </summary>
public class CheckCommand : Command<CheckCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandOption("--source-path <PATH>")]
        [Description("Path to source code directory to scan. Defaults to parent directory of resource path.")]
        public string? SourcePath { get; set; }

        [CommandOption("--exclude <PATTERNS>")]
        [Description("Glob patterns to exclude from scan (comma-separated)")]
        public string? ExcludePatterns { get; set; }

        [CommandOption("--strict")]
        [Description("Strict mode for scanning - only high-confidence references")]
        public bool StrictMode { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration
        settings.LoadConfiguration();

        var format = settings.GetOutputFormat();
        var isTableFormat = format == OutputFormat.Table;

        if (isTableFormat)
        {
            AnsiConsole.MarkupLine("[bold blue]Running Complete Check (Validate + Scan)[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]═══ Step 1: Validation ═══[/]");
            AnsiConsole.WriteLine();
        }

        // Step 1: Run validation
        var validateCommand = new ValidateCommand();
        var validateSettings = new ValidateCommandSettings
        {
            ResourcePath = settings.ResourcePath,
            ConfigFilePath = settings.ConfigFilePath,
            Format = settings.Format
        };

        var validateResult = validateCommand.Execute(context, validateSettings, cancellationToken);

        if (validateResult != 0)
        {
            if (isTableFormat)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[red]✗ Validation failed - stopping check[/]");
            }
            return validateResult;
        }

        // Step 2: Run scan
        if (isTableFormat)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]═══ Step 2: Code Scanning ═══[/]");
            AnsiConsole.WriteLine();
        }

        var scanCommand = new ScanCommand();
        var scanSettings = new ScanCommand.Settings
        {
            ResourcePath = settings.ResourcePath,
            ConfigFilePath = settings.ConfigFilePath,
            Format = settings.Format,
            SourcePath = settings.SourcePath,
            ExcludePatterns = settings.ExcludePatterns,
            StrictMode = settings.StrictMode
        };

        var scanResult = scanCommand.Execute(context, scanSettings, cancellationToken);

        // Final summary
        if (isTableFormat)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]═══ Summary ═══[/]");
            AnsiConsole.WriteLine();

            if (validateResult == 0 && scanResult == 0)
            {
                AnsiConsole.MarkupLine("[green]✓ All checks passed![/]");
                AnsiConsole.MarkupLine("  • Resource files are valid");
                AnsiConsole.MarkupLine("  • No missing or unused keys");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Issues found:[/]");
                if (validateResult != 0)
                    AnsiConsole.MarkupLine("  • [red]Resource validation failed[/]");
                if (scanResult != 0)
                    AnsiConsole.MarkupLine("  • [yellow]Code scanning found issues[/]");
            }
        }

        // Return non-zero if either check failed
        return validateResult != 0 || scanResult != 0 ? 1 : 0;
    }
}
