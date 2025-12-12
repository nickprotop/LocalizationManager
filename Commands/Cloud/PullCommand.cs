// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Security;
using System.Text.Json;

namespace LocalizationManager.Commands.Cloud;

/// <summary>
/// Settings for the pull command.
/// </summary>
public class PullCommandSettings : BaseCommandSettings
{
    [CommandOption("--dry-run")]
    [Description("Show what would be pulled without actually pulling")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("--force")]
    [Description("Force pull and overwrite local changes without prompting")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [CommandOption("--no-backup")]
    [Description("Skip creating a backup before pulling")]
    [DefaultValue(false)]
    public bool NoBackup { get; set; }

    [CommandOption("--strategy <STRATEGY>")]
    [Description("Conflict resolution strategy: local, remote, prompt, abort (default: prompt)")]
    [DefaultValue("prompt")]
    public string Strategy { get; set; } = "prompt";

    [CommandOption("--config-only")]
    [Description("Pull only configuration (lrm.json), skip resources")]
    [DefaultValue(false)]
    public bool ConfigOnly { get; set; }

    [CommandOption("--resources-only")]
    [Description("Pull only resources, skip configuration")]
    [DefaultValue(false)]
    public bool ResourcesOnly { get; set; }
}

/// <summary>
/// Command to pull remote changes (resources + lrm.json) from the cloud.
/// </summary>
public class PullCommand : Command<PullCommandSettings>
{
    public override int Execute(CommandContext context, PullCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDirectory = settings.GetResourcePath();

            AnsiConsole.MarkupLine("[blue]Pulling changes from cloud...[/]");
            AnsiConsole.WriteLine();

            // Load cloud configuration
            var cloudConfig = CloudConfigManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Check for API key from environment
            var envApiKey = CloudConfigManager.GetApiKeyFromEnvironment();
            if (!string.IsNullOrWhiteSpace(envApiKey) && string.IsNullOrWhiteSpace(cloudConfig.ApiKey))
            {
                cloudConfig.ApiKey = envApiKey;
            }

            // Validate remote configuration
            if (!cloudConfig.HasProject)
            {
                AnsiConsole.MarkupLine("[red]✗ No remote project configured![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'lrm cloud init <url>' to connect to a project[/]");
                return 1;
            }

            // Parse remote URL
            if (!RemoteUrlParser.TryParse(cloudConfig.Remote!, out var remoteUrl))
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid remote URL:[/] {cloudConfig.Remote?.EscapeMarkup()}");
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

            // Parse resolution strategy
            var strategy = ParseStrategy(settings.Strategy);
            if (strategy == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid strategy:[/] {settings.Strategy.EscapeMarkup()}");
                AnsiConsole.MarkupLine("[dim]Valid strategies: local, remote, prompt, abort[/]");
                return 1;
            }

            // Check authentication
            if (!cloudConfig.IsLoggedIn)
            {
                AnsiConsole.MarkupLine("[red]✗ Not authenticated![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud login {remoteUrl.Host}' to authenticate[/]");
                AnsiConsole.MarkupLine($"[dim]Or set an API key: lrm cloud set-api-key[/]");
                AnsiConsole.MarkupLine($"[dim]Or use environment variable: LRM_CLOUD_API_KEY[/]");
                return 1;
            }

            // Create API client
            using var apiClient = new CloudApiClient(remoteUrl);

            if (!string.IsNullOrWhiteSpace(cloudConfig.ApiKey))
            {
                apiClient.SetApiKey(cloudConfig.ApiKey);
                AnsiConsole.MarkupLine("[dim]Using API key authentication[/]");
            }
            else
            {
                apiClient.SetAccessToken(cloudConfig.AccessToken);
            }

            // Fetch remote project info and validate format compatibility
            CloudProject? remoteProject = null;
            try
            {
                AnsiConsole.Status()
                    .Start("Checking remote project...", ctx =>
                    {
                        remoteProject = apiClient.GetProjectAsync(cancellationToken).GetAwaiter().GetResult();
                    });
            }
            catch (CloudApiException ex) when (ex.StatusCode == 404)
            {
                AnsiConsole.MarkupLine("[red]✗ Remote project not found![/]");
                AnsiConsole.MarkupLine("[dim]The project may have been deleted or you don't have access.[/]");
                return 1;
            }

            // Load local config if exists
            ConfigurationModel? localConfig = null;
            var localConfigPath = Path.Combine(projectDirectory, "lrm.json");
            if (File.Exists(localConfigPath))
            {
                localConfig = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            }

            // Validate format compatibility
            var syncValidator = new CloudSyncValidator(projectDirectory);
            var syncValidation = syncValidator.ValidateForPull(localConfig, remoteProject!);

            if (syncValidation.Warnings.Any())
            {
                foreach (var warning in syncValidation.Warnings)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ {warning.EscapeMarkup()}[/]");
                }
                AnsiConsole.WriteLine();
            }

