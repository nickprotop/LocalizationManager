// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the status command.
/// </summary>
public class StatusCommandSettings : BaseFormattableCommandSettings
{
}

/// <summary>
/// Command to show cloud sync status and recent activity.
/// </summary>
public class StatusCommand : Command<StatusCommandSettings>
{
    public override int Execute(CommandContext context, StatusCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            // Load configuration
            var config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check if cloud is configured
            if (config.Cloud == null || string.IsNullOrWhiteSpace(config.Cloud.Remote))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new { configured = false, error = "No remote URL configured" }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No remote URL configured[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url>' to configure a remote URL[/]");
                }
                return 1;
            }

            // Parse remote URL
            if (!RemoteUrlParser.TryParse(config.Cloud.Remote, out var remoteUrl))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new { configured = true, error = "Invalid remote URL" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {config.Cloud.Remote.EscapeMarkup()}");
                }
                return 1;
            }

            // Check authentication
            var hasToken = AuthTokenManager.HasTokenAsync(projectDirectory, remoteUrl!.Host, cancellationToken).GetAwaiter().GetResult();

            if (!hasToken)
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        configured = true,
                        authenticated = false,
                        remote = remoteUrl.ToString(),
                        enabled = config.Cloud.Enabled
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ Not authenticated![/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud set-token --host {remoteUrl.Host.EscapeMarkup()}' to authenticate[/]");
                }
                return 1;
            }

            // Get token and create API client
            var token = AuthTokenManager.GetTokenAsync(projectDirectory, remoteUrl.Host, cancellationToken).GetAwaiter().GetResult();

            using var apiClient = new CloudApiClient(remoteUrl);
            apiClient.SetAccessToken(token);

            // Fetch sync status
            SyncStatus? syncStatus = null;
            try
            {
                syncStatus = apiClient.GetSyncStatusAsync(cancellationToken).GetAwaiter().GetResult();
            }
            catch (CloudApiException ex)
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        configured = true,
                        authenticated = true,
                        remote = remoteUrl.ToString(),
                        enabled = config.Cloud.Enabled,
                        error = ex.Message
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to fetch sync status:[/] {ex.Message.EscapeMarkup()}");
                }
                return 1;
            }

            // Display status
            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    DisplayJson(remoteUrl, config.Cloud, syncStatus);
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplaySimple(remoteUrl, config.Cloud, syncStatus);
                    break;
                case Core.Enums.OutputFormat.Table:
                default:
                    DisplayTable(remoteUrl, config.Cloud, syncStatus);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private void DisplayTable(RemoteUrl remoteUrl, Core.Configuration.CloudConfiguration cloudConfig, SyncStatus syncStatus)
    {
        AnsiConsole.MarkupLine("[blue bold]Cloud Sync Status[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Remote URL", remoteUrl.ToString().EscapeMarkup());
        table.AddRow("Project", remoteUrl.ProjectName);

        var statusColor = cloudConfig.Enabled ? "green" : "yellow";
        var statusText = cloudConfig.Enabled ? "Enabled" : "Disabled";
        table.AddRow("Status", $"[{statusColor}]{statusText}[/]");

        var syncColor = syncStatus.IsSynced ? "green" : "yellow";
        var syncText = syncStatus.IsSynced ? "In sync" : "Out of sync";
        table.AddRow("Sync", $"[{syncColor}]{syncText}[/]");

        if (syncStatus.LastPush.HasValue)
        {
            table.AddRow("Last Push", FormatDateTime(syncStatus.LastPush.Value));
        }

        if (syncStatus.LastPull.HasValue)
        {
            table.AddRow("Last Pull", FormatDateTime(syncStatus.LastPull.Value));
        }

        if (syncStatus.LocalChanges > 0)
        {
            table.AddRow("Local Changes", $"[yellow]{syncStatus.LocalChanges}[/]");
        }

        if (syncStatus.RemoteChanges > 0)
        {
            table.AddRow("Remote Changes", $"[yellow]{syncStatus.RemoteChanges}[/]");
        }

        AnsiConsole.Write(table);

        if (!syncStatus.IsSynced)
        {
            AnsiConsole.WriteLine();
            if (syncStatus.LocalChanges > 0)
            {
                AnsiConsole.MarkupLine("[yellow]💡 Use 'lrm push' to push local changes to the cloud[/]");
            }
            if (syncStatus.RemoteChanges > 0)
            {
                AnsiConsole.MarkupLine("[yellow]💡 Use 'lrm pull' to pull remote changes from the cloud[/]");
            }
        }
    }

    private void DisplaySimple(RemoteUrl remoteUrl, Core.Configuration.CloudConfiguration cloudConfig, SyncStatus syncStatus)
    {
        Console.WriteLine($"Remote URL: {remoteUrl}");
        Console.WriteLine($"Project: {remoteUrl.ProjectName}");
        Console.WriteLine($"Status: {(cloudConfig.Enabled ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Sync: {(syncStatus.IsSynced ? "In sync" : "Out of sync")}");

        if (syncStatus.LastPush.HasValue)
        {
            Console.WriteLine($"Last Push: {FormatDateTime(syncStatus.LastPush.Value)}");
        }

        if (syncStatus.LastPull.HasValue)
        {
            Console.WriteLine($"Last Pull: {FormatDateTime(syncStatus.LastPull.Value)}");
        }

        if (syncStatus.LocalChanges > 0)
        {
            Console.WriteLine($"Local Changes: {syncStatus.LocalChanges}");
        }

        if (syncStatus.RemoteChanges > 0)
        {
            Console.WriteLine($"Remote Changes: {syncStatus.RemoteChanges}");
        }
    }

    private void DisplayJson(RemoteUrl remoteUrl, Core.Configuration.CloudConfiguration cloudConfig, SyncStatus syncStatus)
    {
        var output = new
        {
            configured = true,
            authenticated = true,
            remote = remoteUrl.ToString(),
            project = remoteUrl.ProjectName,
            enabled = cloudConfig.Enabled,
            synced = syncStatus.IsSynced,
            lastPush = syncStatus.LastPush,
            lastPull = syncStatus.LastPull,
            localChanges = syncStatus.LocalChanges,
            remoteChanges = syncStatus.RemoteChanges
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private string FormatDateTime(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalMinutes < 1)
        {
            return "just now";
        }
        else if (timeAgo.TotalHours < 1)
        {
            return $"{(int)timeAgo.TotalMinutes} minute(s) ago";
        }
        else if (timeAgo.TotalDays < 1)
        {
            return $"{(int)timeAgo.TotalHours} hour(s) ago";
        }
        else if (timeAgo.TotalDays < 7)
        {
            return $"{(int)timeAgo.TotalDays} day(s) ago";
        }
        else
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
    }
}
