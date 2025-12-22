// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the revert command.
/// </summary>
public class RevertCommandSettings : BaseFormattableCommandSettings
{
    [CommandArgument(0, "<HISTORY_ID>")]
    [Description("The history entry ID to revert (e.g., abc12345)")]
    public required string HistoryId { get; set; }

    [CommandOption("-m|--message <MESSAGE>")]
    [Description("Message describing why the revert was done")]
    public string? Message { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt")]
    [DefaultValue(false)]
    public bool Yes { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be reverted without actually reverting")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }
}

/// <summary>
/// Command to revert a previous push (undo changes).
/// </summary>
public class RevertCommand : Command<RevertCommandSettings>
{
    public override int Execute(CommandContext context, RevertCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            // Load cloud configuration
            var cloudConfig = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(cloudConfig.ApiKey))
            {
                cloudConfig.ApiKey = envApiKey;
            }

            // Validate remote configuration
            if (!cloudConfig.HasProject)
            {
                AnsiConsole.MarkupLine("[red]✗ No remote project configured![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud init <url>' to connect to a project[/]");
                return 1;
            }

            // Parse remote URL
            if (!RemoteUrlParser.TryParse(cloudConfig.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {cloudConfig.Remote?.EscapeMarkup()}");
                return 1;
            }

            // Check authentication
            if (!cloudConfig.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]✗ Not authenticated![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login {remoteUrl!.Host}' to authenticate[/]");
                return 1;
            }

            // Create API client
            using var apiClient = new CloudApiClient(remoteUrl!);

            if (!string.IsNullOrWhiteSpace(cloudConfig.ApiKey))
            {
                apiClient.SetApiKey(cloudConfig.ApiKey);
            }
            else
            {
                apiClient.SetAccessToken(cloudConfig.AccessToken);
                apiClient.EnableAutoRefresh(projectDirectory);
            }

            // Fetch history detail to show what will be reverted
            SyncHistoryDetailDto? detail = null;

            AnsiConsole.Status()
                .Start("Fetching history details...", ctx =>
                {
                    detail = apiClient.GetSyncHistoryDetailAsync(settings.HistoryId, cancellationToken).GetAwaiter().GetResult();
                });

            if (detail == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ History entry '{settings.HistoryId}' not found[/]");
                return 1;
            }

            // Check if already reverted
            if (detail.Status == "reverted")
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ This push has already been reverted[/]");
                return 1;
            }

            // Show what will be reverted
            if (format == Core.Enums.OutputFormat.Table)
            {
                ShowRevertPreview(detail);
            }

            // Handle dry run
            if (settings.DryRun)
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        dryRun = true,
                        historyId = detail.HistoryId,
                        operationType = detail.OperationType,
                        message = detail.Message,
                        entriesAdded = detail.EntriesAdded,
                        entriesModified = detail.EntriesModified,
                        entriesDeleted = detail.EntriesDeleted,
                        changes = detail.Changes.Count
                    }));
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]Dry run - no changes will be made[/]");
                }
                return 0;
            }

            // Confirm with user
            if (!settings.Yes && format == Core.Enums.OutputFormat.Table)
            {
                AnsiConsole.WriteLine();
                var confirm = AnsiConsole.Confirm("[yellow]Are you sure you want to revert this push?[/]", false);

                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[dim]Revert cancelled[/]");
                    return 0;
                }
            }

            // Perform the revert
            RevertResponse? response = null;

            AnsiConsole.Status()
                .Start("Reverting changes...", ctx =>
                {
                    response = apiClient.RevertSyncHistoryAsync(settings.HistoryId, settings.Message, cancellationToken).GetAwaiter().GetResult();
                });

            if (response == null || !response.Success)
            {
                AnsiConsole.MarkupLine("[red]✗ Revert failed[/]");
                return 1;
            }

            // Display result
            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    DisplayResultJson(response);
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplayResultSimple(response);
                    break;
                case Core.Enums.OutputFormat.Table:
                default:
                    DisplayResultTable(response);
                    break;
            }

            // Hint to pull
            if (format == Core.Enums.OutputFormat.Table)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud pull' to update your local files with the reverted state.[/]");
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Cloud API error: {ex.Message.EscapeMarkup()}[/]");

            if (ex.StatusCode == 401)
            {
                AnsiConsole.MarkupLine("[dim]Your authentication token may have expired. Please login again.[/]");
            }
            else if (ex.StatusCode == 403)
            {
                AnsiConsole.MarkupLine("[dim]You don't have permission to revert changes in this project.[/]");
            }
            else if (ex.StatusCode == 400)
            {
                AnsiConsole.MarkupLine("[dim]This push may have already been reverted or has no changes to revert.[/]");
            }

            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private void ShowRevertPreview(SyncHistoryDetailDto detail)
    {
        AnsiConsole.MarkupLine("[yellow bold]About to revert:[/]");
        AnsiConsole.WriteLine();

        // Show the push details
        var typeColor = detail.OperationType == "revert" ? "yellow" : "blue";
        AnsiConsole.MarkupLine($"[{typeColor}]{detail.OperationType}[/] [{typeColor}]{detail.HistoryId}[/]");

        // Author and date
        var author = detail.UserName ?? detail.UserEmail ?? "unknown";
        AnsiConsole.MarkupLine($"[dim]Author:[/] {author.EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[dim]Date:[/]   {FormatDateTime(detail.CreatedAt)}");

        // Message
        if (!string.IsNullOrWhiteSpace(detail.Message))
        {
            AnsiConsole.MarkupLine($"[dim]Message:[/] {detail.Message.EscapeMarkup()}");
        }

        // Stats
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Entries to restore:[/]");

        if (detail.EntriesAdded > 0)
        {
            AnsiConsole.MarkupLine($"  [red]Will remove {detail.EntriesAdded} added entries[/]");
        }
        if (detail.EntriesModified > 0)
        {
            AnsiConsole.MarkupLine($"  [yellow]Will restore {detail.EntriesModified} modified entries to previous values[/]");
        }
        if (detail.EntriesDeleted > 0)
        {
            AnsiConsole.MarkupLine($"  [green]Will restore {detail.EntriesDeleted} deleted entries[/]");
        }

        // Show first few changes
        if (detail.Changes.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]First few changes that will be reversed:[/]");

            foreach (var change in detail.Changes.Take(5))
            {
                var inverse = GetInverseChange(change.ChangeType);
                AnsiConsole.MarkupLine($"  {inverse}: {change.Key.EscapeMarkup()} ({change.Lang})");
            }

            if (detail.Changes.Count > 5)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {detail.Changes.Count - 5} more[/]");
            }
        }
    }

    private string GetInverseChange(string changeType)
    {
        return changeType switch
        {
            "added" => "[red]Remove[/]",
            "deleted" => "[green]Restore[/]",
            "modified" => "[yellow]Revert[/]",
            _ => changeType
        };
    }

    private void DisplayResultTable(RevertResponse response)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green bold]✓ Revert completed successfully![/]");
        AnsiConsole.WriteLine();

        if (response.History != null)
        {
            AnsiConsole.MarkupLine($"  [dim]Revert ID:[/] {response.History.HistoryId}");
        }
        AnsiConsole.MarkupLine($"  [dim]Entries restored:[/] {response.EntriesRestored}");
    }

    private void DisplayResultSimple(RevertResponse response)
    {
        Console.WriteLine($"Success: {response.Success}");
        Console.WriteLine($"Entries Restored: {response.EntriesRestored}");
        if (response.History != null)
        {
            Console.WriteLine($"Revert ID: {response.History.HistoryId}");
        }
    }

    private void DisplayResultJson(RevertResponse response)
    {
        Console.WriteLine(OutputFormatter.FormatJson(new
        {
            success = response.Success,
            entriesRestored = response.EntriesRestored,
            history = response.History != null ? new
            {
                historyId = response.History.HistoryId,
                operationType = response.History.OperationType,
                message = response.History.Message,
                entriesAdded = response.History.EntriesAdded,
                entriesModified = response.History.EntriesModified,
                entriesDeleted = response.History.EntriesDeleted,
                createdAt = response.History.CreatedAt
            } : null
        }));
    }

    private string FormatDateTime(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalMinutes < 1)
            return "just now";
        if (timeAgo.TotalHours < 1)
            return $"{(int)timeAgo.TotalMinutes} minute(s) ago";
        if (timeAgo.TotalDays < 1)
            return $"{(int)timeAgo.TotalHours} hour(s) ago";
        if (timeAgo.TotalDays < 7)
            return $"{(int)timeAgo.TotalDays} day(s) ago";

        return dateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }
}
