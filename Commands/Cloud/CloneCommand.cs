// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the clone command.
/// </summary>
public class CloneCommandSettings : CommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("Cloud project URL (e.g., https://lrm-cloud.com/@username/project or https://lrm-cloud.com/org/project)")]
    public string Url { get; set; } = string.Empty;

    [CommandArgument(1, "[path]")]
    [Description("Target directory (default: ./{project-slug})")]
    public string? Path { get; set; }

    [CommandOption("--email <EMAIL>")]
    [Description("Email for authentication")]
    public string? Email { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Password (not recommended - will prompt if not provided)")]
    public string? Password { get; set; }

    [CommandOption("--api-key <KEY>")]
    [Description("API key for authentication")]
    public string? ApiKey { get; set; }

    [CommandOption("--no-pull")]
    [Description("Don't pull resources after cloning")]
    [DefaultValue(false)]
    public bool NoPull { get; set; }

    [CommandOption("--force")]
    [Description("Skip confirmation prompts")]
    [DefaultValue(false)]
    public bool Force { get; set; }
}

/// <summary>
/// Command to clone an existing cloud project.
/// Combines login + remote set + pull into a single operation (like git clone).
/// </summary>
public class CloneCommand : Command<CloneCommandSettings>
{
    private static readonly Regex EmailRegex = new Regex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override int Execute(CommandContext context, CloneCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Parse and validate remote URL
            if (!RemoteUrlParser.TryParse(settings.Url, out var remoteUrl))
            {
                AnsiConsole.MarkupLine("[red]✗ Invalid remote URL format![/]");
                AnsiConsole.MarkupLine("[dim]Expected: https://host/org/project or https://host/@username/project[/]");
                AnsiConsole.MarkupLine("[dim]Examples:[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm-cloud.com/acme-corp/mobile-app[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm-cloud.com/@john/personal-project[/]");
                return 1;
            }

            // 2. Determine target directory
            var targetDirectory = DetermineTargetDirectory(settings.Path, remoteUrl!);
            var targetDirExists = Directory.Exists(targetDirectory);
            var hasLrmConfig = targetDirExists && Directory.Exists(System.IO.Path.Combine(targetDirectory, ".lrm"));

            // 3. Check if already linked to a project
            if (hasLrmConfig)
            {
                var existingConfig = CloudConfigManager.LoadAsync(targetDirectory, cancellationToken).GetAwaiter().GetResult();
                if (existingConfig.HasProject)
                {
                    AnsiConsole.MarkupLine("[red]✗ Directory already linked to a cloud project![/]");
                    AnsiConsole.MarkupLine($"[dim]  Existing remote: {existingConfig.Remote?.EscapeMarkup()}[/]");
                    AnsiConsole.MarkupLine($"[dim]  Target: {targetDirectory.EscapeMarkup()}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]To change the remote, use:[/]");
                    AnsiConsole.MarkupLine($"[dim]  lrm cloud remote set {settings.Url.EscapeMarkup()} --path {targetDirectory.EscapeMarkup()}[/]");
                    return 1;
                }
            }

            // 4. Create target directory if it doesn't exist
            if (!targetDirExists)
            {
                AnsiConsole.MarkupLine($"[dim]Creating directory {targetDirectory.EscapeMarkup()}[/]");
                Directory.CreateDirectory(targetDirectory);
            }

            // 5. Load or create cloud config
            var cloudConfig = CloudConfigManager.LoadAsync(targetDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();

            // 6. Handle authentication
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                // Use provided API key
                cloudConfig.ApiKey = settings.ApiKey;
                AnsiConsole.MarkupLine("[dim]Using provided API key[/]");
            }
            else if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                // Use environment API key
                cloudConfig.ApiKey = envApiKey;
                AnsiConsole.MarkupLine("[dim]Using API key from LRM_CLOUD_API_KEY environment variable[/]");
            }
            else if (!cloudConfig.IsLoggedIn)
            {
                // Need to authenticate
                AnsiConsole.MarkupLine($"[dim]Connecting to {remoteUrl.Host.EscapeMarkup()}...[/]");
                AnsiConsole.WriteLine();

                if (!Authenticate(remoteUrl, cloudConfig, settings, cancellationToken))
                {
                    // Cleanup: remove empty directory if we created it
                    CleanupEmptyDirectory(targetDirectory, targetDirExists);
                    return 1;
                }

                CloudConfigManager.SaveAsync(targetDirectory, cloudConfig, cancellationToken).GetAwaiter().GetResult();
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Using existing authentication[/]");
            }

