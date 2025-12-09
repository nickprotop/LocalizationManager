// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using LrmConfigurationManager = LocalizationManager.Core.Configuration.ConfigurationManager;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the cloud init command.
/// </summary>
public class CloudInitCommandSettings : BaseCommandSettings
{
    [CommandOption("--host <HOST>")]
    [Description("Cloud host (default: lrm.cloud)")]
    public string? Host { get; set; }

    [CommandOption("-n|--name <NAME>")]
    [Description("Project name (skip selection if matches existing project)")]
    public string? ProjectName { get; set; }

    [CommandOption("--organization <ORG>")]
    [Description("Organization slug for new project")]
    public string? Organization { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts")]
    [DefaultValue(false)]
    public bool SkipConfirmation { get; set; }
}

/// <summary>
/// Command to initialize a cloud connection - authenticate and link/create a project.
/// </summary>
public class CloudInitCommand : Command<CloudInitCommandSettings>
{
    private const string CreateNewProjectOption = "[+] Create new project";

    private static readonly Regex EmailRegex = new Regex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override int Execute(CommandContext context, CloudInitCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            AnsiConsole.MarkupLine("[blue]Initializing cloud connection...[/]");
            AnsiConsole.WriteLine();

            // 1. Determine host
            var (host, port, useHttps) = DetermineHost(projectDirectory, settings.Host, cancellationToken);

            // 2. Check/trigger authentication
            var (authenticated, user) = EnsureAuthenticated(projectDirectory, host, port, useHttps, cancellationToken);
            if (!authenticated || user == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Authentication required to initialize cloud connection[/]");
                return 1;
            }

            // 3. Fetch user's projects
            var projects = FetchUserProjects(host, port, useHttps, projectDirectory, cancellationToken);

            // 4. Select or create project
            CloudProject? selectedProject = null;
            string remoteUrl;

            if (!string.IsNullOrWhiteSpace(settings.ProjectName))
            {
                // Try to find existing project by name
                selectedProject = projects.FirstOrDefault(p =>
                    p.Name.Equals(settings.ProjectName, StringComparison.OrdinalIgnoreCase));

                if (selectedProject == null)
                {
                    // Create new project with provided name
                    var (format, defaultLanguage) = DetectProjectSettings(projectDirectory, cancellationToken);
                    selectedProject = CreateNewProject(host, port, useHttps, projectDirectory,
                        settings.ProjectName, null, settings.Organization, format, defaultLanguage, cancellationToken);

                    if (selectedProject == null)
                        return 1;
                }
            }
            else if (projects.Count == 0)
            {
                // No existing projects, must create new
                AnsiConsole.MarkupLine("[dim]No existing projects found.[/]");
                selectedProject = PromptCreateNewProject(host, port, useHttps, projectDirectory, user.Username, cancellationToken);

                if (selectedProject == null)
                    return 1;
            }
            else
            {
                // Interactive selection
                selectedProject = PromptSelectOrCreateProject(projects, host, port, useHttps, projectDirectory, user.Username, cancellationToken);

                if (selectedProject == null)
                    return 0; // User cancelled
            }

            // 5. Build remote URL
            remoteUrl = BuildRemoteUrl(host, port, useHttps, selectedProject, user.Username);

            // 6. Save remote configuration
            SaveRemoteConfiguration(projectDirectory, remoteUrl, cancellationToken);

            // 7. Display success
            DisplaySuccess(selectedProject.Name, remoteUrl);

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
            return ParseHost(providedHost);
        }

        // Try to load from remotes configuration
        try
        {
            var remotesConfig = LrmConfigurationManager
                .LoadRemotesConfigurationAsync(projectDirectory, cancellationToken)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrWhiteSpace(remotesConfig.Remote))
            {
                if (RemoteUrlParser.TryParse(remotesConfig.Remote, out var remoteUrl))
                {
                    AnsiConsole.MarkupLine($"[dim]Using host from configured remote: {remoteUrl!.Host}[/]");
                    return (remoteUrl.Host, remoteUrl.Port, remoteUrl.UseHttps);
                }
            }
        }
        catch
        {
            // Ignore errors loading remotes config
        }

