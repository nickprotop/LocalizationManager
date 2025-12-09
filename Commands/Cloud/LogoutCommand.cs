// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using LrmConfigurationManager = LocalizationManager.Core.Configuration.ConfigurationManager;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the cloud logout command.
/// </summary>
public class LogoutCommandSettings : BaseCommandSettings
{
    [CommandOption("--host <HOST>")]
    [Description("Cloud host to logout from (default: auto-detect from remote or lrm.cloud)")]
    public string? Host { get; set; }

    [CommandOption("--all")]
    [Description("Logout from all hosts")]
    [DefaultValue(false)]
    public bool All { get; set; }
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

            if (settings.All)
            {
                return LogoutAll(projectDirectory, cancellationToken);
            }

            // Determine host
            var host = DetermineHost(projectDirectory, settings.Host, cancellationToken);

            // Check if logged in
            var hasToken = AuthTokenManager.HasTokenAsync(projectDirectory, host, cancellationToken)
                .GetAwaiter().GetResult();

            if (!hasToken)
            {
                AnsiConsole.MarkupLine($"[yellow]Not logged in to {host.EscapeMarkup()}[/]");
                return 0;
            }

            // Remove token
            AuthTokenManager.RemoveTokenAsync(projectDirectory, host, cancellationToken)
                .GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[green]✓ Logged out from {host.EscapeMarkup()}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private int LogoutAll(string projectDirectory, CancellationToken cancellationToken)
    {
        var authPath = Path.Combine(projectDirectory, ".lrm", "auth.json");

        if (!File.Exists(authPath))
        {
            AnsiConsole.MarkupLine("[yellow]Not logged in to any hosts[/]");
            return 0;
        }

        try
        {
            File.Delete(authPath);
            AnsiConsole.MarkupLine("[green]✓ Logged out from all hosts[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to remove auth tokens: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private string DetermineHost(string projectDirectory, string? providedHost, CancellationToken cancellationToken)
    {
        // If host provided via CLI, use it
        if (!string.IsNullOrWhiteSpace(providedHost))
        {
            // Strip port if present for host matching
            if (providedHost.Contains(':'))
            {
                return providedHost.Split(':')[0];
            }
            return providedHost;
        }

        // Try to load from remotes configuration
        try
        {
            var remotesConfig = LrmConfigurationManager
                .LoadRemotesConfigurationAsync(projectDirectory, cancellationToken)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrWhiteSpace(remotesConfig.Remote))
            {
                if (RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
                {
                    return remoteUrl!.Host;
                }
            }
        }
        catch
        {
            // Ignore errors loading remotes config
        }

        // Default to lrm.cloud
        return "lrm.cloud";
    }
}