            // 7. Fetch and validate remote project
            using var apiClient = new CloudApiClient(remoteUrl);
            ConfigureAuth(apiClient, cloudConfig);

            CloudProject? remoteProject = null;
            try
            {
                AnsiConsole.Status()
                    .Start("Fetching project info...", ctx =>
                    {
                        remoteProject = apiClient.GetProjectAsync(cancellationToken).GetAwaiter().GetResult();
                    });
            }
            catch (CloudApiException ex) when (ex.StatusCode == 404)
            {
                AnsiConsole.MarkupLine("[red]✗ Project not found![/]");
                AnsiConsole.MarkupLine($"[dim]  URL: {remoteUrl.ToString().EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]To create a new project, use:[/]");
                AnsiConsole.MarkupLine("[dim]  lrm cloud init[/]");
                CleanupEmptyDirectory(targetDirectory, targetDirExists);
                return 1;
            }
            catch (CloudApiException ex) when (ex.StatusCode == 401 || ex.StatusCode == 403)
            {
                AnsiConsole.MarkupLine("[red]✗ Access denied![/]");
                AnsiConsole.MarkupLine("[dim]You may not have permission to access this project.[/]");
                CleanupEmptyDirectory(targetDirectory, targetDirExists);
                return 1;
            }

            // 8. Display project info
            AnsiConsole.MarkupLine($"[green]✓ Found project:[/] {remoteProject!.Name.EscapeMarkup()}");
            AnsiConsole.MarkupLine($"[dim]  Format: {remoteProject.Format}[/]");
            AnsiConsole.MarkupLine($"[dim]  Default language: {remoteProject.DefaultLanguage}[/]");
            AnsiConsole.WriteLine();

            // 9. Check for existing resource files and validate compatibility
            var hasExistingFiles = HasResourceFiles(targetDirectory, remoteProject.Format);
            if (hasExistingFiles)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Target directory contains existing resource files[/]");

                // Validate compatibility
                var validator = new CloudSyncValidator(targetDirectory);
                var validationResult = validator.ValidateForLink(remoteProject);

                // Check local lrm.json config vs remote
                var localConfig = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(targetDirectory, cancellationToken).GetAwaiter().GetResult();
                if (localConfig != null)
                {
                    ValidateConfigCompatibility(localConfig, remoteProject, validationResult);
                }

