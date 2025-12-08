// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
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

            // Parse resolution strategy
            var strategy = ParseStrategy(settings.Strategy);
            if (strategy == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid strategy:[/] {settings.Strategy.EscapeMarkup()}");
                AnsiConsole.MarkupLine("[dim]Valid strategies: local, remote, prompt, abort[/]");
                return 1;
            }

            // Check authentication
            var token = AuthTokenManager.GetTokenAsync(projectDirectory, remoteUrl.Host, cancellationToken).GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(token))
            {
                AnsiConsole.MarkupLine("[red]✗ Not authenticated![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Use 'lrm cloud set-token --host {remoteUrl.Host.EscapeMarkup()}' to authenticate[/]");
                return 1;
            }

            // Create API client
            using var apiClient = new CloudApiClient(remoteUrl);
            apiClient.SetAccessToken(token);

            // Fetch remote data
            ConfigurationSnapshot? remoteConfig = null;
            List<ResourceFile>? remoteResources = null;

            AnsiConsole.Status()
                .Start("Fetching remote data...", ctx =>
                {
                    if (!settings.ResourcesOnly)
                    {
                        ctx.Status("Fetching configuration...");
                        try
                        {
                            remoteConfig = apiClient.GetConfigurationAsync(cancellationToken).GetAwaiter().GetResult();
                        }
                        catch (CloudApiException ex) when (ex.StatusCode == 404)
                        {
                            // No remote config yet
                        }
                    }

                    if (!settings.ConfigOnly)
                    {
                        ctx.Status("Fetching resources...");
                        remoteResources = apiClient.GetResourcesAsync(cancellationToken).GetAwaiter().GetResult();
                    }
                });

            // Detect conflicts and show diff
            var conflictDetector = new ConflictDetector();
            var conflicts = new List<ConflictDetector.Conflict>();

            // Check configuration conflict
            if (!settings.ResourcesOnly && remoteConfig != null)
            {
                var localConfigJson = Core.Configuration.ConfigurationManager
                    .LoadTeamConfigurationAsync(projectDirectory, cancellationToken)
                    .GetAwaiter()
                    .GetResult();

                var localConfigJsonString = JsonSerializer.Serialize(localConfigJson);
                var configConflict = conflictDetector.DetectConfigurationConflict(localConfigJsonString, remoteConfig.ConfigJson);

                if (configConflict != null)
                {
                    conflicts.Add(configConflict);
                }
            }

            // Check resource conflicts
            ConflictDetector.DiffSummary? diffSummary = null;
            if (!settings.ConfigOnly && remoteResources != null)
            {
                var localResources = GetLocalResources(settings, projectDirectory);
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
                        if (!settings.ResourcesOnly && remoteConfig != null)
                        {
                            var configConflict = conflicts.FirstOrDefault(c => c.Type == ConflictDetector.ConflictType.ConfigurationConflict);
                            if (configConflict?.Resolution != ConflictDetector.ResolutionStrategy.Local)
                            {
                                ctx.Status("Updating configuration...");
                                ApplyConfigurationChanges(projectDirectory, remoteConfig.ConfigJson, cancellationToken);
                            }
                        }

                        if (!settings.ConfigOnly && remoteResources != null)
                        {
                            ctx.Status("Updating resources...");
                            ApplyResourceChanges(projectDirectory, remoteResources, conflicts, cancellationToken);
                        }
                    });

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

    private List<ResourceFile> GetLocalResources(PullCommandSettings settings, string projectDirectory)
    {
        var languages = settings.DiscoverLanguages();
        var localResources = new List<ResourceFile>();

        foreach (var lang in languages)
        {
            var resourceFile = settings.ReadResourceFile(lang);
            var content = SerializeResourceFile(resourceFile);
            var hash = ComputeHash(content);

            localResources.Add(new ResourceFile
            {
                Path = GetRelativePath(lang.FilePath, projectDirectory),
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
        List<ResourceFile> remoteResources,
        List<ConflictDetector.Conflict> conflicts,
        CancellationToken cancellationToken)
    {
        var resourcesPath = Path.Combine(projectDirectory, "Resources");

        foreach (var resource in remoteResources)
        {
            var conflict = conflicts.FirstOrDefault(c => c.Path == resource.Path);

            // Skip if conflict resolved to keep local
            if (conflict?.Resolution == ConflictDetector.ResolutionStrategy.Local)
            {
                continue;
            }

            var fullPath = Path.Combine(resourcesPath, resource.Path);
            var directory = Path.GetDirectoryName(fullPath);

            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write resource content
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

    private string SerializeResourceFile(Core.Models.ResourceFile resourceFile)
    {
        var entries = resourceFile.Entries.ToDictionary(e => e.Key, e => e.Value);
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetRelativePath(string fullPath, string projectDirectory)
    {
        var resourcesPath = Path.Combine(projectDirectory, "Resources");
        if (fullPath.StartsWith(resourcesPath))
        {
            return fullPath.Substring(resourcesPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
