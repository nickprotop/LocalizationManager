// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Base settings for snapshot commands.
/// </summary>
public class SnapshotCommandSettings : BaseFormattableCommandSettings
{
}

/// <summary>
/// Settings for listing snapshots.
/// </summary>
public class ListSnapshotsSettings : SnapshotCommandSettings
{
    [CommandOption("--page")]
    [Description("Page number (default: 1)")]
    [DefaultValue(1)]
    public int Page { get; set; } = 1;

    [CommandOption("--page-size")]
    [Description("Number of items per page (default: 20)")]
    [DefaultValue(20)]
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Settings for creating a snapshot.
/// </summary>
public class CreateSnapshotSettings : SnapshotCommandSettings
{
    [CommandArgument(0, "[message]")]
    [Description("Description for the snapshot (optional)")]
    public string? Message { get; set; }
}

/// <summary>
/// Settings for showing a snapshot.
/// </summary>
public class ShowSnapshotSettings : SnapshotCommandSettings
{
    [CommandArgument(0, "<snapshot-id>")]
    [Description("The snapshot ID to show details for")]
    public string SnapshotId { get; set; } = string.Empty;
}

/// <summary>
/// Settings for restoring a snapshot.
/// </summary>
public class RestoreSnapshotSettings : SnapshotCommandSettings
{
    [CommandArgument(0, "<snapshot-id>")]
    [Description("The snapshot ID to restore")]
    public string SnapshotId { get; set; } = string.Empty;

    [CommandOption("--no-backup")]
    [Description("Don't create a backup before restoring")]
    public bool NoBackup { get; set; }

    [CommandOption("-m|--message")]
    [Description("Message for the restore operation")]
    public string? Message { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt")]
    public bool Yes { get; set; }
}

/// <summary>
/// Settings for deleting a snapshot.
/// </summary>
public class DeleteSnapshotSettings : SnapshotCommandSettings
{
    [CommandArgument(0, "<snapshot-id>")]
    [Description("The snapshot ID to delete")]
    public string SnapshotId { get; set; } = string.Empty;

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt")]
    public bool Yes { get; set; }
}

/// <summary>
/// Settings for comparing snapshots.
/// </summary>
public class DiffSnapshotsSettings : SnapshotCommandSettings
{
    [CommandArgument(0, "<from-snapshot-id>")]
    [Description("The source snapshot ID")]
    public string FromSnapshotId { get; set; } = string.Empty;

    [CommandArgument(1, "<to-snapshot-id>")]
    [Description("The target snapshot ID")]
    public string ToSnapshotId { get; set; } = string.Empty;
}

/// <summary>
/// Command to list all snapshots for the project.
/// </summary>
public class ListSnapshotsCommand : AsyncCommand<ListSnapshotsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListSnapshotsSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud init' to connect to a project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud login' to authenticate.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            var response = await apiClient.ListSnapshotsAsync(settings.Page, settings.PageSize);

            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    DisplayListJson(response);
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplayListSimple(response);
                    break;
                default:
                    DisplayListTable(response);
                    break;
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private void DisplayListTable(SnapshotListResponse response)
    {
        if (response.Items.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No snapshots found.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use 'lrm cloud snapshot create' to create a snapshot.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[blue bold]Snapshots[/] (Page {response.Page}, {response.Items.Count} of {response.TotalCount})");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("ID");
        table.AddColumn("Type");
        table.AddColumn("Description");
        table.AddColumn("Keys");
        table.AddColumn("Created");
        table.AddColumn("By");

        foreach (var snapshot in response.Items)
        {
            var typeColor = snapshot.SnapshotType switch
            {
                "manual" => "green",
                "push" => "blue",
                "restore" => "yellow",
                "pre-restore" => "dim",
                _ => "dim"
            };

            table.AddRow(
                snapshot.SnapshotId.EscapeMarkup(),
                $"[{typeColor}]{snapshot.SnapshotType.EscapeMarkup()}[/]",
                (snapshot.Description ?? "").EscapeMarkup(),
                snapshot.KeyCount.ToString(),
                FormatDateTime(snapshot.CreatedAt),
                (snapshot.CreatedByUsername ?? "system").EscapeMarkup()
            );
        }

        AnsiConsole.Write(table);
    }

    private void DisplayListSimple(SnapshotListResponse response)
    {
        Console.WriteLine($"Total: {response.TotalCount}");
        Console.WriteLine($"Page: {response.Page}/{(response.TotalCount + response.PageSize - 1) / response.PageSize}");
        Console.WriteLine();

        foreach (var snapshot in response.Items)
        {
            Console.WriteLine($"{snapshot.SnapshotId}\t{snapshot.SnapshotType}\t{snapshot.KeyCount} keys\t{snapshot.CreatedAt:yyyy-MM-dd HH:mm}\t{snapshot.Description ?? ""}");
        }
    }

    private void DisplayListJson(SnapshotListResponse response)
    {
        Console.WriteLine(OutputFormatter.FormatJson(response));
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalMinutes < 1)
            return "just now";
        if (timeAgo.TotalHours < 1)
            return $"{(int)timeAgo.TotalMinutes}m ago";
        if (timeAgo.TotalDays < 1)
            return $"{(int)timeAgo.TotalHours}h ago";
        if (timeAgo.TotalDays < 7)
            return $"{(int)timeAgo.TotalDays}d ago";

        return dateTime.ToString("yyyy-MM-dd");
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}

/// <summary>
/// Command to create a new snapshot.
/// </summary>
public class CreateSnapshotCommand : AsyncCommand<CreateSnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateSnapshotSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            var description = settings.Message ?? $"Manual snapshot at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

