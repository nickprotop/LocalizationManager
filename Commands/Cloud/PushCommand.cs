// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the push command.
/// </summary>
public class PushCommandSettings : BaseCommandSettings
{
    [CommandOption("-m|--message <MESSAGE>")]
    [Description("Commit message for this push")]
    public string? Message { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be pushed without actually pushing")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("--force")]
    [Description("Force push even if there are conflicts")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [CommandOption("--config-only")]
    [Description("Push only configuration (lrm.json), skip resources")]
    [DefaultValue(false)]
    public bool ConfigOnly { get; set; }

    [CommandOption("--resources-only")]
    [Description("Push only resources, skip configuration")]
    [DefaultValue(false)]
    public bool ResourcesOnly { get; set; }
}

/// <summary>
/// Command to push local changes (resources + lrm.json) to the cloud.
/// </summary>
public class PushCommand : Command<PushCommandSettings>
{
    public override int Execute(CommandContext context, PushCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            AnsiConsole.MarkupLine("[blue]Preparing to push to cloud...[/]");
            AnsiConsole.WriteLine();

            // Load configuration
            var config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Validate cloud configuration
            if (config.Cloud == null || string.IsNullOrWhiteSpace(config.Cloud.Remote))
            {
                AnsiConsole.MarkupLine("[red]✗ No remote URL configured![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url>' to configure a remote URL[/]");
                return 1;
            }

            if (!config.Cloud.Enabled)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Cloud synchronization is disabled[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm remote set <url> --enable' to enable cloud synchronization[/]");
                return 1;
            }

            // Parse remote URL
            if (!RemoteUrlParser.TryParse(config.Cloud.Remote, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {config.Cloud.Remote.EscapeMarkup()}");
                return 1;
            }

            AnsiConsole.MarkupLine($"[dim]Remote: {remoteUrl!.ToString().EscapeMarkup()}[/]");
            AnsiConsole.WriteLine();

            // Validate conflicting options
            if (settings.ConfigOnly && settings.ResourcesOnly)
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot use --config-only and --resources-only together![/]");
                return 1;
            }

            // Validate configuration
            var validator = new ConfigurationValidator(projectDirectory);
            var validationResult = validator.Validate(config);

            if (!validationResult.IsValid)
            {
                AnsiConsole.MarkupLine("[red]✗ Configuration validation failed:[/]");
                foreach (var error in validationResult.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]• {error.EscapeMarkup()}[/]");
                }
                return 1;
            }

            if (validationResult.Warnings.Any())
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Configuration warnings:[/]");
                foreach (var warning in validationResult.Warnings)
                {
                    AnsiConsole.MarkupLine($"  [yellow]• {warning.EscapeMarkup()}[/]");
                }
                AnsiConsole.WriteLine();
            }

            // Check authentication
            var token = AuthTokenManager.GetTokenAsync(projectDirectory, remoteUrl.Host, cancellationToken).GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(token))
            {
                AnsiConsole.MarkupLine("[red]✗ Not authenticated![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login {remoteUrl.Host}' to authenticate[/]");
                AnsiConsole.MarkupLine($"[dim]Or set token manually: lrm cloud set-token --host {remoteUrl.Host.EscapeMarkup()} --token <your-token>[/]");
                return 1;
            }

            // Create API client
            using var apiClient = new CloudApiClient(remoteUrl);
            apiClient.SetAccessToken(token);

            // Collect items to push
            var itemsToPush = new List<string>();
            if (!settings.ResourcesOnly)
            {
                itemsToPush.Add("Configuration (lrm.json)");
            }

            if (!settings.ConfigOnly)
            {
                itemsToPush.Add("Resource files");
            }

