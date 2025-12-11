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
    [CommandArgument(0, "[KEY]")]
    [Description("API key (will prompt if not provided)")]
    public string? Key { get; set; }

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
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            if (settings.Remove)
            {
                return RemoveApiKey(projectDirectory, config, cancellationToken);
            }
            else
            {
                return SetApiKey(projectDirectory, config, settings.Key, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private int SetApiKey(string projectDirectory, CloudConfig config, string? providedKey, CancellationToken cancellationToken)
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
        config.ApiKey = apiKey;
        CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

        // Display success
        var host = config.Host ?? "not set";
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]API key saved successfully![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Key:[/] {apiKey[..10].EscapeMarkup()}...");
        if (config.Host != null)
        {
            AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");
        }
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]The API key will be used for cloud operations (push, pull, status).[/]");
        AnsiConsole.MarkupLine("[dim]You can also set LRM_CLOUD_API_KEY environment variable for CI/CD.[/]");

        return 0;
    }

    private int RemoveApiKey(string projectDirectory, CloudConfig config, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Removing CLI API key...[/]");
        AnsiConsole.WriteLine();

        // Check if key exists
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            AnsiConsole.MarkupLine("[yellow]No API key stored[/]");
            return 0;
        }

        // Remove API key
        config.ApiKey = null;
        CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

        AnsiConsole.MarkupLine("[green]API key removed successfully![/]");

        return 0;
    }
}