            CloudSnapshot snapshot;
            if (format == Core.Enums.OutputFormat.Table)
            {
                snapshot = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Creating snapshot...", async _ =>
                        await apiClient.CreateSnapshotAsync(description));

                AnsiConsole.MarkupLine($"[green]Snapshot created:[/] {snapshot.SnapshotId}");
                AnsiConsole.MarkupLine($"[dim]Keys: {snapshot.KeyCount}, Translations: {snapshot.TranslationCount}[/]");
            }
            else
            {
                snapshot = await apiClient.CreateSnapshotAsync(description);

                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(snapshot));
                }
                else
                {
                    Console.WriteLine($"Created: {snapshot.SnapshotId}");
                    Console.WriteLine($"Keys: {snapshot.KeyCount}");
                    Console.WriteLine($"Translations: {snapshot.TranslationCount}");
                }
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}

/// <summary>
/// Command to show snapshot details.
/// </summary>
public class ShowSnapshotCommand : AsyncCommand<ShowSnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShowSnapshotSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            if (string.IsNullOrWhiteSpace(settings.SnapshotId))
            {
                AnsiConsole.MarkupLine("[red]Snapshot ID is required.[/]");
                return 1;
            }

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            var snapshot = await apiClient.GetSnapshotAsync(settings.SnapshotId);

            if (snapshot == null)
            {
                AnsiConsole.MarkupLine($"[red]Snapshot not found:[/] {settings.SnapshotId}");
                return 1;
            }

            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    Console.WriteLine(OutputFormatter.FormatJson(snapshot));
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplayShowSimple(snapshot);
                    break;
                default:
                    DisplayShowTable(snapshot);
                    break;
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private void DisplayShowTable(SnapshotDetail snapshot)
    {
        AnsiConsole.MarkupLine($"[blue bold]Snapshot:[/] {snapshot.SnapshotId}");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("ID", snapshot.SnapshotId);
        table.AddRow("Type", snapshot.SnapshotType);
        table.AddRow("Description", snapshot.Description ?? "(none)");
        table.AddRow("Keys", snapshot.KeyCount.ToString());
        table.AddRow("Translations", snapshot.TranslationCount.ToString());
        table.AddRow("Files", snapshot.FileCount.ToString());
        table.AddRow("Created At", snapshot.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
        table.AddRow("Created By", snapshot.CreatedByUsername ?? "system");

        AnsiConsole.Write(table);

        if (snapshot.Files.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Files:[/]");

            var filesTable = new Table();
            filesTable.Border = TableBorder.Simple;
            filesTable.AddColumn("Path");
            filesTable.AddColumn("Language");

            foreach (var file in snapshot.Files)
            {
                filesTable.AddRow(file.Path.EscapeMarkup(), file.LanguageCode.EscapeMarkup());
            }

            AnsiConsole.Write(filesTable);
        }
    }

    private void DisplayShowSimple(SnapshotDetail snapshot)
    {
        Console.WriteLine($"ID: {snapshot.SnapshotId}");
        Console.WriteLine($"Type: {snapshot.SnapshotType}");
        Console.WriteLine($"Description: {snapshot.Description ?? "(none)"}");
        Console.WriteLine($"Keys: {snapshot.KeyCount}");
        Console.WriteLine($"Translations: {snapshot.TranslationCount}");
        Console.WriteLine($"Files: {snapshot.FileCount}");
        Console.WriteLine($"Created At: {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Created By: {snapshot.CreatedByUsername ?? "system"}");

        if (snapshot.Files.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Files:");
            foreach (var file in snapshot.Files)
            {
                Console.WriteLine($"  {file.Path} ({file.LanguageCode})");
            }
        }
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}

/// <summary>
/// Command to restore from a snapshot.
/// </summary>
public class RestoreSnapshotCommand : AsyncCommand<RestoreSnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RestoreSnapshotSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            if (string.IsNullOrWhiteSpace(settings.SnapshotId))
            {
                AnsiConsole.MarkupLine("[red]Snapshot ID is required.[/]");
                return 1;
            }

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            // Confirmation
            if (!settings.Yes && format == Core.Enums.OutputFormat.Table)
            {
                AnsiConsole.MarkupLine("[yellow bold]WARNING:[/] This will replace the server state with the snapshot.");
                AnsiConsole.MarkupLine("[yellow]All team members will see this change on their next pull.[/]");
                AnsiConsole.WriteLine();

                if (!settings.NoBackup)
                {
                    AnsiConsole.MarkupLine("[dim]A backup snapshot will be created before restoring.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No backup will be created before restoring.[/]");
                }

                AnsiConsole.WriteLine();

                if (!AnsiConsole.Confirm($"Restore from snapshot [green]{settings.SnapshotId}[/]?", false))
                {
                    AnsiConsole.MarkupLine("[dim]Operation cancelled.[/]");
                    return 0;
                }
            }

            CloudSnapshot result;
            if (format == Core.Enums.OutputFormat.Table)
            {
                result = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Restoring snapshot...", async _ =>
                        await apiClient.RestoreSnapshotAsync(
                            settings.SnapshotId,
                            !settings.NoBackup,
                            settings.Message));

                AnsiConsole.MarkupLine($"[green]Snapshot restored successfully![/]");
                AnsiConsole.MarkupLine($"[dim]New snapshot created: {result.SnapshotId}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud pull' to sync local files with the restored state.[/]");
            }
            else
            {
                result = await apiClient.RestoreSnapshotAsync(
                    settings.SnapshotId,
                    !settings.NoBackup,
                    settings.Message);

                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        restored = true,
                        snapshotId = settings.SnapshotId,
                        newSnapshotId = result.SnapshotId
                    }));
                }
                else
                {
                    Console.WriteLine($"Restored: {settings.SnapshotId}");
                    Console.WriteLine($"New snapshot: {result.SnapshotId}");
                }
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}

