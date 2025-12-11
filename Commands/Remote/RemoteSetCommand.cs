// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Remote;

/// <summary>
/// Settings for the remote set command.
/// </summary>
public class RemoteSetCommandSettings : BaseCommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("Remote URL (e.g., https://lrm.cloud/org/project or https://lrm.cloud/@username/project)")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Command to set the remote URL for cloud synchronization.
/// </summary>
public class RemoteSetCommand : Command<RemoteSetCommandSettings>
{
    public override int Execute(CommandContext context, RemoteSetCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // Validate remote URL format
            if (!RemoteUrlParser.TryParse(settings.Url, out var remoteUrl))
            {
                AnsiConsole.MarkupLine("[red]✗ Invalid remote URL format![/]");
                AnsiConsole.MarkupLine("[dim]Expected: https://host/org/project or https://host/@username/project[/]");
                AnsiConsole.MarkupLine("[dim]Examples:[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm.cloud/acme-corp/mobile-app[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm.cloud/@john/personal-project[/]");
                return 1;
            }

            // Load current cloud configuration
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Set the remote URL
            config.Remote = remoteUrl!.ToString();

            // Save configuration
            CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

            // Display success message
            AnsiConsole.MarkupLine($"[green]✓ Remote URL set to:[/] {remoteUrl.ToString().EscapeMarkup()}");

            if (remoteUrl.IsPersonalProject)
            {
                AnsiConsole.MarkupLine($"[dim]  Type: Personal project (@{remoteUrl.Username})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]  Type: Organization project ({remoteUrl.Organization})[/]");
            }

            AnsiConsole.MarkupLine($"[dim]  Project: {remoteUrl.ProjectName}[/]");
            AnsiConsole.MarkupLine($"[dim]  API URL: {remoteUrl.ApiBaseUrl.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine($"[dim]  Saved to: .lrm/cloud.json[/]");

            // Warn if using HTTP for non-localhost
            if (!remoteUrl.UseHttps && !IsLocalhost(remoteUrl.Host))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]⚠ Warning: Using HTTP instead of HTTPS for non-localhost connection![/]");
            }

            // Hint about authentication
            if (!config.IsLoggedIn)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Next: 'lrm cloud login {remoteUrl.Host}' to authenticate[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private static bool IsLocalhost(string host)
    {
        var lower = host.ToLowerInvariant();
        return lower == "localhost" || lower == "127.0.0.1" || lower == "::1";
    }
}
