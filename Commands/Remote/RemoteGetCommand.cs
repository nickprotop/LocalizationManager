// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Remote;

/// <summary>
/// Settings for the remote get command.
/// </summary>
public class RemoteGetCommandSettings : BaseFormattableCommandSettings
{
}

/// <summary>
/// Command to get the current remote URL configuration.
/// </summary>
public class RemoteGetCommand : Command<RemoteGetCommandSettings>
{
    public override int Execute(CommandContext context, RemoteGetCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();
            var format = settings.GetOutputFormat();

            // Load remotes configuration
            var remotesConfig = Core.Configuration.ConfigurationManager.LoadRemotesConfigurationAsync(projectDirectory).GetAwaiter().GetResult();

            // Check if remote is configured
            if (string.IsNullOrWhiteSpace(remotesConfig.Remote))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new { remote = (string?)null, enabled = false, configured = false }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No remote URL configured[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url>' to configure a remote URL[/]");
                }
                return 1;
            }

            // Parse the remote URL
            if (!RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        remote = remotesConfig.Remote,
                        enabled = remotesConfig.Enabled,
                        configured = true,
                        error = "Invalid URL format"
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {remotesConfig.Remote.EscapeMarkup()}");
                }
                return 1;
            }

            // Display based on format
            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    DisplayJson(remoteUrl!, remotesConfig);
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplaySimple(remoteUrl!, remotesConfig);
                    break;
                case Core.Enums.OutputFormat.Table:
                default:
                    DisplayTable(remoteUrl!, remotesConfig);
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

    private void DisplayTable(RemoteUrl remoteUrl, RemotesConfiguration remotesConfig)
    {
        AnsiConsole.MarkupLine("[blue]Remote Configuration:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Remote URL", remoteUrl.ToString().EscapeMarkup());
        table.AddRow("Type", remoteUrl.IsPersonalProject
            ? $"Personal (@{remoteUrl.Username})"
            : $"Organization ({remoteUrl.Organization})");
        table.AddRow("Project", remoteUrl.ProjectName);
        table.AddRow("Host", remoteUrl.Host);
        table.AddRow("Protocol", remoteUrl.UseHttps ? "HTTPS" : "HTTP");
        if (!IsDefaultPort(remoteUrl))
        {
            table.AddRow("Port", remoteUrl.Port.ToString());
        }
        table.AddRow("API Base URL", remoteUrl.ApiBaseUrl.EscapeMarkup());
        table.AddRow("Project API URL", remoteUrl.ProjectApiUrl.EscapeMarkup());

        var statusColor = remotesConfig.Enabled ? "green" : "yellow";
        var statusText = remotesConfig.Enabled ? "Enabled" : "Disabled";
        table.AddRow("Status", $"[{statusColor}]{statusText}[/]");

        AnsiConsole.Write(table);

        if (!remotesConfig.Enabled)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]💡 Use 'lrm remote set <url> --enable' to enable cloud synchronization[/]");
        }

        // Warn if using HTTP for non-localhost
        if (!remoteUrl.UseHttps && !IsLocalhost(remoteUrl.Host))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]⚠ Warning: Using HTTP instead of HTTPS for non-localhost connection![/]");
        }
    }

    private void DisplaySimple(RemoteUrl remoteUrl, RemotesConfiguration remotesConfig)
    {
        Console.WriteLine($"Remote URL: {remoteUrl}");
        Console.WriteLine($"Type: {(remoteUrl.IsPersonalProject ? $"Personal (@{remoteUrl.Username})" : $"Organization ({remoteUrl.Organization})")}");
        Console.WriteLine($"Project: {remoteUrl.ProjectName}");
        Console.WriteLine($"Host: {remoteUrl.Host}");
        Console.WriteLine($"Protocol: {(remoteUrl.UseHttps ? "HTTPS" : "HTTP")}");
        if (!IsDefaultPort(remoteUrl))
        {
            Console.WriteLine($"Port: {remoteUrl.Port}");
        }
        Console.WriteLine($"API Base URL: {remoteUrl.ApiBaseUrl}");
        Console.WriteLine($"Project API URL: {remoteUrl.ProjectApiUrl}");
        Console.WriteLine($"Status: {(remotesConfig.Enabled ? "Enabled" : "Disabled")}");
    }

    private void DisplayJson(RemoteUrl remoteUrl, RemotesConfiguration remotesConfig)
    {
        var output = new
        {
            remote = remoteUrl.ToString(),
            type = remoteUrl.IsPersonalProject ? "personal" : "organization",
            owner = remoteUrl.IsPersonalProject ? remoteUrl.Username : remoteUrl.Organization,
            project = remoteUrl.ProjectName,
            host = remoteUrl.Host,
            port = remoteUrl.Port,
            protocol = remoteUrl.UseHttps ? "https" : "http",
            apiBaseUrl = remoteUrl.ApiBaseUrl,
            projectApiUrl = remoteUrl.ProjectApiUrl,
            enabled = remotesConfig.Enabled,
            configured = true
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private static bool IsDefaultPort(RemoteUrl remoteUrl)
    {
        return (remoteUrl.UseHttps && remoteUrl.Port == 443) || (!remoteUrl.UseHttps && remoteUrl.Port == 80);
    }

    private static bool IsLocalhost(string host)
    {
        var lower = host.ToLowerInvariant();
        return lower == "localhost" || lower == "127.0.0.1" || lower == "::1";
    }
}