/// <summary>
/// Command to delete a snapshot.
/// </summary>
public class DeleteSnapshotCommand : AsyncCommand<DeleteSnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteSnapshotSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            if (string.IsNullOrWhiteSpace(settings.SnapshotId))
            {
                AnsiConsole.MarkupLine("[red]Snapshot ID is required.[/]");
                return 1;
            }

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            // Confirmation
            if (!settings.Yes && format == Core.Enums.OutputFormat.Table)
            {
                if (!AnsiConsole.Confirm($"Delete snapshot [red]{settings.SnapshotId}[/]?", false))
                {
                    AnsiConsole.MarkupLine("[dim]Operation cancelled.[/]");
                    return 0;
                }
            }

            if (format == Core.Enums.OutputFormat.Table)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Deleting snapshot...", async _ =>
                        await apiClient.DeleteSnapshotAsync(settings.SnapshotId));

                AnsiConsole.MarkupLine($"[green]Snapshot deleted:[/] {settings.SnapshotId}");
            }
            else
            {
                await apiClient.DeleteSnapshotAsync(settings.SnapshotId);

                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        deleted = true,
                        snapshotId = settings.SnapshotId
                    }));
                }
                else
                {
                    Console.WriteLine($"Deleted: {settings.SnapshotId}");
                }
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}

