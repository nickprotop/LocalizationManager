// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using LocalizationManager.Commands.Backup;
using LocalizationManager.Commands.Config;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the chain command
/// </summary>
public class ChainCommandSettings : CommandSettings
{
    [CommandArgument(0, "<COMMANDS>")]
    [Description("Commands to execute, separated by ' -- ' (space-dash-dash-space)")]
    public string CommandString { get; set; } = string.Empty;

    [CommandOption("--continue-on-error")]
    [Description("Continue executing commands even if one fails (default: stop on first error)")]
    [DefaultValue(false)]
    public bool ContinueOnError { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what commands would be executed without running them")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }
}

/// <summary>
/// Command to execute multiple LRM commands sequentially in a single invocation
/// </summary>
public class ChainCommand : Command<ChainCommandSettings>
{
    public override int Execute(CommandContext context, ChainCommandSettings settings, CancellationToken cancellationToken = default)
    {
        // Validate and parse command chain
        var (isValid, errorMessage) = ChainCommandParser.ValidateChain(settings.CommandString);
        if (!isValid)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {errorMessage}");
            return 1;
        }

        var commands = ChainCommandParser.ParseChain(settings.CommandString);

        if (commands.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No commands provided in chain");
            AnsiConsole.MarkupLine("[yellow]Usage:[/] lrm chain <COMMAND1> -- <COMMAND2> -- <COMMAND3>");
            AnsiConsole.MarkupLine("[yellow]Example:[/] lrm chain validate --format json -- translate --only-missing -- export -o output.csv");
            return 1;
        }

        // Dry run mode - just display execution plan
        if (settings.DryRun)
        {
            DisplayExecutionPlan(commands);
            return 0;
        }

        // Create execution context
        var ctx = new ChainExecutionContext
        {
            TotalSteps = commands.Count,
            StopOnError = !settings.ContinueOnError,
            Results = new List<ChainCommandResult>()
        };

        // Execute command chain with progress display
        return ExecuteCommandChain(commands, ctx);
    }

