// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
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
    [CommandOption("--account")]
    [Description("Show account info (user profile, projects, organizations) instead of sync status")]
    public bool ShowAccountInfo { get; set; }
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

            // Load cloud config
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(config.ApiKey))
            {
                config.ApiKey = envApiKey;
            }

            // If no remote or --account flag, show account info
            if (!config.HasProject || settings.ShowAccountInfo)
            {
                return DisplayAccountStatus(config, projectDirectory, format, cancellationToken);
            }

            // Show sync status
            return DisplaySyncStatus(config, projectDirectory, format, cancellationToken);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private int DisplaySyncStatus(CloudConfig config, string projectDirectory, Core.Enums.OutputFormat format, CancellationToken cancellationToken)
    {
        // Parse remote URL to get RemoteUrl object
        if (!RemoteUrlParser.TryParse(config.Remote!, out var remoteUrl))
        {
            if (format == Core.Enums.OutputFormat.Json)
            {
                Console.WriteLine(OutputFormatter.FormatJson(new { error = "Invalid remote URL" }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Invalid remote URL:[/] {config.Remote?.EscapeMarkup()}");
            }
            return 1;
        }

        // Check authentication
        if (!config.IsLoggedIn)
        {
            if (format == Core.Enums.OutputFormat.Json)
            {
                Console.WriteLine(OutputFormatter.FormatJson(new
                {
                    configured = true,
                    authenticated = false,
                    remote = config.Remote
                }));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Not authenticated[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login' to authenticate[/]");
            }
            return 1;
        }

        // Create API client
        using var apiClient = new CloudApiClient(remoteUrl!);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
        }

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
                    remote = config.Remote,
                    error = ex.Message
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to fetch sync status:[/] {ex.Message.EscapeMarkup()}");
            }
            return 1;
        }

        // Display status
        switch (format)
        {
            case Core.Enums.OutputFormat.Json:
                DisplaySyncJson(config, remoteUrl!, syncStatus);
                break;
            case Core.Enums.OutputFormat.Simple:
                DisplaySyncSimple(config, remoteUrl!, syncStatus);
                break;
            case Core.Enums.OutputFormat.Table:
            default:
                DisplaySyncTable(config, remoteUrl!, syncStatus);
                break;
        }

        return 0;
    }

    private int DisplayAccountStatus(CloudConfig config, string projectDirectory, Core.Enums.OutputFormat format, CancellationToken cancellationToken)
    {
        var host = config.Host;

        // Check if logged in
        if (!config.IsLoggedIn)
        {
            if (format == Core.Enums.OutputFormat.Json)
            {
                Console.WriteLine(OutputFormatter.FormatJson(new
                {
                    authenticated = false,
                    error = "Not logged in"
                }));
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Not logged in[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud login <host>' to authenticate[/]");
                AnsiConsole.MarkupLine("[dim]Example: lrm cloud login lrm.cloud[/]");
            }
            return 0;
        }

        // Create API client
        var isLocalhost = host?.Contains("localhost") ?? false;
        var remoteUrl = new RemoteUrl
        {
            Host = host ?? "lrm.cloud",
            UseHttps = !isLocalhost,
            Port = config.Port ?? (isLocalhost ? 3000 : 443),
            ProjectName = "_account_"
        };

        using var apiClient = new CloudApiClient(remoteUrl);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
        }

        // Fetch user info
        UserProfile? userProfile = null;
        List<CloudProject>? projects = null;
        List<CloudOrganization>? organizations = null;
        string? errorMessage = null;

        try
        {
            userProfile = apiClient.GetCurrentUserAsync(cancellationToken).GetAwaiter().GetResult();
            projects = apiClient.GetUserProjectsAsync(cancellationToken).GetAwaiter().GetResult();
            organizations = apiClient.GetUserOrganizationsAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (CloudApiException ex)
        {
            errorMessage = ex.Message;
        }

        if (userProfile == null)
        {
            if (format == Core.Enums.OutputFormat.Json)
            {
                Console.WriteLine(OutputFormatter.FormatJson(new
                {
                    host,
                    authenticated = true,
                    error = errorMessage ?? "Failed to retrieve user profile"
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to retrieve account info:[/] {(errorMessage ?? "Unknown error").EscapeMarkup()}");
            }
            return 1;
        }

        // Display
        switch (format)
        {
            case Core.Enums.OutputFormat.Json:
                DisplayAccountJson(host ?? "unknown", config.HasProject, userProfile, projects, organizations);
                break;
            case Core.Enums.OutputFormat.Simple:
                DisplayAccountSimple(host ?? "unknown", config.HasProject, userProfile, projects, organizations);
                break;
            case Core.Enums.OutputFormat.Table:
            default:
                DisplayAccountTable(host ?? "unknown", config.HasProject, userProfile, projects, organizations);
                break;
        }

        return 0;
    }

    private void DisplaySyncTable(CloudConfig config, RemoteUrl remoteUrl, SyncStatus syncStatus)
    {
        AnsiConsole.MarkupLine("[blue bold]Cloud Sync Status[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Remote", config.Remote?.EscapeMarkup() ?? "");
        table.AddRow("Project", remoteUrl.ProjectName);

        var syncColor = syncStatus.IsSynced ? "green" : "yellow";
        var syncText = syncStatus.IsSynced ? "In sync" : "Out of sync";
        table.AddRow("Sync", $"[{syncColor}]{syncText}[/]");

        if (syncStatus.LastPush.HasValue)
            table.AddRow("Last Push", FormatDateTime(syncStatus.LastPush.Value));

        if (syncStatus.LastPull.HasValue)
            table.AddRow("Last Pull", FormatDateTime(syncStatus.LastPull.Value));

        if (syncStatus.LocalChanges > 0)
            table.AddRow("Local Changes", $"[yellow]{syncStatus.LocalChanges}[/]");

        if (syncStatus.RemoteChanges > 0)
            table.AddRow("Remote Changes", $"[yellow]{syncStatus.RemoteChanges}[/]");

        AnsiConsole.Write(table);

        if (!syncStatus.IsSynced)
        {
            AnsiConsole.WriteLine();
            if (syncStatus.LocalChanges > 0)
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud push' to push local changes[/]");
            if (syncStatus.RemoteChanges > 0)
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud pull' to pull remote changes[/]");
        }
    }

    private void DisplaySyncSimple(CloudConfig config, RemoteUrl remoteUrl, SyncStatus syncStatus)
    {
        Console.WriteLine($"Remote: {config.Remote}");
        Console.WriteLine($"Project: {remoteUrl.ProjectName}");
        Console.WriteLine($"Sync: {(syncStatus.IsSynced ? "In sync" : "Out of sync")}");

        if (syncStatus.LastPush.HasValue)
            Console.WriteLine($"Last Push: {FormatDateTime(syncStatus.LastPush.Value)}");

        if (syncStatus.LastPull.HasValue)
            Console.WriteLine($"Last Pull: {FormatDateTime(syncStatus.LastPull.Value)}");

        if (syncStatus.LocalChanges > 0)
            Console.WriteLine($"Local Changes: {syncStatus.LocalChanges}");

        if (syncStatus.RemoteChanges > 0)
            Console.WriteLine($"Remote Changes: {syncStatus.RemoteChanges}");
    }

    private void DisplaySyncJson(CloudConfig config, RemoteUrl remoteUrl, SyncStatus syncStatus)
    {
        Console.WriteLine(OutputFormatter.FormatJson(new
        {
            configured = true,
            authenticated = true,
            remote = config.Remote,
            project = remoteUrl.ProjectName,
            synced = syncStatus.IsSynced,
            lastPush = syncStatus.LastPush,
            lastPull = syncStatus.LastPull,
            localChanges = syncStatus.LocalChanges,
            remoteChanges = syncStatus.RemoteChanges
        }));
    }

    private void DisplayAccountTable(string host, bool hasProject, UserProfile user, List<CloudProject>? projects, List<CloudOrganization>? organizations)
    {
        AnsiConsole.MarkupLine("[blue bold]Cloud Account Status[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[green]Connected to {host.EscapeMarkup()}[/]");
        AnsiConsole.WriteLine();

        // User Info
        AnsiConsole.MarkupLine("[bold]User Info[/]");
        var userTable = new Table();
        userTable.Border = TableBorder.Rounded;
        userTable.AddColumn("Property");
        userTable.AddColumn("Value");

        userTable.AddRow("Email", user.Email.EscapeMarkup());
        userTable.AddRow("Username", user.Username.EscapeMarkup());
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            userTable.AddRow("Display Name", user.DisplayName.EscapeMarkup());

        var planColor = user.Plan == "free" ? "yellow" : "green";
        userTable.AddRow("Plan", $"[{planColor}]{user.Plan.EscapeMarkup()}[/]");

        var usagePercent = user.TranslationCharsLimit > 0
            ? (user.TranslationCharsUsed * 100) / user.TranslationCharsLimit
            : 0;
        var usageColor = usagePercent > 90 ? "red" : usagePercent > 70 ? "yellow" : "green";
        var usageText = user.TranslationCharsLimit > 0
            ? $"{user.TranslationCharsUsed:N0} / {user.TranslationCharsLimit:N0} chars"
            : $"{user.TranslationCharsUsed:N0} chars";
        userTable.AddRow("Translation Usage", $"[{usageColor}]{usageText}[/]");

        AnsiConsole.Write(userTable);
        AnsiConsole.WriteLine();

        // Projects
        if (projects != null && projects.Count > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Your Projects ({projects.Count})[/]");
            var projectTable = new Table();
            projectTable.Border = TableBorder.Rounded;
            projectTable.AddColumn("Name");
            projectTable.AddColumn("Owner");
            projectTable.AddColumn("Format");

            foreach (var project in projects)
            {
                var owner = string.IsNullOrWhiteSpace(project.OrganizationName)
                    ? $"@{user.Username}"
                    : project.OrganizationName;
                projectTable.AddRow(
                    project.Name.EscapeMarkup(),
                    owner.EscapeMarkup(),
                    project.Format.EscapeMarkup());
            }

            AnsiConsole.Write(projectTable);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No projects yet[/]");
            AnsiConsole.WriteLine();
        }

        // Organizations
        if (organizations != null && organizations.Count > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Your Organizations ({organizations.Count})[/]");
            var orgTable = new Table();
            orgTable.Border = TableBorder.Rounded;
            orgTable.AddColumn("Name");
            orgTable.AddColumn("Role");
            orgTable.AddColumn("Members");

            foreach (var org in organizations)
            {
                var roleColor = org.UserRole == "owner" ? "green" : org.UserRole == "admin" ? "blue" : "dim";
                orgTable.AddRow(
                    org.Name.EscapeMarkup(),
                    $"[{roleColor}]{org.UserRole.EscapeMarkup()}[/]",
                    org.MemberCount.ToString());
            }

            AnsiConsole.Write(orgTable);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No organizations[/]");
            AnsiConsole.WriteLine();
        }

        // Hint
        if (!hasProject)
        {
            AnsiConsole.MarkupLine("[dim]Use 'lrm cloud init' to connect this folder to a project.[/]");
        }
    }

    private void DisplayAccountSimple(string host, bool hasProject, UserProfile user, List<CloudProject>? projects, List<CloudOrganization>? organizations)
    {
        Console.WriteLine($"Host: {host}");
        Console.WriteLine($"Email: {user.Email}");
        Console.WriteLine($"Username: {user.Username}");
        Console.WriteLine($"Plan: {user.Plan}");
        Console.WriteLine($"Translation Usage: {user.TranslationCharsUsed:N0} / {user.TranslationCharsLimit:N0}");

        if (projects != null && projects.Count > 0)
        {
            Console.WriteLine($"Projects: {projects.Count}");
            foreach (var project in projects)
                Console.WriteLine($"  - {project.Name} ({project.Format})");
        }

        if (organizations != null && organizations.Count > 0)
        {
            Console.WriteLine($"Organizations: {organizations.Count}");
            foreach (var org in organizations)
                Console.WriteLine($"  - {org.Name} ({org.UserRole})");
        }
    }

    private void DisplayAccountJson(string host, bool hasProject, UserProfile user, List<CloudProject>? projects, List<CloudOrganization>? organizations)
    {
        Console.WriteLine(OutputFormatter.FormatJson(new
        {
            host,
            authenticated = true,
            linked = hasProject,
            user = new
            {
                id = user.Id,
                email = user.Email,
                username = user.Username,
                displayName = user.DisplayName,
                plan = user.Plan,
                translationUsage = new
                {
                    used = user.TranslationCharsUsed,
                    limit = user.TranslationCharsLimit,
                    resetAt = user.TranslationCharsResetAt
                },
                createdAt = user.CreatedAt
            },
            projects = projects?.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                organization = p.OrganizationName,
                format = p.Format,
                defaultLanguage = p.DefaultLanguage
            }),
            organizations = organizations?.Select(o => new
            {
                id = o.Id,
                name = o.Name,
                slug = o.Slug,
                role = o.UserRole,
                memberCount = o.MemberCount
            })
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