/// <summary>
/// Command to compare two snapshots.
/// </summary>
public class DiffSnapshotsCommand : AsyncCommand<DiffSnapshotsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DiffSnapshotsSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            if (string.IsNullOrWhiteSpace(settings.FromSnapshotId) || string.IsNullOrWhiteSpace(settings.ToSnapshotId))
            {
                AnsiConsole.MarkupLine("[red]Both snapshot IDs are required.[/]");
                return 1;
            }

            // Load cloud config
            var config = await CloudConfigManager.LoadAsync(projectDirectory);

            if (!config.HasProject)
            {
                AnsiConsole.MarkupLine("[red]Not connected to a cloud project.[/]");
                return 1;
            }

            if (!config.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]Not authenticated.[/]");
                return 1;
            }

            if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
                return 1;
            }

            using var apiClient = new CloudApiClient(remoteUrl!);
            SetupAuth(apiClient, config, projectDirectory);

            var diff = await apiClient.DiffSnapshotsAsync(settings.FromSnapshotId, settings.ToSnapshotId);

            if (diff == null)
            {
                AnsiConsole.MarkupLine("[red]One or both snapshots not found.[/]");
                return 1;
            }

            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    Console.WriteLine(OutputFormatter.FormatJson(diff));
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplayDiffSimple(diff);
                    break;
                default:
                    DisplayDiffTable(diff);
                    break;
            }

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private void DisplayDiffTable(SnapshotDiff diff)
    {
        AnsiConsole.MarkupLine($"[blue bold]Comparing snapshots[/]");
        AnsiConsole.MarkupLine($"[dim]From: {diff.FromSnapshotId} -> To: {diff.ToSnapshotId}[/]");
        AnsiConsole.WriteLine();

        // Summary
        var summaryTable = new Table();
        summaryTable.Border = TableBorder.Rounded;
        summaryTable.AddColumn("Change");
        summaryTable.AddColumn("Count");

        if (diff.KeysAdded > 0)
            summaryTable.AddRow("[green]Keys Added[/]", $"[green]+{diff.KeysAdded}[/]");
        if (diff.KeysRemoved > 0)
            summaryTable.AddRow("[red]Keys Removed[/]", $"[red]-{diff.KeysRemoved}[/]");
        if (diff.KeysModified > 0)
            summaryTable.AddRow("[yellow]Keys Modified[/]", $"[yellow]~{diff.KeysModified}[/]");

        if (diff.KeysAdded == 0 && diff.KeysRemoved == 0 && diff.KeysModified == 0)
        {
            summaryTable.AddRow("[dim]No changes[/]", "[dim]0[/]");
        }

        AnsiConsole.Write(summaryTable);

        // Files
        if (diff.Files.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]File Changes:[/]");

            var filesTable = new Table();
            filesTable.Border = TableBorder.Simple;
            filesTable.AddColumn("Path");
            filesTable.AddColumn("Change");

            foreach (var file in diff.Files)
            {
                var changeColor = file.ChangeType switch
                {
                    "added" => "green",
                    "removed" => "red",
                    "modified" => "yellow",
                    _ => "dim"
                };

                filesTable.AddRow(file.Path.EscapeMarkup(), $"[{changeColor}]{file.ChangeType}[/]");
            }

            AnsiConsole.Write(filesTable);
        }
    }

    private void DisplayDiffSimple(SnapshotDiff diff)
    {
        Console.WriteLine($"From: {diff.FromSnapshotId}");
        Console.WriteLine($"To: {diff.ToSnapshotId}");
        Console.WriteLine($"Keys Added: {diff.KeysAdded}");
        Console.WriteLine($"Keys Removed: {diff.KeysRemoved}");
        Console.WriteLine($"Keys Modified: {diff.KeysModified}");

        if (diff.Files.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Files:");
            foreach (var file in diff.Files)
            {
                var prefix = file.ChangeType switch
                {
                    "added" => "+",
                    "removed" => "-",
                    "modified" => "~",
                    _ => " "
                };
                Console.WriteLine($"  {prefix} {file.Path}");
            }
        }
    }

    private static void SetupAuth(CloudApiClient apiClient, CloudConfig config, string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
            apiClient.EnableAutoRefresh(projectDirectory);
        }
    }
}