            if (!syncValidation.CanSync)
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot pull due to compatibility issues:[/]");
                foreach (var error in syncValidation.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]• {error.EscapeMarkup()}[/]");
                }
                return 1;
            }

            // Fetch remote data (V2 - pull files from database)
            PullResponse? pullResponse = null;

            AnsiConsole.Status()
                .Start("Fetching remote data...", ctx =>
                {
                    pullResponse = apiClient.PullResourcesAsync(cancellationToken).GetAwaiter().GetResult();
                });

            if (pullResponse == null)
            {
                throw new InvalidOperationException("Failed to pull resources from server");
            }

            // Detect conflicts and show diff
            var conflictDetector = new ConflictDetector();
            var conflicts = new List<ConflictDetector.Conflict>();

            // Check configuration conflict
            if (!settings.ResourcesOnly && pullResponse.Configuration != null)
            {
                if (File.Exists(localConfigPath))
                {
                    var localConfigJson = File.ReadAllText(localConfigPath);
                    var configConflict = conflictDetector.DetectConfigurationConflict(localConfigJson, pullResponse.Configuration);

                    if (configConflict != null)
                    {
                        conflicts.Add(configConflict);
                    }
                }
            }

            // Check resource conflicts
            ConflictDetector.DiffSummary? diffSummary = null;
            if (!settings.ConfigOnly && pullResponse.Files.Count > 0)
            {
                var localResources = GetLocalResources(settings, projectDirectory);
                var remoteResources = pullResponse.Files;
                var resourceConflicts = conflictDetector.DetectResourceConflicts(localResources, remoteResources);
                conflicts.AddRange(resourceConflicts);

                diffSummary = conflictDetector.GetDiffSummary(localResources, remoteResources);
            }

            // Show changes summary
            ShowChangesSummary(diffSummary, conflicts, settings);

            if (diffSummary != null && !diffSummary.HasChanges && conflicts.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]✓ Already up to date![/]");
                return 0;
            }

            if (settings.DryRun)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Dry run - no changes will be made[/]");
                return 0;
            }

            // Handle conflicts
            if (conflicts.Any())
            {
                if (!settings.Force && strategy.Value == ConflictDetector.ResolutionStrategy.Abort)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[red]✗ Conflicts detected. Use --force or --strategy to resolve.[/]");
                    return 1;
                }

                if (!settings.Force && strategy.Value == ConflictDetector.ResolutionStrategy.Prompt)
                {
                    if (!ResolveConflictsInteractively(conflicts))
                    {
                        AnsiConsole.MarkupLine("[yellow]Pull cancelled[/]");
                        return 0;
                    }
                }
                else if (strategy.Value == ConflictDetector.ResolutionStrategy.Local)
                {
                    foreach (var conflict in conflicts)
                    {
                        conflict.Resolution = ConflictDetector.ResolutionStrategy.Local;
                    }
                }
                else if (strategy.Value == ConflictDetector.ResolutionStrategy.Remote || settings.Force)
                {
                    foreach (var conflict in conflicts)
                    {
                        conflict.Resolution = ConflictDetector.ResolutionStrategy.Remote;
                    }
                }
            }

            // Confirm pull
            if (!settings.Force && !AnsiConsole.Confirm("Pull changes from cloud?", true))
            {
                AnsiConsole.MarkupLine("[yellow]Pull cancelled[/]");
                return 0;
            }

            // Create backup
            string? backupPath = null;
            if (!settings.NoBackup)
            {
                AnsiConsole.Status()
                    .Start("Creating backup...", ctx =>
                    {
                        var backupManager = new PullBackupManager(projectDirectory);
                        backupPath = backupManager.CreateBackupAsync(cancellationToken).GetAwaiter().GetResult();
                    });

                AnsiConsole.MarkupLine($"[dim]Backup created: {Path.GetFileName(backupPath)}[/]");
            }

            // Apply changes
            try
            {
                AnsiConsole.Status()
                    .Start("Applying changes...", ctx =>
                    {
                        if (!settings.ResourcesOnly && pullResponse.Configuration != null)
                        {
                            var configConflict = conflicts.FirstOrDefault(c => c.Type == ConflictDetector.ConflictType.ConfigurationConflict);
                            if (configConflict?.Resolution != ConflictDetector.ResolutionStrategy.Local)
                            {
                                ctx.Status("Updating configuration...");
                                ApplyConfigurationChanges(projectDirectory, pullResponse.Configuration, cancellationToken);
                            }
                        }

                        if (!settings.ConfigOnly && pullResponse.Files.Count > 0)
                        {
                            ctx.Status("Updating resources...");
                            ApplyResourceChanges(projectDirectory, pullResponse.Files, conflicts, cancellationToken);
                        }
                    });

                // Update sync state with pulled files
                var syncStateFiles = new Dictionary<string, string>();
                foreach (var file in pullResponse.Files)
                {
                    syncStateFiles[file.Path] = file.Hash ?? ComputeHash(file.Content);
                }

                var newSyncState = new Core.Cloud.Models.SyncState
                {
                    Timestamp = DateTime.UtcNow,
                    ConfigHash = pullResponse.Configuration != null ? ComputeHash(pullResponse.Configuration) : null,
                    Files = syncStateFiles
                };

                Core.Cloud.SyncStateManager.SaveAsync(projectDirectory, newSyncState, cancellationToken).GetAwaiter().GetResult();

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green bold]✓ Pull completed successfully![/]");

                if (backupPath != null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]To rollback: lrm cloud restore-backup \"{Path.GetFileName(backupPath)}\"[/]");
                }

                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[red]✗ Error applying changes: {ex.Message.EscapeMarkup()}[/]");

                if (backupPath != null)
                {
                    AnsiConsole.MarkupLine($"[yellow]Restoring from backup...[/]");
                    var backupManager = new PullBackupManager(projectDirectory);
                    backupManager.RestoreBackupAsync(backupPath, cancellationToken).GetAwaiter().GetResult();
                    AnsiConsole.MarkupLine("[green]✓ Backup restored[/]");
                }

                return 1;
            }
        }
        catch (CloudApiException ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]✗ Cloud API error: {ex.Message.EscapeMarkup()}[/]");

            if (ex.StatusCode == 401)
            {
                AnsiConsole.MarkupLine("[dim]Your authentication token may have expired. Please login again.[/]");
            }
            else if (ex.StatusCode == 404)
            {
                AnsiConsole.MarkupLine("[dim]Project not found on remote server.[/]");
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

    private List<FileDto> GetLocalResources(PullCommandSettings settings, string projectDirectory)
    {
        var languages = settings.DiscoverLanguages();
        var localResources = new List<FileDto>();

        foreach (var lang in languages)
        {
            var relativePath = GetRelativePath(lang.FilePath, projectDirectory);
            var content = File.ReadAllText(lang.FilePath);  // Read raw file content
            var hash = ComputeHash(content);

            localResources.Add(new FileDto
            {
                Path = relativePath,
                Content = content,
                Hash = hash
            });
        }

        return localResources;
    }

    private void ShowChangesSummary(
        ConflictDetector.DiffSummary? diffSummary,
        List<ConflictDetector.Conflict> conflicts,
        PullCommandSettings settings)
    {
        if (diffSummary != null)
        {
            AnsiConsole.MarkupLine("[blue]Changes to pull:[/]");

            if (diffSummary.FilesToAdd.Any())
            {
                AnsiConsole.MarkupLine($"  [green]+ {diffSummary.FilesToAdd.Count} file(s) to add[/]");
            }

            if (diffSummary.FilesToUpdate.Any())
            {
                AnsiConsole.MarkupLine($"  [yellow]~ {diffSummary.FilesToUpdate.Count} file(s) to update[/]");
            }

            if (diffSummary.FilesToDelete.Any())
            {
                AnsiConsole.MarkupLine($"  [red]- {diffSummary.FilesToDelete.Count} file(s) to delete[/]");
            }

            AnsiConsole.WriteLine();
        }

        if (conflicts.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {conflicts.Count} conflict(s) detected[/]");
            foreach (var conflict in conflicts)
            {
                AnsiConsole.MarkupLine($"  [yellow]• {conflict.Path.EscapeMarkup()}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private bool ResolveConflictsInteractively(List<ConflictDetector.Conflict> conflicts)
    {
        AnsiConsole.MarkupLine("[yellow bold]Conflicts detected:[/]");
        AnsiConsole.WriteLine();

        foreach (var conflict in conflicts)
        {
            AnsiConsole.MarkupLine($"[yellow]Conflict in:[/] {conflict.Path.EscapeMarkup()}");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("How do you want to resolve this conflict?")
                    .AddChoices(new[]
                    {
                        "Use remote version",
                        "Keep local version",
                        "Abort pull"
                    }));

            conflict.Resolution = choice switch
            {
                "Use remote version" => ConflictDetector.ResolutionStrategy.Remote,
                "Keep local version" => ConflictDetector.ResolutionStrategy.Local,
                _ => ConflictDetector.ResolutionStrategy.Abort
            };

            if (conflict.Resolution == ConflictDetector.ResolutionStrategy.Abort)
            {
                return false;
            }

            AnsiConsole.WriteLine();
        }

        return true;
    }

    private void ApplyConfigurationChanges(string projectDirectory, string remoteConfigJson, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<ConfigurationModel>(remoteConfigJson);
        if (config != null)
        {
            Core.Configuration.ConfigurationManager.SaveTeamConfigurationAsync(projectDirectory, config, cancellationToken).GetAwaiter().GetResult();
        }
    }

    private void ApplyResourceChanges(
        string projectDirectory,
        List<FileDto> remoteResources,
        List<ConflictDetector.Conflict> conflicts,
        CancellationToken cancellationToken)
    {
        // Get normalized base path for security validation
        var basePath = Path.GetFullPath(projectDirectory);

        foreach (var resource in remoteResources)
        {
            var conflict = conflicts.FirstOrDefault(c => c.Path == resource.Path);

            // Skip if conflict resolved to keep local
            if (conflict?.Resolution == ConflictDetector.ResolutionStrategy.Local)
            {
                continue;
            }

            // Security: Validate path doesn't escape project directory
            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, resource.Path));
            if (!fullPath.StartsWith(basePath + Path.DirectorySeparatorChar) && fullPath != basePath)
            {
                throw new SecurityException($"Security error: path traversal detected in '{resource.Path}'");
            }

            var directory = Path.GetDirectoryName(fullPath);

            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write raw file content (preserves original format)
            File.WriteAllText(fullPath, resource.Content);
        }
    }

    private ConflictDetector.ResolutionStrategy? ParseStrategy(string strategy)
    {
        return strategy.ToLowerInvariant() switch
        {
            "local" => ConflictDetector.ResolutionStrategy.Local,
            "remote" => ConflictDetector.ResolutionStrategy.Remote,
            "prompt" => ConflictDetector.ResolutionStrategy.Prompt,
            "abort" => ConflictDetector.ResolutionStrategy.Abort,
            _ => null
        };
    }

    private string GetRelativePath(string fullPath, string projectDirectory)
    {
        // projectDirectory already points to the correct resource directory
        if (fullPath.StartsWith(projectDirectory))
        {
            return fullPath.Substring(projectDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return Path.GetFileName(fullPath);
    }

    private string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
