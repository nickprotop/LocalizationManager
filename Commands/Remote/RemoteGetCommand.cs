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

            // Load cloud configuration
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check if remote is configured
            if (string.IsNullOrWhiteSpace(config.Remote))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new { remote = (string?)null, configured = false }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No remote URL configured[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url>' or 'lrm cloud init' to configure a remote[/]");
                }
                return 1;
            }

            // Parse the remote URL
            if (!RemoteUrlParser.TryParse(config.Remote, out var remoteUrl))
            {
                if (format == Core.Enums.OutputFormat.Json)
                {
                    Console.WriteLine(OutputFormatter.FormatJson(new
                    {
                        remote = config.Remote,
                        configured = true,
                        error = "Invalid URL format"
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {config.Remote.EscapeMarkup()}");
                }
                return 1;
            }

            // Display based on format
            switch (format)
            {
                case Core.Enums.OutputFormat.Json:
                    DisplayJson(remoteUrl!, config);
                    break;
                case Core.Enums.OutputFormat.Simple:
                    DisplaySimple(remoteUrl!, config);
                    break;
                case Core.Enums.OutputFormat.Table:
                default:
                    DisplayTable(remoteUrl!, config);
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

    private void DisplayTable(RemoteUrl remoteUrl, CloudConfig config)
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

        var authColor = config.IsLoggedIn ? "green" : "yellow";
        var authText = config.IsLoggedIn ? "Authenticated" : "Not authenticated";
        table.AddRow("Auth Status", $"[{authColor}]{authText}[/]");

        AnsiConsole.Write(table);

        if (!config.IsLoggedIn)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login {remoteUrl.Host}' to authenticate[/]");
        }

        // Warn if using HTTP for non-localhost
        if (!remoteUrl.UseHttps && !IsLocalhost(remoteUrl.Host))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]⚠ Warning: Using HTTP instead of HTTPS for non-localhost connection![/]");
        }
    }

    private void DisplaySimple(RemoteUrl remoteUrl, CloudConfig config)
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
        Console.WriteLine($"Auth Status: {(config.IsLoggedIn ? "Authenticated" : "Not authenticated")}");
    }

    private void DisplayJson(RemoteUrl remoteUrl, CloudConfig config)
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
            authenticated = config.IsLoggedIn,
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
