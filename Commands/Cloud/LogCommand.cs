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
/// Settings for the log command.
/// </summary>
public class LogCommandSettings : BaseFormattableCommandSettings
{
    [CommandArgument(0, "[HISTORY_ID]")]
    [Description("Optional history ID to show details for a specific push")]
    public string? HistoryId { get; set; }

    [CommandOption("-n|--number <COUNT>")]
    [Description("Number of entries to show (default: 10)")]
    [DefaultValue(10)]
    public int Count { get; set; }

    [CommandOption("--page <PAGE>")]
    [Description("Page number for pagination (default: 1)")]
    [DefaultValue(1)]
    public int Page { get; set; }

    [CommandOption("--oneline")]
    [Description("Show compact one-line format")]
    [DefaultValue(false)]
    public bool OneLine { get; set; }
}

/// <summary>
/// Command to show sync history (like git log).
/// </summary>
public class LogCommand : Command<LogCommandSettings>
{
    public override int Execute(CommandContext context, LogCommandSettings settings, CancellationToken cancellationToken = default)
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

            // If history ID is provided, show details
            if (!string.IsNullOrWhiteSpace(settings.HistoryId))
            {
                return ShowHistoryDetail(apiClient, settings.HistoryId, format, cancellationToken);
            }

            // Otherwise, show history list
            return ShowHistoryList(apiClient, settings, format, cancellationToken);
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
                AnsiConsole.MarkupLine("[dim]You don't have permission to view history for this project.[/]");
            }

            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private int ShowHistoryList(CloudApiClient apiClient, LogCommandSettings settings, Core.Enums.OutputFormat format, CancellationToken cancellationToken)
    {
        SyncHistoryListResponse? history = null;

        AnsiConsole.Status()
            .Start("Fetching history...", ctx =>
            {
                history = apiClient.GetSyncHistoryAsync(settings.Page, settings.Count, cancellationToken).GetAwaiter().GetResult();
            });

        if (history == null || history.Items.Count == 0)
        {
            if (format == Core.Enums.OutputFormat.Json)
            {
                Console.WriteLine(OutputFormatter.FormatJson(new { items = Array.Empty<object>(), total = 0 }));
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No history entries found[/]");
            }
            return 0;
        }

        switch (format)
        {
            case Core.Enums.OutputFormat.Json:
                DisplayHistoryJson(history);
                break;
            case Core.Enums.OutputFormat.Simple:
                DisplayHistorySimple(history, settings.OneLine);
                break;
            case Core.Enums.OutputFormat.Table:
            default:
                if (settings.OneLine)
                {
                    DisplayHistoryOneLine(history);
                }
                else
                {
                    DisplayHistoryTable(history);
                }
                break;
        }

        return 0;
    }

    private int ShowHistoryDetail(CloudApiClient apiClient, string historyId, Core.Enums.OutputFormat format, CancellationToken cancellationToken)
    {
        SyncHistoryDetailDto? detail = null;

        AnsiConsole.Status()
            .Start("Fetching history details...", ctx =>
            {
                detail = apiClient.GetSyncHistoryDetailAsync(historyId, cancellationToken).GetAwaiter().GetResult();
            });

        if (detail == null)
        {
            AnsiConsole.MarkupLine($"[red]✗ History entry '{historyId}' not found[/]");
            return 1;
        }

        switch (format)
        {
            case Core.Enums.OutputFormat.Json:
                DisplayDetailJson(detail);
                break;
            case Core.Enums.OutputFormat.Simple:
                DisplayDetailSimple(detail);
                break;
            case Core.Enums.OutputFormat.Table:
            default:
                DisplayDetailTable(detail);
                break;
        }

        return 0;
    }

    #region History List Display

    private void DisplayHistoryTable(SyncHistoryListResponse history)
    {
        foreach (var item in history.Items)
        {
            DisplayHistoryEntry(item);
            AnsiConsole.WriteLine();
        }

        if (history.HasMore)
        {
            AnsiConsole.MarkupLine($"[dim]Showing {history.Items.Count} of {history.Total} entries. Use --page to see more.[/]");
        }
    }

    private void DisplayHistoryEntry(SyncHistoryDto item)
    {
        // Header line (like git log)
        var typeColor = item.OperationType == "revert" ? "yellow" : "blue";
        AnsiConsole.MarkupLine($"[{typeColor} bold]{item.OperationType}[/] [{typeColor}]{item.HistoryId}[/]");

        // Author and date
        var author = item.UserName ?? item.UserEmail ?? "unknown";
        AnsiConsole.MarkupLine($"[dim]Author:[/] {author.EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[dim]Date:[/]   {FormatDateTime(item.CreatedAt)}");

        // Status if reverted
        if (item.Status == "reverted")
        {
            AnsiConsole.MarkupLine("[yellow]Status: reverted[/]");
        }

        // Stats
        var stats = new List<string>();
        if (item.EntriesAdded > 0) stats.Add($"[green]+{item.EntriesAdded}[/]");
        if (item.EntriesModified > 0) stats.Add($"[yellow]~{item.EntriesModified}[/]");
        if (item.EntriesDeleted > 0) stats.Add($"[red]-{item.EntriesDeleted}[/]");

        if (stats.Count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Stats:[/]  {string.Join(" ", stats)}");
        }

        // Message
        if (!string.IsNullOrWhiteSpace(item.Message))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"    {item.Message.EscapeMarkup()}");
        }
    }

    private void DisplayHistoryOneLine(SyncHistoryListResponse history)
    {
        foreach (var item in history.Items)
        {
            var typeChar = item.OperationType == "revert" ? "R" : "P";
            var typeColor = item.OperationType == "revert" ? "yellow" : "blue";
            var statusMark = item.Status == "reverted" ? "[dim](reverted)[/]" : "";

            var stats = new List<string>();
            if (item.EntriesAdded > 0) stats.Add($"[green]+{item.EntriesAdded}[/]");
            if (item.EntriesModified > 0) stats.Add($"[yellow]~{item.EntriesModified}[/]");
            if (item.EntriesDeleted > 0) stats.Add($"[red]-{item.EntriesDeleted}[/]");
            var statsStr = stats.Count > 0 ? $" ({string.Join(" ", stats)})" : "";

            var message = item.Message ?? "[dim]no message[/]";
            if (message.Length > 50)
            {
                message = message.Substring(0, 47) + "...";
            }

            AnsiConsole.MarkupLine($"[{typeColor}]{item.HistoryId}[/] [{typeColor}]{typeChar}[/]{statsStr} {message.EscapeMarkup()} {statusMark}");
        }

        if (history.HasMore)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]({history.Total} total - use --page for more)[/]");
        }
    }

    private void DisplayHistorySimple(SyncHistoryListResponse history, bool oneLine)
    {
        foreach (var item in history.Items)
        {
            if (oneLine)
            {
                Console.WriteLine($"{item.HistoryId} {item.OperationType} +{item.EntriesAdded} ~{item.EntriesModified} -{item.EntriesDeleted} {item.Message ?? ""}");
            }
            else
            {
                Console.WriteLine($"ID: {item.HistoryId}");
                Console.WriteLine($"Type: {item.OperationType}");
                Console.WriteLine($"Author: {item.UserName ?? item.UserEmail ?? "unknown"}");
                Console.WriteLine($"Date: {item.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Added: {item.EntriesAdded}");
                Console.WriteLine($"Modified: {item.EntriesModified}");
                Console.WriteLine($"Deleted: {item.EntriesDeleted}");
                Console.WriteLine($"Status: {item.Status}");
                if (!string.IsNullOrWhiteSpace(item.Message))
                    Console.WriteLine($"Message: {item.Message}");
                Console.WriteLine();
            }
        }
    }

    private void DisplayHistoryJson(SyncHistoryListResponse history)
    {
        Console.WriteLine(OutputFormatter.FormatJson(new
        {
            items = history.Items.Select(h => new
            {
                historyId = h.HistoryId,
                operationType = h.OperationType,
                message = h.Message,
                userEmail = h.UserEmail,
                userName = h.UserName,
                entriesAdded = h.EntriesAdded,
                entriesModified = h.EntriesModified,
                entriesDeleted = h.EntriesDeleted,
                status = h.Status,
                createdAt = h.CreatedAt
            }),
            total = history.Total,
            page = history.Page,
            pageSize = history.PageSize,
            hasMore = history.HasMore
        }));
    }

    #endregion

    #region History Detail Display

    private void DisplayDetailTable(SyncHistoryDetailDto detail)
    {
        // Header
        var typeColor = detail.OperationType == "revert" ? "yellow" : "blue";
        AnsiConsole.MarkupLine($"[{typeColor} bold]{detail.OperationType}[/] [{typeColor}]{detail.HistoryId}[/]");

        if (detail.RevertedFromId != null)
        {
            AnsiConsole.MarkupLine($"[dim]Reverted from:[/] {detail.RevertedFromId}");
        }

        // Author and date
        var author = detail.UserName ?? detail.UserEmail ?? "unknown";
        AnsiConsole.MarkupLine($"[dim]Author:[/] {author.EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[dim]Date:[/]   {FormatDateTime(detail.CreatedAt)}");

        // Status
        if (detail.Status == "reverted")
        {
            AnsiConsole.MarkupLine("[yellow]Status: This push has been reverted[/]");
        }

        // Message
        if (!string.IsNullOrWhiteSpace(detail.Message))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"    {detail.Message.EscapeMarkup()}");
        }

        // Changes
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Changes ({detail.Changes.Count}):[/]");
        AnsiConsole.WriteLine();

        if (detail.Changes.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No changes recorded[/]");
            return;
        }

        foreach (var change in detail.Changes)
        {
            var changeColor = change.ChangeType switch
            {
                "added" => "green",
                "deleted" => "red",
                _ => "yellow"
            };
            var changeSymbol = change.ChangeType switch
            {
                "added" => "+",
                "deleted" => "-",
                _ => "~"
            };

            AnsiConsole.MarkupLine($"[{changeColor}]{changeSymbol}[/] [{changeColor}]{change.Key.EscapeMarkup()}[/] ({change.Lang})");

            if (change.ChangeType == "added")
            {
                AnsiConsole.MarkupLine($"  [green]+ {TruncateValue(change.AfterValue)}[/]");
            }
            else if (change.ChangeType == "deleted")
            {
                AnsiConsole.MarkupLine($"  [red]- {TruncateValue(change.BeforeValue)}[/]");
            }
            else // modified
            {
                AnsiConsole.MarkupLine($"  [red]- {TruncateValue(change.BeforeValue)}[/]");
                AnsiConsole.MarkupLine($"  [green]+ {TruncateValue(change.AfterValue)}[/]");
            }
        }

        // Hint
        if (detail.Status != "reverted")
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]To revert this push: lrm cloud revert {detail.HistoryId}[/]");
        }
    }

    private void DisplayDetailSimple(SyncHistoryDetailDto detail)
    {
        Console.WriteLine($"ID: {detail.HistoryId}");
        Console.WriteLine($"Type: {detail.OperationType}");
        Console.WriteLine($"Author: {detail.UserName ?? detail.UserEmail ?? "unknown"}");
        Console.WriteLine($"Date: {detail.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Status: {detail.Status}");
        if (!string.IsNullOrWhiteSpace(detail.Message))
            Console.WriteLine($"Message: {detail.Message}");
        if (detail.RevertedFromId != null)
            Console.WriteLine($"Reverted From: {detail.RevertedFromId}");

        Console.WriteLine();
        Console.WriteLine($"Changes ({detail.Changes.Count}):");
        foreach (var change in detail.Changes)
        {
            Console.WriteLine($"  {change.ChangeType}: {change.Key} ({change.Lang})");
            if (change.BeforeValue != null)
                Console.WriteLine($"    Before: {change.BeforeValue}");
            if (change.AfterValue != null)
                Console.WriteLine($"    After: {change.AfterValue}");
        }
    }

    private void DisplayDetailJson(SyncHistoryDetailDto detail)
    {
        Console.WriteLine(OutputFormatter.FormatJson(new
        {
            historyId = detail.HistoryId,
            operationType = detail.OperationType,
            message = detail.Message,
            userEmail = detail.UserEmail,
            userName = detail.UserName,
            entriesAdded = detail.EntriesAdded,
            entriesModified = detail.EntriesModified,
            entriesDeleted = detail.EntriesDeleted,
            status = detail.Status,
            createdAt = detail.CreatedAt,
            revertedFromId = detail.RevertedFromId,
            changes = detail.Changes.Select(c => new
            {
                key = c.Key,
                lang = c.Lang,
                changeType = c.ChangeType,
                beforeValue = c.BeforeValue,
                afterValue = c.AfterValue,
                beforeComment = c.BeforeComment,
                afterComment = c.AfterComment
            })
        }));
    }

    #endregion

    #region Helpers

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

    private string TruncateValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "[dim](empty)[/]";

        // Escape markup first
        var escaped = value.EscapeMarkup();

        // Replace newlines
        escaped = escaped.Replace("\r\n", "↵").Replace("\n", "↵");

        if (escaped.Length > 80)
        {
            return escaped.Substring(0, 77) + "...";
        }

        return escaped;
    }

    #endregion
}
