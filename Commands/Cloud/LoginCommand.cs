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
    [CommandOption("--email <EMAIL>")]
    [Description("Email address for authentication")]
    public string? Email { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Password (not recommended - will prompt if not provided)")]
    public string? Password { get; set; }

    [CommandOption("--host <HOST>")]
    [Description("Remote host (e.g., lrm.cloud). Auto-detected from remote URL if configured")]
    public string? Host { get; set; }
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

            // 1. Determine host
            var (host, port, useHttps) = DetermineHost(projectDirectory, settings.Host, cancellationToken);

            // 2. Get credentials (prompt if not provided)
            string email = GetEmail(settings.Email);
            string password = GetPassword(settings.Password);

            // 3. Validate inputs
            if (!IsValidEmail(email))
            {
                AnsiConsole.MarkupLine("[red]✗ Invalid email address[/]");
                return 1;
            }

            // 4. Create API client and authenticate
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
                AnsiConsole.MarkupLine("[red]✗ Authentication failed[/]");
                return 1;
            }

            // 5. Save authentication tokens
            SaveAuthenticationTokens(projectDirectory, host, response, cancellationToken);

            // 6. Display success message
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
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private (string host, int port, bool useHttps) DetermineHost(string projectDirectory, string? providedHost, CancellationToken cancellationToken)
    {
        // If host provided via CLI, use it
        if (!string.IsNullOrWhiteSpace(providedHost))
        {
            // Parse port from host if present (e.g., "localhost:3000")
            if (providedHost.Contains(':'))
            {
                var parts = providedHost.Split(':');
                var host = parts[0];
                var port = int.Parse(parts[1]);
                var useHttps = !host.Contains("localhost") && !host.Contains("127.0.0.1");
                return (host, port, useHttps);
            }

            var https = !providedHost.Contains("localhost") && !providedHost.Contains("127.0.0.1");
            return (providedHost, https ? 443 : 80, https);
        }

        // Try to load from remotes configuration
        try
        {
            var remotesConfig = Core.Configuration.ConfigurationManager
                .LoadRemotesConfigurationAsync(projectDirectory, cancellationToken)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrWhiteSpace(remotesConfig.Remote))
            {
                if (RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
                {
                    AnsiConsole.MarkupLine($"[dim]Using host from configured remote: {remoteUrl!.Host}:{remoteUrl.Port}[/]");
                    return (remoteUrl.Host, remoteUrl.Port, remoteUrl.UseHttps);
                }
            }
        }
        catch
        {
            // Ignore errors loading remotes config
        }

        // Prompt for host
        var hostInput = AnsiConsole.Ask<string>("Cloud host:", "lrm.cloud");
        if (hostInput.Contains(':'))
        {
            var parts = hostInput.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            var useHttps = !host.Contains("localhost") && !host.Contains("127.0.0.1");
            return (host, port, useHttps);
        }

        var defaultHttps = !hostInput.Contains("localhost") && !hostInput.Contains("127.0.0.1");
        return (hostInput, defaultHttps ? 443 : 80, defaultHttps);
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
        // Use dummy organization/project since they're not needed for auth
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

    private void SaveAuthenticationTokens(
        string projectDirectory,
        string host,
        LoginResponse response,
        CancellationToken cancellationToken)
    {
        AuthTokenManager.SetAuthenticationAsync(
            projectDirectory,
            host,
            response.Token,
            response.ExpiresAt,
            response.RefreshToken,
            response.RefreshTokenExpiresAt,
            cancellationToken
        ).GetAwaiter().GetResult();
    }

    private void DisplaySuccessMessage(UserInfo user, string host)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Successfully authenticated![/]");
        AnsiConsole.WriteLine();

        var displayName = !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : user.Username;
        AnsiConsole.MarkupLine($"[dim]Logged in as:[/] {displayName.EscapeMarkup()} ({user.Email.EscapeMarkup()})");
        AnsiConsole.MarkupLine($"[dim]Host:[/] {host.EscapeMarkup()}");
        AnsiConsole.MarkupLine("[dim]Tokens stored in .lrm/auth.json[/]");

        if (!user.EmailVerified)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]⚠ Your email is not verified. Please check your inbox.[/]");
        }
    }

    private int HandleCloudError(CloudApiException ex)
    {
        AnsiConsole.WriteLine();

        switch (ex.StatusCode)
        {
            case 401:
                AnsiConsole.MarkupLine("[red]✗ Invalid email or password[/]");
                break;

            case 403:
                if (ex.Message.ToLower().Contains("verif") || ex.Message.ToLower().Contains("email"))
                {
                    AnsiConsole.MarkupLine("[red]✗ Email not verified[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Please check your inbox and click the verification link.[/]");
                    AnsiConsole.MarkupLine("[dim]Didn't receive the email? Contact support or check spam folder.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Access denied: {ex.Message.EscapeMarkup()}[/]");
                }
                break;

            case 429:
                AnsiConsole.MarkupLine("[red]✗ Too many login attempts[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Please try again later.[/]");
                break;

            case 500:
            case 502:
            case 503:
                AnsiConsole.MarkupLine("[red]✗ Server error[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]The authentication server is experiencing issues. Please try again later.[/]");
                break;

            default:
                if (ex.Message.Contains("Unable to connect") || ex.Message.Contains("connection"))
                {
                    AnsiConsole.MarkupLine("[red]✗ Unable to connect to authentication server[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Please check your internet connection and try again.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Authentication failed: {ex.Message.EscapeMarkup()}[/]");
                }
                break;
        }

        return 1;
    }
}
