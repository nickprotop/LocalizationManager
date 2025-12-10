// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the set-api-key command.
/// </summary>
public class SetApiKeyCommandSettings : BaseCommandSettings
{
    [CommandOption("--key <KEY>")]
    [Description("API key (will prompt if not provided)")]
    public string? Key { get; set; }

    [CommandOption("--host <HOST>")]
    [Description("Remote host (e.g., lrm.cloud). Auto-detected from remote URL if configured")]
    public string? Host { get; set; }

    [CommandOption("--remove")]
    [Description("Remove stored API key instead of setting one")]
    [DefaultValue(false)]
    public bool Remove { get; set; }
}

/// <summary>
/// Command to set or remove a CLI API key for cloud authentication.
/// </summary>
public class SetApiKeyCommand : Command<SetApiKeyCommandSettings>
{
    public override int Execute(CommandContext context, SetApiKeyCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // 1. Determine host
            var host = DetermineHost(projectDirectory, settings.Host, cancellationToken);

            if (settings.Remove)
            {
                return RemoveApiKey(projectDirectory, host, cancellationToken);
            }
            else
            {
                return SetApiKey(projectDirectory, host, settings.Key, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private int SetApiKey(string projectDirectory, string host, string? providedKey, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Setting CLI API key...[/]");
        AnsiConsole.WriteLine();

        // Get API key (prompt if not provided)
        var apiKey = providedKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("API key:")
                    .Secret());
        }

        // Validate format
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AnsiConsole.MarkupLine("[red]API key cannot be empty[/]");
            return 1;
        }

        if (!apiKey.StartsWith("lrm_"))
        {
            AnsiConsole.MarkupLine("[red]Invalid API key format. API keys must start with 'lrm_'[/]");
            return 1;
        }

        if (apiKey.Length < 20)
        {
            AnsiConsole.MarkupLine("[red]Invalid API key format. API key is too short[/]");
            return 1;
        }

        // Save API key
        AuthTokenManager.SetApiKeyAsync(projectDirectory, host, apiKey, cancellationToken)
            .GetAwaiter().GetResult();

        // Display success
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]API key saved successfully![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[dim]Key:[/] {apiKey[..10].EscapeMarkup()}...");
        AnsiConsole.MarkupLine("[dim]Stored in .lrm/auth.json[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]The API key will be used for cloud operations (push, pull, status).[/]");
        AnsiConsole.MarkupLine("[dim]You can also set LRM_CLOUD_API_KEY environment variable for CI/CD.[/]");

        return 0;
    }

    private int RemoveApiKey(string projectDirectory, string host, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Removing CLI API key...[/]");
        AnsiConsole.WriteLine();

        // Check if key exists
        var existingKey = AuthTokenManager.GetApiKeyAsync(projectDirectory, host, cancellationToken)
            .GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(existingKey))
        {
            AnsiConsole.MarkupLine("[yellow]No API key found for this host[/]");
            return 0;
        }

        // Remove API key
        AuthTokenManager.RemoveApiKeyAsync(projectDirectory, host, cancellationToken)
            .GetAwaiter().GetResult();

        AnsiConsole.MarkupLine("[green]API key removed successfully![/]");
        AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");

        return 0;
    }

    private string DetermineHost(string projectDirectory, string? providedHost, CancellationToken cancellationToken)
    {
        // If host provided via CLI, use it
        if (!string.IsNullOrWhiteSpace(providedHost))
        {
            // Strip port if present for storage key
            if (providedHost.Contains(':'))
            {
                return providedHost.Split(':')[0];
            }
            return providedHost;
        }

        // Try to load from remotes configuration
        try
        {
            var remotesConfig = Core.Configuration.ConfigurationManager
                .LoadRemotesConfigurationAsync(projectDirectory, cancellationToken)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrWhiteSpace(remotesConfig.Remote))
            {
                if (RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
                {
                    AnsiConsole.MarkupLine($"[dim]Using host from configured remote: {remoteUrl!.Host}[/]");
                    return remoteUrl.Host;
                }
            }
        }
        catch
        {
            // Ignore errors loading remotes config
        }

        // Prompt for host
        var hostInput = AnsiConsole.Ask<string>("Cloud host:", "lrm.cloud");

        // Strip port if present
        if (hostInput.Contains(':'))
        {
            return hostInput.Split(':')[0];
        }

        return hostInput;
    }
}