                // Display errors
                if (validationResult.Errors.Any())
                {
                    AnsiConsole.MarkupLine("[red]✗ Compatibility errors:[/]");
                    foreach (var error in validationResult.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]• {error.EscapeMarkup()}[/]");
                    }
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Fix these issues or choose a different target directory.[/]");
                    CleanupEmptyDirectory(targetDirectory, targetDirExists);
                    return 1;
                }

                // Display warnings
                if (validationResult.Warnings.Any())
                {
                    foreach (var warning in validationResult.Warnings)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]• {warning.EscapeMarkup()}[/]");
                    }
                }

                // Prompt user
                if (!settings.Force)
                {
                    AnsiConsole.WriteLine();
                    if (!AnsiConsole.Confirm("Continue and link to this project?", false))
                    {
                        AnsiConsole.MarkupLine("[dim]Aborted.[/]");
                        CleanupEmptyDirectory(targetDirectory, targetDirExists);
                        return 1;
                    }
                }
                AnsiConsole.WriteLine();
            }

            // 10. Save remote URL to cloud.json
            cloudConfig.Remote = remoteUrl.ToString();
            CloudConfigManager.SaveAsync(targetDirectory, cloudConfig, cancellationToken).GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[green]✓ Linked to remote project[/]");
            AnsiConsole.MarkupLine($"[dim]  Saved to: {System.IO.Path.Combine(targetDirectory, ".lrm", "cloud.json").EscapeMarkup()}[/]");

            // 11. Pull resources (unless --no-pull)
            if (!settings.NoPull)
            {
                AnsiConsole.WriteLine();
                return PullResources(targetDirectory, remoteUrl, cloudConfig, remoteProject, apiClient, cancellationToken);
            }
            else
            {
                AnsiConsole.WriteLine();
                DisplaySuccessMessage(targetDirectory, remoteUrl);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Skipped pulling resources (--no-pull)[/]");
                AnsiConsole.MarkupLine("[dim]Run 'lrm cloud pull' when ready[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private string DetermineTargetDirectory(string? providedPath, RemoteUrl remoteUrl)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            return System.IO.Path.GetFullPath(providedPath);
        }

        // Default: ./{project-slug}
        return System.IO.Path.GetFullPath(remoteUrl.ProjectName);
    }

    private bool Authenticate(RemoteUrl remoteUrl, CloudConfig config, CloneCommandSettings settings, CancellationToken ct)
    {
        // Get email (from option or prompt)
        var email = settings.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = AnsiConsole.Ask<string>("Email:");
        }

        if (!IsValidEmail(email))
        {
            AnsiConsole.MarkupLine("[red]✗ Invalid email address[/]");
            return false;
        }

        // Get password (from option or prompt)
        var password = settings.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = AnsiConsole.Prompt(
                new TextPrompt<string>("Password:")
                    .PromptStyle("red")
                    .Secret());
        }

        try
        {
            LoginResponse? response = null;
            AnsiConsole.Status()
                .Start("Authenticating...", ctx =>
                {
                    using var apiClient = new CloudApiClient(remoteUrl);
                    response = apiClient.LoginAsync(email, password, ct).GetAwaiter().GetResult();
                });

            if (response == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Authentication failed[/]");
                return false;
            }

            config.AccessToken = response.Token;
            config.ExpiresAt = response.ExpiresAt;
            config.RefreshToken = response.RefreshToken;
            config.RefreshTokenExpiresAt = response.RefreshTokenExpiresAt;

            var displayName = !string.IsNullOrWhiteSpace(response.User.DisplayName)
                ? response.User.DisplayName
                : response.User.Username;

            AnsiConsole.MarkupLine($"[green]✓ Logged in as {displayName.EscapeMarkup()}[/]");
            return true;
        }
        catch (CloudApiException ex) when (ex.StatusCode == 401)
        {
            AnsiConsole.MarkupLine("[red]✗ Invalid email or password[/]");
            return false;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Authentication failed: {ex.Message.EscapeMarkup()}[/]");
            return false;
        }
    }

    private static void ConfigureAuth(CloudApiClient apiClient, CloudConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            apiClient.SetApiKey(config.ApiKey);
        }
        else
        {
            apiClient.SetAccessToken(config.AccessToken);
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    private static bool HasResourceFiles(string directory, string format)
    {
        if (!Directory.Exists(directory))
            return false;

        var extensions = format.ToLowerInvariant() switch
        {
            "resx" => new[] { "*.resx" },
            "json" or "i18next" => new[] { "*.json" },
            _ => new[] { "*.resx", "*.json" }
        };

        foreach (var ext in extensions)
        {
            if (Directory.GetFiles(directory, ext, SearchOption.TopDirectoryOnly).Length > 0)
                return true;
        }

        return false;
    }

    private static void ValidateConfigCompatibility(
        ConfigurationModel localConfig,
        CloudProject remoteProject,
        CloudSyncValidator.SyncValidationResult result)
    {
        // Check I18nextCompatible for JSON projects
        if (remoteProject.Format == "json" || remoteProject.Format == "i18next")
        {
            var localI18next = localConfig.Json?.I18nextCompatible ?? false;
            var remoteI18next = remoteProject.Format == "i18next";

            if (localI18next != remoteI18next)
            {
                if (localI18next && !remoteI18next)
                {
                    result.AddWarning("I18next mode mismatch: local uses i18next naming (en.json), but remote expects standard naming (strings.json)");
                }
                else
                {
                    result.AddWarning("I18next mode mismatch: local uses standard naming (strings.json), but remote expects i18next naming (en.json)");
                }
                result.AddWarning("File naming may differ during sync operations.");
            }
        }

        // Check default language
        var localDefault = localConfig.DefaultLanguageCode?.ToLowerInvariant();
        var remoteDefault = remoteProject.DefaultLanguage?.ToLowerInvariant();

        if (!string.IsNullOrEmpty(localDefault) &&
            !string.IsNullOrEmpty(remoteDefault) &&
            localDefault != remoteDefault)
        {
            result.AddWarning($"Default language differs: local='{localConfig.DefaultLanguageCode}', remote='{remoteProject.DefaultLanguage}'");
            result.AddWarning("This may affect which translations are considered 'source' strings.");
        }

        // Check format matches
        var localFormat = localConfig.ResourceFormat?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(localFormat))
        {
            var effectiveRemoteFormat = remoteProject.Format == "i18next" ? "json" : remoteProject.Format;
            if (localFormat != effectiveRemoteFormat)
            {
                result.AddError($"Format mismatch: local lrm.json specifies '{localConfig.ResourceFormat}', but remote project uses '{remoteProject.Format}'");
            }
        }
    }

    private int PullResources(string targetDirectory, RemoteUrl remoteUrl, CloudConfig cloudConfig,
        CloudProject remoteProject, CloudApiClient apiClient, CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[blue]Pulling resources...[/]");
        AnsiConsole.WriteLine();

        // Fetch resources from remote using V2 pull API
        PullResponse? pullResponse = null;
        try
        {
            AnsiConsole.Status()
                .Start("Fetching resources...", ctx =>
                {
                    pullResponse = apiClient.PullResourcesAsync(ct).GetAwaiter().GetResult();
                });
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to fetch resources: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }

        if (pullResponse == null || pullResponse.Files.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No resources to pull (project is empty)[/]");
            DisplaySuccessMessage(targetDirectory, remoteUrl);
            return 0;
        }

        // Write each resource file
        var pullCount = 0;
        foreach (var file in pullResponse.Files)
        {
            var fileName = file.Path;
            var filePath = System.IO.Path.Combine(targetDirectory, fileName);

            try
            {
                // Ensure directory exists
                var fileDir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                File.WriteAllText(filePath, file.Content);
                AnsiConsole.MarkupLine($"[green]✓[/] {fileName.EscapeMarkup()}");
                pullCount++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {fileName.EscapeMarkup()}: {ex.Message.EscapeMarkup()}");
            }
        }

        // Pull lrm.json config if available from pull response
        if (!string.IsNullOrWhiteSpace(pullResponse.Configuration))
        {
            try
            {
                var configPath = System.IO.Path.Combine(targetDirectory, "lrm.json");
                File.WriteAllText(configPath, pullResponse.Configuration);
                AnsiConsole.MarkupLine("[green]✓[/] lrm.json (config)");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Failed to write lrm.json: {ex.Message.EscapeMarkup()}");
            }
        }

        // Update sync state
        try
        {
            var syncState = new SyncState
            {
                Timestamp = DateTime.UtcNow,
                Files = pullResponse.Files.ToDictionary(f => f.Path, f => f.Hash ?? "")
            };
            SyncStateManager.SaveAsync(targetDirectory, syncState, ct).GetAwaiter().GetResult();
        }
        catch
        {
            // Non-critical, ignore
        }

        AnsiConsole.WriteLine();
        DisplaySuccessMessage(targetDirectory, remoteUrl);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Pulled {pullCount} resource file(s)[/]");

        return 0;
    }

    private static void DisplaySuccessMessage(string targetDirectory, RemoteUrl remoteUrl)
    {
        AnsiConsole.MarkupLine("[green]Done! Project cloned successfully.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Directory: {targetDirectory.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine($"[dim]Remote: {remoteUrl.ToString().EscapeMarkup()}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Next steps:[/]");

        var currentDir = Directory.GetCurrentDirectory();
        if (!targetDirectory.Equals(currentDir, StringComparison.OrdinalIgnoreCase))
        {
            var relativeDir = System.IO.Path.GetRelativePath(currentDir, targetDirectory);
            AnsiConsole.MarkupLine($"[dim]  cd {relativeDir.EscapeMarkup()}[/]");
        }
        AnsiConsole.MarkupLine("[dim]  lrm stats          # View translation coverage[/]");
        AnsiConsole.MarkupLine("[dim]  lrm cloud push     # Push local changes[/]");
    }

    private static void CleanupEmptyDirectory(string directory, bool existedBefore)
    {
        // Only cleanup if we created the directory and it's empty
        if (!existedBefore && Directory.Exists(directory))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
