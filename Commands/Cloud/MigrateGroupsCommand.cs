// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Cloud;

public class MigrateGroupsCommandSettings : BaseCommandSettings
{
    [CommandOption("--from <BASE_NAME>")]
    [Description("BaseName to migrate FROM. Defaults to \"\" (legacy single-group rows).")]
    public string FromBaseName { get; set; } = string.Empty;

    [CommandOption("--to <BASE_NAME>")]
    [Description("BaseName to migrate TO. Required; must be non-empty.")]
    public string? ToBaseName { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt.")]
    public bool Yes { get; set; }
}

/// <summary>
/// CLI command to bulk-rekey resource keys in the cloud project from one
/// BaseName to another. Run this after upgrading a single-group project to a
/// multi-group layout so existing cloud rows (originally stored with
/// <c>BaseName=""</c>) match the new BaseName the client now sends.
/// </summary>
public class MigrateGroupsCommand : Command<MigrateGroupsCommandSettings>
{
    public override int Execute(CommandContext context, MigrateGroupsCommandSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ToBaseName))
        {
            AnsiConsole.MarkupLine("[red]--to is required and must be non-empty[/]");
            return 1;
        }

        try
        {
            var projectDirectory = settings.GetResourcePath();
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(config.ApiKey))
            {
                config.ApiKey = envApiKey;
            }

            if (!config.HasProject || !RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine("[red]No cloud remote configured for this project. Run 'lrm cloud init' first.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated. Run 'lrm cloud login' or 'lrm cloud set-api-key' first.[/]");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                apiClient.SetApiKey(config.ApiKey);
            }
            else if (!string.IsNullOrWhiteSpace(config.AccessToken))
            {
                apiClient.SetAccessToken(config.AccessToken);
            }

            AnsiConsole.MarkupLine($"Migrating resource keys: BaseName [yellow]'{settings.FromBaseName.EscapeMarkup()}'[/] → [green]'{settings.ToBaseName!.EscapeMarkup()}'[/]");
            AnsiConsole.MarkupLine("[dim]This rekeys every row in the source group; conflicts (same KeyName already in target) are detected and rolled back.[/]");

            if (!settings.Yes)
            {
                var confirm = AnsiConsole.Confirm("Proceed?", defaultValue: false);
                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[yellow]Aborted by user[/]");
                    return 0;
                }
            }

            var request = new MigrateGroupsRequest
            {
                FromBaseName = settings.FromBaseName,
                ToBaseName = settings.ToBaseName!
            };

            MigrateGroupsResponse response;
            try
            {
                response = apiClient.MigrateGroupsAsync(request, cancellationToken).GetAwaiter().GetResult();
            }
            catch (CloudApiException ex) when (ex.StatusCode == 409)
            {
                AnsiConsole.MarkupLine("[red]Migration would conflict with existing rows in the target group.[/]");
                AnsiConsole.MarkupLine("[dim]Resolve the conflicts (delete one side, edit the target, etc.) and retry.[/]");
                return 1;
            }

            if (response.ConflictingKeys.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]Migration aborted: {response.ConflictingKeys.Count} conflicting key(s):[/]");
                foreach (var key in response.ConflictingKeys.Take(10))
                {
                    AnsiConsole.MarkupLine($"  - {key.EscapeMarkup()}");
                }
                if (response.ConflictingKeys.Count > 10)
                {
                    AnsiConsole.MarkupLine($"  ... and {response.ConflictingKeys.Count - 10} more");
                }
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓ Migrated {response.RowsUpdated} resource key(s)[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
