// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands.Remote;

/// <summary>
/// Settings for the remote set command.
/// </summary>
public class RemoteSetCommandSettings : BaseCommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("Remote URL (e.g., https://lrm-cloud.com/org/project or https://lrm-cloud.com/@username/project)")]
    public string Url { get; set; } = string.Empty;

    [CommandOption("--force")]
    [Description("Force set remote even with compatibility warnings")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [CommandOption("--offline")]
    [Description("Skip remote validation (only save URL locally)")]
    [DefaultValue(false)]
    public bool Offline { get; set; }
}

/// <summary>
/// Command to set the remote URL for cloud synchronization.
/// Validates project compatibility before linking.
/// </summary>
public class RemoteSetCommand : Command<RemoteSetCommandSettings>
{
    public override int Execute(CommandContext context, RemoteSetCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            // Validate remote URL format
            if (!RemoteUrlParser.TryParse(settings.Url, out var remoteUrl))
            {
                AnsiConsole.MarkupLine("[red]✗ Invalid remote URL format![/]");
                AnsiConsole.MarkupLine("[dim]Expected: https://host/org/project or https://host/@username/project[/]");
                AnsiConsole.MarkupLine("[dim]Examples:[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm-cloud.com/acme-corp/mobile-app[/]");
                AnsiConsole.MarkupLine("[dim]  • https://lrm-cloud.com/@john/personal-project[/]");
                return 1;
            }

            // Load current cloud configuration
            var cloudConfig = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(cloudConfig.ApiKey))
            {
                cloudConfig.ApiKey = envApiKey;
            }

            // Detect host change and clear auth if needed
            if (IsHostChanged(cloudConfig, remoteUrl!))
            {
                var oldHost = GetAuthority(cloudConfig.Remote!);
                var newHost = GetAuthority(remoteUrl.OriginalUrl);

                AnsiConsole.MarkupLine($"[yellow]⚠ Host changed from {oldHost.EscapeMarkup()} to {newHost.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine("[dim]  Previous credentials cleared. Re-authentication required.[/]");
                AnsiConsole.WriteLine();

                // Clear all stored auth (env API key is kept - user's choice)
                cloudConfig.AccessToken = null;
                cloudConfig.RefreshToken = null;
                cloudConfig.ExpiresAt = null;
                cloudConfig.RefreshTokenExpiresAt = null;
                cloudConfig.ApiKey = null;
            }

            // If --offline, skip validation (existing behavior)
            if (settings.Offline)
            {
                cloudConfig.Remote = remoteUrl!.ToString();
                CloudConfigManager.SaveAsync(projectDirectory, cloudConfig, cancellationToken).GetAwaiter().GetResult();

                AnsiConsole.MarkupLine($"[green]✓ Remote URL set (offline mode):[/] {remoteUrl.ToString().EscapeMarkup()}");
                DisplayProjectInfo(remoteUrl);
                AnsiConsole.MarkupLine("[dim]  Note: Remote project was not validated. Run 'lrm cloud status' to verify.[/]");
                return 0;
            }

            // Check authentication - prompt login if not authenticated
            if (!cloudConfig.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Not authenticated. Please log in to validate remote project.[/]");
                AnsiConsole.WriteLine();

                if (!PromptAndLogin(remoteUrl!, cloudConfig, projectDirectory, cancellationToken))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Use --offline to skip validation and set remote without login[/]");
                    return 1;
                }
                AnsiConsole.WriteLine();
            }

            // Fetch remote project to validate it exists and check compatibility
            using var apiClient = new CloudApiClient(remoteUrl!);
            ConfigureAuth(apiClient, cloudConfig);

            CloudProject? remoteProject = null;
            try
            {
                AnsiConsole.Status()
                    .Start("Connecting to remote...", ctx =>
                    {
                        remoteProject = apiClient.GetProjectAsync(cancellationToken).GetAwaiter().GetResult();
                    });
            }
            catch (CloudApiException ex) when (ex.StatusCode == 404)
            {
                AnsiConsole.MarkupLine("[red]✗ Project not found![/]");
                AnsiConsole.MarkupLine($"[dim]  URL: {remoteUrl.ToString().EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]To create this project, run:[/]");
                AnsiConsole.MarkupLine("[dim]  lrm cloud init[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Or use --offline to set remote without validation:[/]");
                AnsiConsole.MarkupLine($"[dim]  lrm cloud remote set --offline {settings.Url.EscapeMarkup()}[/]");
                return 1;
            }
            catch (CloudApiException ex) when (ex.StatusCode == 401 || ex.StatusCode == 403)
            {
                AnsiConsole.MarkupLine("[red]✗ Access denied![/]");
                AnsiConsole.MarkupLine("[dim]You may not have permission to access this project.[/]");
                return 1;
            }

            // Show remote project info
            AnsiConsole.MarkupLine($"[green]✓ Found project:[/] {remoteProject!.Name.EscapeMarkup()}");
            AnsiConsole.MarkupLine($"[dim]  Format: {remoteProject.Format}, Default language: {remoteProject.DefaultLanguage}[/]");
            AnsiConsole.WriteLine();

            // Run validations
            var validator = new CloudSyncValidator(projectDirectory);
            var validationResult = validator.ValidateForLink(remoteProject);

