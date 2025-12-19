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
    [CommandArgument(0, "[URL]")]
    [Description("Remote URL (e.g., https://lrm-cloud.com/org/project). Interactive if not provided.")]
    public string? Url { get; set; }

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

            // Load existing config
            var config = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(config.ApiKey))
            {
                config.ApiKey = envApiKey;
            }

            // If URL provided directly, use it
            if (!string.IsNullOrWhiteSpace(settings.Url))
            {
                return InitWithDirectUrl(projectDirectory, config, settings.Url, cancellationToken);
            }

            // Interactive flow
            return InitInteractive(projectDirectory, config, settings, cancellationToken);
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

    private int InitWithDirectUrl(string projectDirectory, CloudConfig config, string url, CancellationToken cancellationToken)
    {
        // Parse the URL
        if (!RemoteUrlParser.TryParse(url, out var remoteUrl))
        {
            AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {url.EscapeMarkup()}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Expected format: https://host/owner/project[/]");
            AnsiConsole.MarkupLine("[dim]Examples:[/]");
            AnsiConsole.MarkupLine("[dim]  https://lrm-cloud.com/org/project[/]");
            AnsiConsole.MarkupLine("[dim]  https://lrm-cloud.com/@username/project[/]");
            AnsiConsole.MarkupLine("[dim]  http://localhost:3000/org/project[/]");
            return 1;
        }

        // Check authentication
        if (!config.IsLoggedIn)
        {
            AnsiConsole.MarkupLine("[yellow]Not authenticated.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login {remoteUrl!.Host}' first, or set LRM_CLOUD_API_KEY env var.[/]");
            return 1;
        }

        // Set the remote
        config.Remote = url;
        CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

        // Ensure .lrm is in .gitignore
        LrmConfigurationManager.EnsureGitIgnoreAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

        DisplaySuccess(remoteUrl!.ProjectName, url);
        return 0;
    }

    private int InitInteractive(string projectDirectory, CloudConfig config, CloudInitCommandSettings settings, CancellationToken cancellationToken)
    {
        // 1. Determine host
        var (host, port, useHttps) = DetermineHost(config);

        // 2. Check/trigger authentication
        var (authenticated, user) = EnsureAuthenticated(projectDirectory, config, host, port, useHttps, cancellationToken);
        if (!authenticated || user == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Authentication required to initialize cloud connection[/]");
            return 1;
        }

        // 3. Fetch user's projects
        var projects = FetchUserProjects(projectDirectory, host, port, useHttps, config, cancellationToken);

        // 4. Select or create project
        CloudProject? selectedProject = null;

        if (!string.IsNullOrWhiteSpace(settings.ProjectName))
        {
            // Try to find existing project by name
            selectedProject = projects.FirstOrDefault(p =>
                p.Name.Equals(settings.ProjectName, StringComparison.OrdinalIgnoreCase));

            if (selectedProject == null)
            {
                // Create new project with provided name
                var (format, defaultLanguage) = DetectProjectSettings(projectDirectory, cancellationToken);
                selectedProject = CreateNewProject(host, port, useHttps, config, projectDirectory,
                    settings.ProjectName, null, settings.Organization, format, defaultLanguage, cancellationToken);

                if (selectedProject == null)
                    return 1;
            }
        }
        else if (projects.Count == 0)
        {
            // No existing projects, must create new
            AnsiConsole.MarkupLine("[dim]No existing projects found.[/]");
            selectedProject = PromptCreateNewProject(host, port, useHttps, config, projectDirectory, user.Username, cancellationToken);

            if (selectedProject == null)
                return 1;
        }
        else
        {
            // Interactive selection
            selectedProject = PromptSelectOrCreateProject(projects, host, port, useHttps, config, projectDirectory, user.Username, cancellationToken);

            if (selectedProject == null)
                return 0; // User cancelled
        }

        // 5. Build remote URL
        var remoteUrl = BuildRemoteUrl(host, port, useHttps, selectedProject, user.Username);

        // 6. Save configuration
        config.Remote = remoteUrl;
        CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

        // Ensure .lrm is in .gitignore
        LrmConfigurationManager.EnsureGitIgnoreAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

        // 7. Display success
        DisplaySuccess(selectedProject.Name, remoteUrl);

        return 0;
    }

    private (string host, int port, bool useHttps) DetermineHost(CloudConfig config)
    {
        // If already have a remote, extract host from it
        if (!string.IsNullOrWhiteSpace(config.Remote) && RemoteUrlParser.TryParse(config.Remote, out var remoteUrl))
        {
            AnsiConsole.MarkupLine($"[dim]Using host from configured remote: {remoteUrl!.Host}[/]");
            return (remoteUrl.Host, remoteUrl.Port, remoteUrl.UseHttps);
        }

        // If logged in with host-only remote, extract host
        if (!string.IsNullOrWhiteSpace(config.Host))
        {
            var isLocalhost = config.Host.Contains("localhost") || config.Host.Contains("127.0.0.1");
            var port = config.Port ?? (isLocalhost ? 3000 : 443);
            return (config.Host, port, config.UseHttps);
        }

        // Default to lrm-cloud.com
        return ("lrm-cloud.com", 443, true);
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
        string projectDirectory, CloudConfig config, string host, int port, bool useHttps, CancellationToken cancellationToken)
    {
        // Check if already authenticated (via API key or access token)
        if (config.IsLoggedIn)
        {
            AnsiConsole.MarkupLine("[dim]Using existing authentication...[/]");
            // Return a placeholder - we'll get real user info during project fetch
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

        // Save tokens to config
        var protocol = useHttps ? "https" : "http";
        var portSuffix = (useHttps && port == 443) || (!useHttps && port == 80) ? "" : $":{port}";
        config.Remote = $"{protocol}://{host}{portSuffix}"; // Host-only for now, will be replaced with full URL

        config.AccessToken = response.Token;
        config.ExpiresAt = response.ExpiresAt;
        config.RefreshToken = response.RefreshToken;
        config.RefreshTokenExpiresAt = response.RefreshTokenExpiresAt;

        CloudConfigManager.SaveAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();

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
        string projectDirectory, string host, int port, bool useHttps, CloudConfig config, CancellationToken cancellationToken)
    {
        var remoteUrl = CreateAuthRemoteUrl(host, port, useHttps);
        List<CloudProject> projects = new();

        AnsiConsole.Status()
            .Start("Fetching your projects...", ctx =>
            {
                using var apiClient = new CloudApiClient(remoteUrl);

                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    apiClient.SetApiKey(config.ApiKey);
                }
                else
                {
                    apiClient.SetAccessToken(config.AccessToken);
                    // Enable auto-refresh for JWT authentication
                    apiClient.EnableAutoRefresh(projectDirectory);
                }

                projects = apiClient.GetUserProjectsAsync(cancellationToken).GetAwaiter().GetResult();
            });

        return projects;
    }

    private CloudProject? PromptSelectOrCreateProject(
        List<CloudProject> projects, string host, int port, bool useHttps,
        CloudConfig config, string projectDirectory, string username, CancellationToken cancellationToken)
    {
        // First ask: link existing or create new
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices("Link to existing project", "Create new project"));

        if (action == "Create new project")
        {
            return PromptCreateNewProject(host, port, useHttps, config, projectDirectory, username, cancellationToken);
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
        string host, int port, bool useHttps, CloudConfig config,
        string projectDirectory, string username, CancellationToken cancellationToken)
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
        return CreateNewProject(host, port, useHttps, config, projectDirectory, name, description, null, format, defaultLanguage, cancellationToken);
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
        string host, int port, bool useHttps, CloudConfig config,
        string projectDirectory, string name, string? description, string? organization,
        string format, string defaultLanguage, CancellationToken cancellationToken)
    {
        var remoteUrl = CreateAuthRemoteUrl(host, port, useHttps);
        CloudProject? project = null;

        AnsiConsole.Status()
            .Start($"Creating project '{name}'...", ctx =>
            {
                using var apiClient = new CloudApiClient(remoteUrl);

                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    apiClient.SetApiKey(config.ApiKey);
                }
                else
                {
                    apiClient.SetAccessToken(config.AccessToken);
                    // Enable auto-refresh for JWT authentication
                    apiClient.EnableAutoRefresh(projectDirectory);
                }

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
        table.AddRow("Config saved", ".lrm/cloud.json");

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
