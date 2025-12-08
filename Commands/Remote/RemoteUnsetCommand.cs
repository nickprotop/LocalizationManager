// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Remote;

/// <summary>
/// Settings for the remote unset command.
/// </summary>
public class RemoteUnsetCommandSettings : BaseCommandSettings
{
    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt")]
    [DefaultValue(false)]
    public bool SkipConfirmation { get; set; }
}

/// <summary>
/// Command to remove the remote URL configuration.
/// </summary>
public class RemoteUnsetCommand : Command<RemoteUnsetCommandSettings>
{
    public override int Execute(CommandContext context, RemoteUnsetCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // Load current configuration
            var config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory).GetAwaiter().GetResult();

            // Check if cloud configuration exists
            if (config.Cloud == null || string.IsNullOrWhiteSpace(config.Cloud.Remote))
            {
                AnsiConsole.MarkupLine("[yellow]No remote URL configured[/]");
                return 0;
            }

            var remoteUrl = config.Cloud.Remote;

            // Ask for confirmation unless --yes is provided
            if (!settings.SkipConfirmation)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[yellow]This will remove the remote URL:[/] {remoteUrl.EscapeMarkup()}");
                AnsiConsole.WriteLine();

                if (!AnsiConsole.Confirm("Are you sure you want to continue?", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled[/]");
                    return 0;
                }
            }

            // Clear cloud configuration
            config.Cloud.Remote = null;
            config.Cloud.Enabled = false;

            // Save configuration
            Core.Configuration.ConfigurationManager.SaveTeamConfigurationAsync(projectDirectory, config).GetAwaiter().GetResult();

            AnsiConsole.MarkupLine("[green]✓ Remote URL removed[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url>' to configure a new remote URL[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
