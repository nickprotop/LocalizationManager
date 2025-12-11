// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the cloud logout command.
/// </summary>
public class LogoutCommandSettings : BaseCommandSettings
{
}

/// <summary>
/// Command to logout from the cloud by clearing stored authentication tokens.
/// </summary>
public class LogoutCommand : Command<LogoutCommandSettings>
{
    public override int Execute(CommandContext context, LogoutCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // Load config
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken)
                .GetAwaiter().GetResult();

            // Check if logged in
            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[yellow]Not logged in[/]");
                return 0;
            }

            var host = config.Host ?? "unknown";

            // Clear auth
            CloudConfigManager.ClearAuthAsync(projectDirectory, cancellationToken)
                .GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[green]Logged out from {host.EscapeMarkup()}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