            // Also check local lrm.json config vs remote
            var localConfig = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            if (localConfig != null)
            {
                ValidateConfigCompatibility(localConfig, remoteProject, validationResult);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]ℹ No local lrm.json found. Will use remote config on pull.[/]");
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
                AnsiConsole.MarkupLine("[dim]Fix these issues before linking, or create a new project with matching settings.[/]");
                return 1;
            }

            // Display warnings and prompt for confirmation
            if (validationResult.Warnings.Any())
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Compatibility warnings:[/]");
                foreach (var warning in validationResult.Warnings)
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning.EscapeMarkup()}[/]");
                }
                AnsiConsole.WriteLine();

                if (!settings.Force)
                {
                    if (!AnsiConsole.Confirm("Continue anyway?", false))
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[dim]Use --force to skip this prompt[/]");
                        return 1;
                    }
                    AnsiConsole.WriteLine();
                }
            }

            // Save remote URL
            cloudConfig.Remote = remoteUrl.ToString();
            CloudConfigManager.SaveAsync(projectDirectory, cloudConfig, cancellationToken).GetAwaiter().GetResult();

            // Display success message
            AnsiConsole.MarkupLine($"[green]✓ Remote URL set successfully![/]");
            DisplayProjectInfo(remoteUrl);

            // Warn if using HTTP for non-localhost
            if (!remoteUrl.UseHttps && !IsLocalhost(remoteUrl.Host))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]⚠ Warning: Using HTTP instead of HTTPS for non-localhost connection![/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private static void DisplayProjectInfo(RemoteUrl remoteUrl)
    {
        if (remoteUrl.IsPersonalProject)
        {
            AnsiConsole.MarkupLine($"[dim]  Type: Personal project (@{remoteUrl.Username})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]  Type: Organization project ({remoteUrl.Organization})[/]");
        }

        AnsiConsole.MarkupLine($"[dim]  Project: {remoteUrl.ProjectName}[/]");
        AnsiConsole.MarkupLine($"[dim]  API URL: {remoteUrl.ApiBaseUrl.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine($"[dim]  Saved to: .lrm/cloud.json[/]");
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

    private static bool PromptAndLogin(RemoteUrl remoteUrl, CloudConfig config,
        string projectDirectory, CancellationToken ct)
    {
        var email = AnsiConsole.Ask<string>("Email:");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Password:")
                .PromptStyle("red")
                .Secret());

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

            CloudConfigManager.SaveAsync(projectDirectory, config, ct).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine("[green]✓ Logged in![/]");
            return true;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Login failed: {ex.Message.EscapeMarkup()}[/]");
            return false;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Login failed: {ex.Message.EscapeMarkup()}[/]");
            return false;
        }
    }

    private static void ValidateConfigCompatibility(
        ConfigurationModel localConfig,
        CloudProject remoteProject,
        CloudSyncValidator.SyncValidationResult result)
    {
        // Check I18nextCompatible for JSON projects
        if (remoteProject.Format == "json" || remoteProject.Format == "i18next")
        {
            // Local is i18next if either: format is "i18next" OR json.i18nextCompatible is true
            var localFormat = localConfig.ResourceFormat?.ToLowerInvariant();
            var localI18next = localFormat == "i18next" || (localConfig.Json?.I18nextCompatible ?? false);
            var remoteI18next = remoteProject.Format == "i18next";

            if (localI18next != remoteI18next)
            {
                if (localI18next && !remoteI18next)
                {
                    result.AddWarning($"I18next mode mismatch: local uses i18next naming (en.json), but remote expects standard naming (strings.json)");
                }
                else
                {
                    result.AddWarning($"I18next mode mismatch: local uses standard naming (strings.json), but remote expects i18next naming (en.json)");
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

        // Check format matches (json and i18next are compatible)
        // Skip validation if remote format is empty (client-agnostic API)
        var localFmt = localConfig.ResourceFormat?.ToLowerInvariant();
        var remoteFmt = remoteProject.Format?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(localFmt) && !string.IsNullOrEmpty(remoteFmt))
        {
            var normalizedLocal = NormalizeJsonFormat(localFmt);
            var normalizedRemote = NormalizeJsonFormat(remoteFmt);
            if (normalizedLocal != normalizedRemote)
            {
                result.AddError($"Format mismatch: local lrm.json specifies '{localConfig.ResourceFormat}', but remote project uses '{remoteProject.Format}'");
            }
        }
    }

    /// <summary>
    /// Normalizes JSON format names for compatibility comparison.
    /// </summary>
    private static string NormalizeJsonFormat(string? format)
    {
        return format?.ToLowerInvariant() switch
        {
            "json" or "jsonlocalization" or "i18next" => "json",
            _ => format?.ToLowerInvariant() ?? ""
        };
    }

    private static bool IsLocalhost(string host)
    {
        var lower = host.ToLowerInvariant();
        return lower == "localhost" || lower == "127.0.0.1" || lower == "::1";
    }

    /// <summary>
    /// Checks if the host has changed between the existing config and the new remote URL.
    /// Compares host and port (different ports = different hosts).
    /// </summary>
    private static bool IsHostChanged(CloudConfig config, RemoteUrl newRemote)
    {
        if (string.IsNullOrWhiteSpace(config.Remote))
            return false; // No previous remote

        try
        {
            var oldUri = new Uri(config.Remote);
            var oldAuthority = $"{oldUri.Host}:{oldUri.Port}".ToLowerInvariant();
            var newAuthority = $"{newRemote.Host}:{newRemote.Port}".ToLowerInvariant();
            return oldAuthority != newAuthority;
        }
        catch
        {
            return false; // Can't parse, assume no change
        }
    }

    /// <summary>
    /// Gets the authority (host:port) from a URL for display purposes.
    /// </summary>
    private static string GetAuthority(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            return url;
        }
    }
}