    /// <summary>
    /// Display execution plan without executing (dry-run mode)
    /// </summary>
    private void DisplayExecutionPlan(List<string[]> commands)
    {
        AnsiConsole.MarkupLine("[yellow]Dry run mode - commands that would be executed:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("[cyan]Step[/]");
        table.AddColumn("[cyan]Command[/]");

        for (int i = 0; i < commands.Count; i++)
        {
            var commandStr = string.Join(" ", commands[i]);
            table.AddRow(
                $"[blue]{i + 1}[/]",
                commandStr.EscapeMarkup()
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[blue]Total commands:[/] {commands.Count}");
    }

    /// <summary>
    /// Execute command chain with progress display
    /// </summary>
    private int ExecuteCommandChain(List<string[]> commands, ChainExecutionContext ctx)
    {
        AnsiConsole.MarkupLine($"[blue]Executing command chain with {commands.Count} step(s)...[/]");
        AnsiConsole.WriteLine();

        // Execute commands sequentially
        for (int i = 0; i < commands.Count; i++)
        {
            ctx.CurrentStep = i + 1;
            var cmdArgs = commands[i];

            // Display current step
            var commandStr = string.Join(" ", cmdArgs);
            AnsiConsole.MarkupLine($"[blue]Step {i + 1}/{commands.Count}:[/] {commandStr.EscapeMarkup()}");

            // Execute command
            var result = ExecuteCommand(cmdArgs);
            ctx.Results.Add(result);

            // Display result
            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine($"[green]✓ Success[/] ({result.Duration.TotalSeconds:F2}s)");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed[/] (exit code: {result.ExitCode}, {result.Duration.TotalSeconds:F2}s)");
            }

            AnsiConsole.WriteLine();

            // Check if should stop on error
            if (result.ExitCode != 0 && ctx.StopOnError)
            {
                AnsiConsole.MarkupLine("[red]Stopping execution due to error. Use --continue-on-error to continue through failures.[/]");
                AnsiConsole.WriteLine();
                DisplaySummary(ctx);
                return result.ExitCode;
            }
        }

        // Display summary
        DisplaySummary(ctx);

        // Return exit code (0 if all succeeded, 1 if any failed)
        return ctx.Results.Any(r => r.ExitCode != 0) ? 1 : 0;
    }

    /// <summary>
    /// Execute a single command and capture result
    /// </summary>
    private ChainCommandResult ExecuteCommand(string[] args)
    {
        var result = new ChainCommandResult
        {
            CommandArgs = args
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Create new CommandApp instance with same configuration
            var app = new CommandApp();
            ConfigureCommandApp(app);

            // Execute command
            result.ExitCode = app.Run(args);
            result.Status = result.ExitCode == 0 ? "Success" : "Failed";
        }
        catch (Exception ex)
        {
            result.ExitCode = 1;
            result.Status = "Error";
            AnsiConsole.MarkupLine($"[red]Error executing command: {ex.Message}[/]");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Configure CommandApp with same configuration as main Program.cs
    /// </summary>
    private void ConfigureCommandApp(CommandApp app)
    {
        app.Configure(config =>
        {
            config.SetApplicationName("lrm");
            config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

            // Propagate exceptions so we can handle them intelligently
            config.PropagateExceptions();

            // Validate presence of commands
            config.ValidateExamples();

            // Register all commands (same as Program.cs)
            config.AddCommand<ValidateCommand>("validate")
                .WithDescription("Validate resource files for missing keys, duplicates, and empty values");

            config.AddCommand<StatsCommand>("stats")
                .WithDescription("Display statistics about resource files and translation coverage");

            config.AddCommand<ViewCommand>("view")
                .WithDescription("View details of a specific localization key");

            config.AddCommand<AddCommand>("add")
                .WithDescription("Add a new localization key to all language files");

            config.AddCommand<UpdateCommand>("update")
                .WithDescription("Update values for an existing localization key");

            config.AddCommand<DeleteCommand>("delete")
                .WithDescription("Delete a localization key from all language files");

            config.AddCommand<MergeDuplicatesCommand>("merge-duplicates")
                .WithDescription("Merge duplicate occurrences of a key into a single entry");

            config.AddCommand<ExportCommand>("export")
                .WithDescription("Export resource files to various formats (CSV, JSON, or simple text)");

            config.AddCommand<ImportCommand>("import")
                .WithDescription("Import translations from CSV format");

            config.AddCommand<EditCommand>("edit")
                .WithDescription("Launch interactive TUI editor for resource files");

            config.AddCommand<TranslateCommand>("translate")
                .WithDescription("Translate missing keys using AI translation providers");

            config.AddCommand<ScanCommand>("scan")
                .WithDescription("Scan source code for localization key references");

            config.AddCommand<CheckCommand>("check")
                .WithDescription("Run validation and code scan together");

            config.AddCommand<ListLanguagesCommand>("list-languages")
                .WithDescription("List all available language resource files");

            config.AddCommand<AddLanguageCommand>("add-language")
                .WithDescription("Add a new language resource file");

            config.AddCommand<RemoveLanguageCommand>("remove-language")
                .WithDescription("Remove a language resource file");

            // Config branch
            config.AddBranch("config", configBranch =>
            {
                configBranch.SetDescription("Manage LRM configuration and API keys");

                configBranch.AddCommand<SetApiKeyCommand>("set-api-key")
                    .WithDescription("Set API key for a translation provider");

                configBranch.AddCommand<GetApiKeyCommand>("get-api-key")
                    .WithDescription("Get API key for a translation provider");

                configBranch.AddCommand<DeleteApiKeyCommand>("delete-api-key")
                    .WithDescription("Delete API key for a translation provider");

                configBranch.AddCommand<ListProvidersCommand>("list-providers")
                    .WithDescription("List all available translation providers");
            });

            // Backup branch
            config.AddBranch("backup", backupBranch =>
            {
                backupBranch.SetDescription("Backup management commands for resource files");

                backupBranch.AddCommand<BackupListCommand>("list")
                    .WithDescription("List all backup versions");

                backupBranch.AddCommand<BackupCreateCommand>("create")
                    .WithDescription("Create a manual backup");

                backupBranch.AddCommand<BackupRestoreCommand>("restore")
                    .WithDescription("Restore from a backup version");

                backupBranch.AddCommand<BackupDiffCommand>("diff")
                    .WithDescription("Compare two backup versions or current state");

                backupBranch.AddCommand<BackupInfoCommand>("info")
                    .WithDescription("View backup metadata and statistics");

                backupBranch.AddCommand<BackupPruneCommand>("prune")
                    .WithDescription("Clean up old backup versions");
            });
        });
    }

    /// <summary>
    /// Display execution summary
    /// </summary>
    private void DisplaySummary(ChainExecutionContext ctx)
    {
        AnsiConsole.MarkupLine("[blue]═══ Execution Summary ═══[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("[cyan]Step[/]");
        table.AddColumn("[cyan]Command[/]");
        table.AddColumn("[cyan]Status[/]");
        table.AddColumn("[cyan]Duration[/]");

        for (int i = 0; i < ctx.Results.Count; i++)
        {
            var result = ctx.Results[i];
            var statusMarkup = result.ExitCode == 0
                ? "[green]✓ Success[/]"
                : "[red]✗ Failed[/]";

            table.AddRow(
                $"[blue]{i + 1}[/]",
                result.CommandString.EscapeMarkup(),
                statusMarkup,
                $"{result.Duration.TotalSeconds:F2}s"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Overall status
        int succeeded = ctx.Results.Count(r => r.ExitCode == 0);
        int failed = ctx.Results.Count(r => r.ExitCode != 0);
        var totalDuration = TimeSpan.FromSeconds(ctx.Results.Sum(r => r.Duration.TotalSeconds));

        if (failed == 0)
        {
            AnsiConsole.MarkupLine($"[green]✓ All {succeeded} command(s) completed successfully[/] (Total: {totalDuration.TotalSeconds:F2}s)");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {succeeded} succeeded, {failed} failed[/] (Total: {totalDuration.TotalSeconds:F2}s)");
        }
    }
}
