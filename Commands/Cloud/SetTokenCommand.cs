// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the set-token command.
/// </summary>
public class SetTokenCommandSettings : BaseCommandSettings
{
    [CommandOption("--host <HOST>")]
    [Description("Remote host (e.g., lrm.cloud)")]
    public string? Host { get; set; }

    [CommandOption("--token <TOKEN>")]
    [Description("Access token")]
    public string? Token { get; set; }

    [CommandOption("--expires <DATETIME>")]
    [Description("Token expiration date/time (ISO 8601 format)")]
    public string? Expires { get; set; }
}

/// <summary>
/// Command to manually set an authentication token for cloud access.
/// </summary>
public class SetTokenCommand : Command<SetTokenCommandSettings>
{
    public override int Execute(CommandContext context, SetTokenCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // Get host from remote config if not provided
            string? host = settings.Host;

            if (string.IsNullOrWhiteSpace(host))
            {
                var remotesConfig = Core.Configuration.ConfigurationManager.LoadRemotesConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(remotesConfig.Remote))
                {
                    AnsiConsole.MarkupLine("[red]✗ No remote URL configured and --host not provided![/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Either configure a remote URL with 'lrm remote set <url>'[/]");
                    AnsiConsole.MarkupLine("[dim]or provide --host explicitly[/]");
                    return 1;
                }

                if (!RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL in configuration:[/] {remotesConfig.Remote.EscapeMarkup()}");
                    return 1;
                }

                host = remoteUrl!.Host;
            }

            // Prompt for token if not provided
            string? token = settings.Token;

            if (string.IsNullOrWhiteSpace(token))
            {
                token = AnsiConsole.Prompt(
                    new TextPrompt<string>("[blue]Enter access token:[/]")
                        .Secret());
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                AnsiConsole.MarkupLine("[red]✗ Token cannot be empty![/]");
                return 1;
            }

            // Parse expiration if provided
            DateTime? expiresAt = null;

            if (!string.IsNullOrWhiteSpace(settings.Expires))
            {
                if (!DateTime.TryParse(settings.Expires, out var parsedExpires))
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid expiration date:[/] {settings.Expires.EscapeMarkup()}");
                    AnsiConsole.MarkupLine("[dim]Expected ISO 8601 format, e.g., 2025-12-31T23:59:59Z[/]");
                    return 1;
                }

                expiresAt = parsedExpires.ToUniversalTime();
            }

            // Save token
            AuthTokenManager.SetTokenAsync(projectDirectory, host, token, expiresAt, cancellationToken).GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[green]✓ Token saved for host:[/] {host.EscapeMarkup()}");

            if (expiresAt.HasValue)
            {
                AnsiConsole.MarkupLine($"[dim]Expires: {expiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Token stored in .lrm/auth.json (git-ignored)[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