        // Default to lrm.cloud
        return ("lrm.cloud", 443, true);
    }

    private (string host, int port, bool useHttps) ParseHost(string hostInput)
    {
        if (hostInput.Contains(':'))
        {
            var parts = hostInput.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            var useHttps = !IsLocalhost(host);
            return (host, port, useHttps);
        }

        var defaultHttps = !IsLocalhost(hostInput);
        return (hostInput, defaultHttps ? 443 : 80, defaultHttps);
    }

    private bool IsLocalhost(string host)
    {
        return host.Contains("localhost") || host.Contains("127.0.0.1");
    }

    private (bool authenticated, UserInfo? user) EnsureAuthenticated(
        string projectDirectory, string host, int port, bool useHttps, CancellationToken cancellationToken)
    {
        // Check if already authenticated
        var existingToken = AuthTokenManager.GetTokenAsync(projectDirectory, host, cancellationToken)
            .GetAwaiter().GetResult();

        if (!string.IsNullOrWhiteSpace(existingToken))
        {
            // Validate token by trying to get user info (we'll get projects anyway)
            AnsiConsole.MarkupLine("[dim]Using existing authentication...[/]");

            // We need to get user info - for now, we'll get it during project fetch
            // Return a placeholder and get real user later
            return (true, new UserInfo { Username = "_placeholder" });
        }

        // Trigger login flow
        AnsiConsole.MarkupLine("[yellow]Not authenticated. Starting login...[/]");
        AnsiConsole.WriteLine();

        var email = AnsiConsole.Ask<string>("Email:");
        if (!IsValidEmail(email))
        {
            AnsiConsole.MarkupLine("[red]✗ Invalid email address[/]");
            return (false, null);
        }

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Password:")
                .Secret());

        // Create API client and authenticate
        var remoteUrl = CreateAuthRemoteUrl(host, port, useHttps);
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
            return (false, null);
        }

        // Save tokens
        AuthTokenManager.SetAuthenticationAsync(
            projectDirectory, host, response.Token, response.ExpiresAt,
            response.RefreshToken, response.RefreshTokenExpiresAt, cancellationToken)
            .GetAwaiter().GetResult();

        AnsiConsole.MarkupLine($"[green]✓ Logged in as {response.User.Email.EscapeMarkup()}[/]");
        AnsiConsole.WriteLine();

        return (true, response.User);
    }

    private bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);
    }

    private RemoteUrl CreateAuthRemoteUrl(string host, int port, bool useHttps)
    {
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

    private List<CloudProject> FetchUserProjects(
        string host, int port, bool useHttps, string projectDirectory, CancellationToken cancellationToken)
    {
        var remoteUrl = CreateAuthRemoteUrl(host, port, useHttps);
        var token = AuthTokenManager.GetTokenAsync(projectDirectory, host, cancellationToken)
            .GetAwaiter().GetResult();

        List<CloudProject> projects = new();

        AnsiConsole.Status()
            .Start("Fetching your projects...", ctx =>
            {
                using var apiClient = new CloudApiClient(remoteUrl);
                apiClient.SetAccessToken(token);
                apiClient.EnableAutoRefresh(projectDirectory);
                projects = apiClient.GetUserProjectsAsync(cancellationToken).GetAwaiter().GetResult();
            });

        return projects;
    }

    private CloudProject? PromptSelectOrCreateProject(
        List<CloudProject> projects, string host, int port, bool useHttps,
        string projectDirectory, string username, CancellationToken cancellationToken)
    {
        // First ask: link existing or create new
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices("Link to existing project", "Create new project"));

        if (action == "Create new project")
        {
            return PromptCreateNewProject(host, port, useHttps, projectDirectory, username, cancellationToken);
        }

        // Show project selection with format info
        var projectChoices = projects.Select(p =>
        {
            var owner = p.OrganizationName ?? $"@{username}";
            var format = !string.IsNullOrEmpty(p.Format) ? $" [{p.Format}]" : "";
            return $"{p.Name} ({owner}/{p.Name}){format}";
        }).ToList();

        var selectedChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a project:")
                .PageSize(10)
                .AddChoices(projectChoices));

        // Find the selected project
        var selectedIndex = projectChoices.IndexOf(selectedChoice);
        var selectedProject = projects[selectedIndex];

        // Validate format compatibility
        var syncValidator = new CloudSyncValidator(projectDirectory);
        var validation = syncValidator.ValidateForLink(selectedProject);

        if (validation.Warnings.Any())
        {
            foreach (var warning in validation.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ {warning.EscapeMarkup()}[/]");
            }
        }

        if (!validation.CanSync)
        {
            AnsiConsole.MarkupLine("[red]✗ Cannot link to this project:[/]");
            foreach (var error in validation.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]• {error.EscapeMarkup()}[/]");
            }
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("Link anyway? (sync operations will fail)", false))
            {
                return null;
            }
        }

        return selectedProject;
    }

    private CloudProject? PromptCreateNewProject(
        string host, int port, bool useHttps, string projectDirectory,
        string username, CancellationToken cancellationToken)
    {
        var name = AnsiConsole.Ask<string>("Project name:");
        var description = AnsiConsole.Prompt(
            new TextPrompt<string>("Description (optional):")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(description))
            description = null;

        // Detect format from local files or lrm.json
        var (format, defaultLanguage) = DetectProjectSettings(projectDirectory, cancellationToken);

        AnsiConsole.MarkupLine($"[dim]Detected format: {format}, default language: {defaultLanguage}[/]");

        // For now, create as personal project
        // TODO: Add organization selection if user belongs to organizations
        return CreateNewProject(host, port, useHttps, projectDirectory, name, description, null, format, defaultLanguage, cancellationToken);
    }

    private (string format, string defaultLanguage) DetectProjectSettings(string projectDirectory, CancellationToken cancellationToken)
    {
        var format = "json"; // Default
        var defaultLanguage = "en"; // Default

        // Try to load from lrm.json first
        try
        {
            var config = LrmConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(config.ResourceFormat))
            {
                format = config.ResourceFormat.ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(config.DefaultLanguageCode))
            {
                defaultLanguage = config.DefaultLanguageCode;
            }
        }
        catch
        {
            // Config doesn't exist, try to detect from files
        }

        // If format still not set, try to detect from files
        if (format == "json")
        {
            var syncValidator = new CloudSyncValidator(projectDirectory);
            var detectedFormat = syncValidator.DetectLocalFormat();
            if (detectedFormat != null)
            {
                format = detectedFormat;
            }
        }

        return (format, defaultLanguage);
    }

    private CloudProject? CreateNewProject(
        string host, int port, bool useHttps, string projectDirectory,
        string name, string? description, string? organization,
        string format, string defaultLanguage, CancellationToken cancellationToken)
    {
        var remoteUrl = CreateAuthRemoteUrl(host, port, useHttps);
        var token = AuthTokenManager.GetTokenAsync(projectDirectory, host, cancellationToken)
            .GetAwaiter().GetResult();

        CloudProject? project = null;

        AnsiConsole.Status()
            .Start($"Creating project '{name}'...", ctx =>
            {
                using var apiClient = new CloudApiClient(remoteUrl);
                apiClient.SetAccessToken(token);
                apiClient.EnableAutoRefresh(projectDirectory);

                var request = new CreateProjectRequest
                {
                    Name = name,
                    Description = description,
                    Format = format,
                    DefaultLanguage = defaultLanguage
                };

                project = apiClient.CreateProjectAsync(request, cancellationToken).GetAwaiter().GetResult();
            });

        if (project == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Failed to create project[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"[green]✓ Project '{name.EscapeMarkup()}' created (format: {format})[/]");
        return project;
    }

    private string BuildRemoteUrl(string host, int port, bool useHttps, CloudProject project, string username)
    {
        var protocol = useHttps ? "https" : "http";
        var portSuffix = (useHttps && port == 443) || (!useHttps && port == 80) ? "" : $":{port}";

        // Use organization name if it's an org project, otherwise use @username
        var owner = project.OrganizationName ?? $"@{username}";

        return $"{protocol}://{host}{portSuffix}/{owner}/{project.Name}";
    }

    private void SaveRemoteConfiguration(string projectDirectory, string remoteUrl, CancellationToken cancellationToken)
    {
        var remotesConfig = new RemotesConfiguration
        {
            Remote = remoteUrl,
            Enabled = true
        };

        LrmConfigurationManager.SaveRemotesConfigurationAsync(projectDirectory, remotesConfig, cancellationToken)
            .GetAwaiter().GetResult();

        // Ensure .lrm is in .gitignore
        LrmConfigurationManager.EnsureGitIgnoreAsync(projectDirectory, cancellationToken)
            .GetAwaiter().GetResult();
    }

    private void DisplaySuccess(string projectName, string remoteUrl)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green bold]✓ Cloud connection initialized![/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Project", projectName.EscapeMarkup());
        table.AddRow("Remote URL", remoteUrl.EscapeMarkup());
        table.AddRow("Config saved", ".lrm/remotes.json");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Next steps:[/]");
        AnsiConsole.MarkupLine("  [blue]1.[/] lrm cloud push     [grey]# Push your resources to the cloud[/]");
        AnsiConsole.MarkupLine("  [blue]2.[/] lrm cloud status   [grey]# Check sync status[/]");
    }

    private int HandleCloudError(CloudApiException ex)
    {
        AnsiConsole.WriteLine();

        switch (ex.StatusCode)
        {
            case 401:
                AnsiConsole.MarkupLine("[red]✗ Authentication failed[/]");
                AnsiConsole.MarkupLine("[dim]Invalid credentials or session expired.[/]");
                break;

            case 403:
                AnsiConsole.MarkupLine("[red]✗ Access denied[/]");
                AnsiConsole.MarkupLine("[dim]You don't have permission for this operation.[/]");
                break;

            case 409:
                AnsiConsole.MarkupLine("[red]✗ Project already exists[/]");
                AnsiConsole.MarkupLine("[dim]A project with this name already exists. Choose a different name.[/]");
                break;

            case 429:
                AnsiConsole.MarkupLine("[red]✗ Too many requests[/]");
                AnsiConsole.MarkupLine("[dim]Please wait a moment and try again.[/]");
                break;

            case 500:
            case 502:
            case 503:
                AnsiConsole.MarkupLine("[red]✗ Server error[/]");
                AnsiConsole.MarkupLine("[dim]The server is experiencing issues. Please try again later.[/]");
                break;

            default:
                if (ex.Message.Contains("Unable to connect") || ex.Message.Contains("connection"))
                {
                    AnsiConsole.MarkupLine("[red]✗ Unable to connect to cloud server[/]");
                    AnsiConsole.MarkupLine("[dim]Please check your internet connection and try again.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
                }
                break;
        }

        return 1;
    }
}
