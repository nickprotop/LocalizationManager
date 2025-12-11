// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the login command.
/// </summary>
public class LoginCommandSettings : BaseCommandSettings
{
    [CommandArgument(0, "[HOST]")]
    [Description("Cloud host (e.g., lrm.cloud, localhost:3000). Uses configured remote if not provided.")]
    public string? Host { get; set; }

    [CommandOption("--email <EMAIL>")]
    [Description("Email address for authentication")]
    public string? Email { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Password (not recommended - will prompt if not provided)")]
    public string? Password { get; set; }
}

/// <summary>
/// Command to authenticate with the cloud using email and password.
/// </summary>
public class LoginCommand : Command<LoginCommandSettings>
{
    private static readonly Regex EmailRegex = new Regex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override int Execute(CommandContext context, LoginCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            AnsiConsole.MarkupLine("[blue]Authenticating with cloud...[/]");
            AnsiConsole.WriteLine();

            // 1. Load existing config
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // 2. Determine host (from argument, existing remote, or error)
            var (host, port, useHttps) = DetermineHost(settings.Host, config);

            // 3. Get credentials (prompt if not provided)
            string email = GetEmail(settings.Email);
            string password = GetPassword(settings.Password);

            // 4. Validate inputs
            if (!IsValidEmail(email))
            {
                AnsiConsole.MarkupLine("[red]Invalid email address[/]");
                return 1;
            }

            // 5. Create API client and authenticate
            var remoteUrl = CreateRemoteUrl(host, port, useHttps);
            LoginResponse? response = null;

            AnsiConsole.Status()
                .Start("Authenticating...", ctx =>
                {
                    using var apiClient = new CloudApiClient(remoteUrl);
                    response = apiClient.LoginAsync(email, password, cancellationToken).GetAwaiter().GetResult();
                });

            if (response == null)
            {
                AnsiConsole.MarkupLine("[red]Authentication failed[/]");
                return 1;
            }

            // 6. Update config with remote (if not set) and tokens
            var remoteBaseUrl = BuildRemoteUrl(host, port, useHttps);

            // If no remote is set, or if it's just a host (no project), set the host-only remote
            if (!config.HasProject)
            {
                config.Remote = remoteBaseUrl;
            }

            // Set tokens
            config.AccessToken = response.Token;
            config.ExpiresAt = response.ExpiresAt;
            config.RefreshToken = response.RefreshToken;
            config.RefreshTokenExpiresAt = response.RefreshTokenExpiresAt;

            CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

            // 7. Display success message
            DisplaySuccessMessage(response.User, host);

            return 0;
        }
        catch (CloudApiException ex)
        {
            return HandleCloudError(ex);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private (string host, int port, bool useHttps) DetermineHost(string? providedHost, CloudConfig config)
    {
        // 1. If host provided as argument, use it
        if (!string.IsNullOrWhiteSpace(providedHost))
        {
            return ParseHost(providedHost);
        }

        // 2. If remote is configured, extract host from it
        if (!string.IsNullOrWhiteSpace(config.Remote))
        {
            var host = config.Host;
            var port = config.Port ?? (config.UseHttps ? 443 : 80);
            AnsiConsole.MarkupLine($"[dim]Using host from remote: {host}[/]");
            return (host!, port, config.UseHttps);
        }

        // 3. No host available - error
        AnsiConsole.MarkupLine("[red]No host specified and no remote configured.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Usage: lrm cloud login <host>[/]");
        AnsiConsole.MarkupLine("[dim]Example: lrm cloud login lrm.cloud[/]");
        AnsiConsole.MarkupLine("[dim]Example: lrm cloud login localhost:3000[/]");
        throw new InvalidOperationException("No host specified");
    }

    private (string host, int port, bool useHttps) ParseHost(string hostString)
    {
        // Handle full URL
        if (hostString.StartsWith("http://") || hostString.StartsWith("https://"))
        {
            var uri = new Uri(hostString);
            return (uri.Host, uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port, uri.Scheme == "https");
        }

        // Parse host:port format
        if (hostString.Contains(':'))
        {
            var parts = hostString.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            var useHttps = !host.Contains("localhost") && !host.Contains("127.0.0.1");
            return (host, port, useHttps);
        }

        // Just host
        var https = !hostString.Contains("localhost") && !hostString.Contains("127.0.0.1");
        return (hostString, https ? 443 : 80, https);
    }

    private string BuildRemoteUrl(string host, int port, bool useHttps)
    {
        var protocol = useHttps ? "https" : "http";
        var portSuffix = (useHttps && port == 443) || (!useHttps && port == 80) ? "" : $":{port}";
        return $"{protocol}://{host}{portSuffix}";
    }

    private string GetEmail(string? providedEmail)
    {
        if (!string.IsNullOrWhiteSpace(providedEmail))
        {
            return providedEmail;
        }

        return AnsiConsole.Ask<string>("Email:");
    }

    private string GetPassword(string? providedPassword)
    {
        if (!string.IsNullOrWhiteSpace(providedPassword))
        {
            return providedPassword;
        }

        return AnsiConsole.Prompt(
            new TextPrompt<string>("Password:")
                .Secret());
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    private RemoteUrl CreateRemoteUrl(string host, int port, bool useHttps)
    {
        // Create a minimal RemoteUrl for authentication
        var protocol = useHttps ? "https" : "http";
        var portSuffix = (useHttps && port == 443) || (!useHttps && port == 80) ? "" : $":{port}";

        return new RemoteUrl
        {
            Host = host,
            Port = port,
            UseHttps = useHttps,
            Organization = "_auth",
            ProjectName = "_auth",
            OriginalUrl = $"{protocol}://{host}{portSuffix}/_auth/_auth"
        };
    }

    private void DisplaySuccessMessage(UserInfo user, string host)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Successfully authenticated![/]");
        AnsiConsole.WriteLine();

        var displayName = !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : user.Username;
        AnsiConsole.MarkupLine($"[dim]Logged in as:[/] {displayName.EscapeMarkup()} ({user.Email.EscapeMarkup()})");
        AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");

        if (!user.EmailVerified)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Your email is not verified. Please check your inbox.[/]");
        }
    }

    private int HandleCloudError(CloudApiException ex)
    {
        AnsiConsole.WriteLine();

        switch (ex.StatusCode)
        {
            case 401:
                AnsiConsole.MarkupLine("[red]Invalid email or password[/]");
                break;

            case 403:
                if (ex.Message.ToLower().Contains("verif") || ex.Message.ToLower().Contains("email"))
                {
                    AnsiConsole.MarkupLine("[red]Email not verified[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Please check your inbox and click the verification link.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Access denied: {ex.Message.EscapeMarkup()}[/]");
                }
                break;

            case 429:
                AnsiConsole.MarkupLine("[red]Too many login attempts. Please try again later.[/]");
                break;

            case 500:
            case 502:
            case 503:
                AnsiConsole.MarkupLine("[red]Server error. Please try again later.[/]");
                break;

            default:
                if (ex.Message.Contains("Unable to connect") || ex.Message.Contains("connection"))
                {
                    AnsiConsole.MarkupLine("[red]Unable to connect to server. Check your internet connection.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Authentication failed: {ex.Message.EscapeMarkup()}[/]");
                }
                break;
        }

        return 1;
    }
}
