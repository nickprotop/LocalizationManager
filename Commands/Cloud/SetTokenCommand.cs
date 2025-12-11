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
    [CommandArgument(0, "[TOKEN]")]
    [Description("Access token (will prompt if not provided)")]
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
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

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
                AnsiConsole.MarkupLine("[red]Token cannot be empty[/]");
                return 1;
            }

            // Parse expiration if provided
            DateTime? expiresAt = null;

            if (!string.IsNullOrWhiteSpace(settings.Expires))
            {
                if (!DateTime.TryParse(settings.Expires, out var parsedExpires))
                {
                    AnsiConsole.MarkupLine($"[red]Invalid expiration date:[/] {settings.Expires.EscapeMarkup()}");
                    AnsiConsole.MarkupLine("[dim]Expected ISO 8601 format, e.g., 2025-12-31T23:59:59Z[/]");
                    return 1;
                }

                expiresAt = parsedExpires.ToUniversalTime();
            }

            // Save token
            config.AccessToken = token;
            config.ExpiresAt = expiresAt;
            CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

            var host = config.Host ?? "not configured";
            AnsiConsole.MarkupLine($"[green]Token saved[/]");

            if (config.Host != null)
            {
                AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");
            }

            if (expiresAt.HasValue)
            {
                AnsiConsole.MarkupLine($"[dim]Expires:[/] {expiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