            // Show what will be pushed
            AnsiConsole.MarkupLine("[blue]Items to push:[/]");
            foreach (var item in itemsToPush)
            {
                AnsiConsole.MarkupLine($"  [dim]• {item}[/]");
            }
            AnsiConsole.WriteLine();

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[yellow]Dry run - no changes will be made[/]");
                return 0;
            }

            // Push configuration
            if (!settings.ResourcesOnly)
            {
                AnsiConsole.Status()
                    .Start("Pushing configuration...", ctx =>
                    {
                        PushConfiguration(projectDirectory, apiClient, settings.Force, cancellationToken);
                    });

                AnsiConsole.MarkupLine("[green]✓ Configuration pushed successfully[/]");
            }

            // Push resources
            if (!settings.ConfigOnly)
            {
                PushResponse? result = null;
                AnsiConsole.Status()
                    .Start("Pushing resources...", ctx =>
                    {
                        result = PushResources(projectDirectory, settings, apiClient, settings.Message, cancellationToken);
                        ctx.Status($"Modified: {result.ModifiedCount}, Deleted: {result.DeletedCount}");
                    });

                if (result != null && (result.ModifiedCount > 0 || result.DeletedCount > 0))
                {
                    AnsiConsole.MarkupLine("[green]✓ Resources pushed successfully[/]");
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green bold]✓ Push completed successfully![/]");

            return 0;
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]✗ Cloud API error: {ex.Message.EscapeMarkup()}[/]");

            if (ex.StatusCode == 401)
            {
                AnsiConsole.MarkupLine("[dim]Your authentication token may have expired. Please login again.[/]");
            }
            else if (ex.StatusCode == 403)
            {
                AnsiConsole.MarkupLine("[dim]You don't have permission to push to this project.[/]");
            }
            else if (ex.StatusCode == 409)
            {
                AnsiConsole.MarkupLine("[dim]Conflict detected. Use --force to override, or pull first to merge changes.[/]");
            }

            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private void PushConfiguration(
        string projectDirectory,
        CloudApiClient apiClient,
        bool force,
        CancellationToken cancellationToken)
    {
        // Get team configuration JSON
        var configJson = Core.Configuration.ConfigurationManager
            .LoadTeamConfigurationAsync(projectDirectory, cancellationToken)
            .GetAwaiter()
            .GetResult();

        var configJsonString = System.Text.Json.JsonSerializer.Serialize(configJson);

        // Get current remote version if not forcing
        string? baseVersion = null;
        if (!force)
        {
            try
            {
                var remoteConfig = apiClient.GetConfigurationAsync(cancellationToken).GetAwaiter().GetResult();
                baseVersion = remoteConfig.Version;
            }
            catch (CloudApiException ex) when (ex.StatusCode == 404)
            {
                // No remote config exists yet, that's okay
            }
        }

        // Push configuration
        apiClient.UpdateConfigurationAsync(configJsonString, baseVersion, cancellationToken).GetAwaiter().GetResult();
    }

    private PushResponse PushResources(
        string projectDirectory,
        PushCommandSettings settings,
        CloudApiClient apiClient,
        string? message,
        CancellationToken cancellationToken)
    {
        // Load sync state (file hashes from last push)
        var syncState = Core.Cloud.SyncStateManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
        var lastPushFiles = syncState?.Files ?? new Dictionary<string, string>();

        // Load configuration JSON (if exists)
        var configPath = Path.Combine(projectDirectory, "lrm.json");
        string? configJson = null;
        string? configHash = null;

        if (File.Exists(configPath))
        {
            configJson = File.ReadAllText(configPath);
            configHash = ComputeHash(configJson);
        }

        // Check if configuration changed
        bool configChanged = configHash != syncState?.ConfigHash;

        // Discover current resource files
        var languages = settings.DiscoverLanguages();
        var currentFiles = new Dictionary<string, (string Path, string Content, string Hash)>();

        foreach (var lang in languages)
        {
            var relativePath = GetResourcePath(lang, settings);
            var content = File.ReadAllText(lang.FilePath);  // Read raw file content
            var hash = ComputeHash(content);

            currentFiles[relativePath] = (relativePath, content, hash);
        }

        // Detect changes
        var modifiedFiles = new List<Core.Cloud.FileDto>();
        var deletedFiles = new List<string>();

        // Check for new/modified files
        foreach (var (path, (filePath, content, hash)) in currentFiles)
        {
            if (!lastPushFiles.ContainsKey(path) || lastPushFiles[path] != hash)
            {
                modifiedFiles.Add(new Core.Cloud.FileDto
                {
                    Path = path,
                    Content = content,
                    Hash = hash
                });
            }
        }

        // Check for deleted files
        foreach (var path in lastPushFiles.Keys)
        {
            if (!currentFiles.ContainsKey(path))
            {
                deletedFiles.Add(path);
            }
        }

        // Early exit if no changes
        if (modifiedFiles.Count == 0 && deletedFiles.Count == 0 && !configChanged)
        {
            AnsiConsole.MarkupLine("[yellow]No changes to push[/]");
            return new PushResponse { Success = true, ModifiedCount = 0, DeletedCount = 0, Message = "No changes" };
        }

        // Show summary
        if (modifiedFiles.Count > 0)
            AnsiConsole.MarkupLine($"[green]Modified files: {modifiedFiles.Count}[/]");
        if (deletedFiles.Count > 0)
            AnsiConsole.MarkupLine($"[red]Deleted files: {deletedFiles.Count}[/]");
        if (configChanged)
            AnsiConsole.MarkupLine("[blue]Configuration changed[/]");
        AnsiConsole.WriteLine();

        // Create push request
        var request = new Core.Cloud.PushRequest
        {
            Configuration = configChanged ? configJson : null,
            ModifiedFiles = modifiedFiles,
            DeletedFiles = deletedFiles
        };

        // Push to server
        var response = apiClient.PushResourcesAsync(request, cancellationToken).GetAwaiter().GetResult();

        // Update sync state
        var newSyncState = new Core.Cloud.Models.SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = configHash,
            Files = currentFiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Hash)
        };

        Core.Cloud.SyncStateManager.SaveAsync(projectDirectory, newSyncState, cancellationToken).GetAwaiter().GetResult();

        return response;
    }

    private string GetResourcePath(Core.Models.LanguageInfo language, PushCommandSettings settings)
    {
        // Get relative path from resource directory
        var resourcePath = settings.GetResourcePath();
        var fullPath = language.FilePath;

        if (fullPath.StartsWith(resourcePath))
        {
            return fullPath.Substring(resourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return Path.GetFileName(fullPath);
    }

    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
