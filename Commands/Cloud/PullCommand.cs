// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
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

    [CommandOption("--include-unapproved")]
    [Description("Include translations that haven't been approved yet (when project has review workflow enabled)")]
    [DefaultValue(false)]
    public bool IncludeUnapproved { get; set; }
}

/// <summary>
/// Command to pull remote changes (resources + lrm.json) from the cloud using key-level sync.
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
            var (strategy, isValidStrategy) = ParseStrategy(settings.Strategy);
            if (!isValidStrategy)
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
                // Enable auto-refresh for JWT authentication
                apiClient.EnableAutoRefresh(projectDirectory);
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

            // Load local configuration for backend options
            var config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();

            // Load sync state
            var syncStateResult = SyncStateManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            var syncState = syncStateResult.State;

            // Handle corrupted or legacy sync state
            if (syncStateResult.WasCorrupted)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Sync state was corrupted - treating as first pull[/]");
                syncState = null;
            }
            else if (syncStateResult.NeedsMigration)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Upgrading from file-based sync to key-level sync[/]");
            }

            // Get backend for the format
            var backendFactory = new Core.Backends.ResourceBackendFactory();
            Core.Abstractions.IResourceBackend backend;
            try
            {
                backend = backendFactory.GetBackend(remoteProject!.Format, config);
            }
            catch (NotSupportedException)
            {
                AnsiConsole.MarkupLine($"[red]✗ Unsupported format: {remoteProject.Format}[/]");
                return 1;
            }

            // Extract local entries
            var languages = backend.Discovery.DiscoverLanguages(projectDirectory);
            var extractor = new LocalEntryExtractor(backend);

            List<LocalEntry> localEntries = new();
            AnsiConsole.Status()
                .Start("Reading local files...", ctx =>
                {
                    localEntries = extractor.ExtractEntriesAsync(languages, cancellationToken).GetAwaiter().GetResult();
                });

            // Fetch remote entries
            KeySyncPullResponse? pullResponse = null;
            AnsiConsole.Status()
                .Start("Fetching remote data...", ctx =>
                {
                    pullResponse = apiClient.KeySyncPullAsync(cancellationToken: cancellationToken).GetAwaiter().GetResult();
                });

            if (pullResponse == null)
            {
                throw new InvalidOperationException("Failed to pull resources from server");
            }

            AnsiConsole.MarkupLine($"[dim]Remote: {pullResponse.Total} entries[/]");

            // Perform three-way merge
            var merger = new KeyLevelMerger();
            MergeResult mergeResult;

            if (syncState == null || !syncState.Entries.Any())
            {
                // First pull - accept all remote
                mergeResult = merger.MergeForFirstPull(pullResponse.Entries);
                AnsiConsole.MarkupLine("[dim]First pull - accepting all remote entries[/]");
            }
            else
            {
                // Normal merge
                mergeResult = merger.MergeForPull(localEntries, pullResponse.Entries, syncState);
            }

            // Handle config merge
            ConfigMergeResult? configMergeResult = null;
            if (!settings.ResourcesOnly && pullResponse.Config != null)
            {
                var configMerger = new ConfigMerger();
                var localConfigJson = File.Exists(localConfigPath) ? File.ReadAllText(localConfigPath) : null;
                var localProps = localConfigJson != null
                    ? configMerger.ExtractConfigProperties(localConfigJson)
                    : new Dictionary<string, (string Value, string Hash)>();

                configMergeResult = configMerger.MergeForPull(localProps, pullResponse.Config, syncState);
            }

            // Show changes summary
            ShowMergeSummary(mergeResult, configMergeResult, settings);

            // Handle conflicts
            if (mergeResult.HasConflicts)
            {
                if (settings.Force || strategy == ResolutionChoice.Remote)
                {
                    // Force accept remote for all conflicts
                    AnsiConsole.MarkupLine($"[yellow]⚠ {mergeResult.Conflicts.Count} conflict(s) - accepting remote values[/]");

                    var resolutions = mergeResult.Conflicts.Select(c => new ConflictResolution
                    {
                        Key = c.Key,
                        Lang = c.Lang,
                        TargetType = ResolutionTargetType.Entry,
                        Resolution = ResolutionChoice.Remote
                    }).ToList();

                    var localEntriesDict = localEntries.ToDictionary(e => (e.Key, e.Lang), e => e);
                    mergeResult = merger.ApplyResolutions(mergeResult, resolutions, localEntriesDict);
                }
                else if (strategy == ResolutionChoice.Local)
                {
                    // Force keep local for all conflicts
                    AnsiConsole.MarkupLine($"[yellow]⚠ {mergeResult.Conflicts.Count} conflict(s) - keeping local values[/]");

                    var resolutions = mergeResult.Conflicts.Select(c => new ConflictResolution
                    {
                        Key = c.Key,
                        Lang = c.Lang,
                        TargetType = ResolutionTargetType.Entry,
                        Resolution = ResolutionChoice.Local
                    }).ToList();

                    var localEntriesDict = localEntries.ToDictionary(e => (e.Key, e.Lang), e => e);
                    mergeResult = merger.ApplyResolutions(mergeResult, resolutions, localEntriesDict);
                }
                else if (strategy == ResolutionChoice.Skip)
                {
                    AnsiConsole.MarkupLine($"[red]✗ {mergeResult.Conflicts.Count} conflict(s) detected - aborting[/]");
                    ShowConflicts(mergeResult.Conflicts);
                    return 1;
                }
                else
                {
                    // Interactive resolution
                    if (!ResolveConflictsInteractively(merger, mergeResult, localEntries))
                    {
                        AnsiConsole.MarkupLine("[yellow]Pull cancelled[/]");
                        return 0;
                    }
                }
            }

            // Check if there's anything to do
            if (mergeResult.ToWrite.Count == 0 && configMergeResult?.ToWrite.Count == 0)
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
                // Update config FIRST so backend uses correct settings for file regeneration
                if (!settings.ResourcesOnly && configMergeResult?.ToWrite.Count > 0)
                {
                    AnsiConsole.Status()
                        .Start("Updating configuration...", ctx =>
                        {
                            var configMerger = new ConfigMerger();
                            var localConfigJson = File.Exists(localConfigPath) ? File.ReadAllText(localConfigPath) : "{}";
                            var newConfigJson = configMerger.ApplyConfigChanges(localConfigJson, configMergeResult.ToWrite);
                            File.WriteAllText(localConfigPath, newConfigJson);
                        });

                    // Reload config and backend after config update
                    config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
                    backend = backendFactory.GetBackend(remoteProject!.Format, config);
                    // Re-discover languages with updated config
                    languages = backend.Discovery.DiscoverLanguages(projectDirectory);
                }

                AnsiConsole.Status()
                    .Start("Applying changes...", ctx =>
                    {
                        // Update resources (now with correct backend from updated config)
                        if (!settings.ConfigOnly && mergeResult.ToWrite.Count > 0)
                        {
                            ctx.Status("Regenerating resource files...");
                            var regenerator = new FileRegenerator(backend, projectDirectory);
                            var regenResult = regenerator.RegenerateFilesAsync(
                                mergeResult.ToWrite,
                                languages,
                                cancellationToken).GetAwaiter().GetResult();

                            if (!regenResult.Success)
                            {
                                throw new Exception($"Failed to regenerate files: {regenResult.Error}");
                            }
                        }
                    });

                // Update sync state
                var newSyncState = BuildNewSyncState(mergeResult, configMergeResult, localEntries);
                SyncStateManager.SaveAsync(projectDirectory, newSyncState, cancellationToken).GetAwaiter().GetResult();

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green bold]✓ Pull completed successfully![/]");

                if (mergeResult.ToWrite.Count > 0)
                {
                    AnsiConsole.MarkupLine($"  [dim]Updated: {mergeResult.ToWrite.Count} entries[/]");
                }
                if (mergeResult.AutoMerged > 0)
                {
                    AnsiConsole.MarkupLine($"  [dim]Auto-merged: {mergeResult.AutoMerged} entries[/]");
                }
                if (configMergeResult?.ToWrite.Count > 0)
                {
                    AnsiConsole.MarkupLine($"  [dim]Config properties: {configMergeResult.ToWrite.Count}[/]");
                }

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

    private void ShowMergeSummary(MergeResult mergeResult, ConfigMergeResult? configMergeResult, PullCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[blue]Merge summary:[/]");

        if (mergeResult.AutoMerged > 0)
        {
            AnsiConsole.MarkupLine($"  [green]↓ {mergeResult.AutoMerged} entry(ies) to update from remote[/]");
        }
        if (mergeResult.Unchanged > 0)
        {
            AnsiConsole.MarkupLine($"  [dim]= {mergeResult.Unchanged} entry(ies) unchanged[/]");
        }
        if (mergeResult.Conflicts.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [yellow]! {mergeResult.Conflicts.Count} conflict(s) need resolution[/]");
        }
        if (configMergeResult?.ToWrite.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [blue]~ {configMergeResult.ToWrite.Count} config property(ies) to update[/]");
        }
        if (configMergeResult?.Conflicts.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [yellow]! {configMergeResult.Conflicts.Count} config conflict(s)[/]");
        }

        AnsiConsole.WriteLine();
    }

    private void ShowConflicts(List<EntryConflict> conflicts)
    {
        AnsiConsole.WriteLine();
        foreach (var conflict in conflicts.Take(10))
        {
            AnsiConsole.MarkupLine($"  [yellow]• {conflict.Key}[/] ({conflict.Lang})");
            AnsiConsole.MarkupLine($"    Local:  \"{conflict.LocalValue?.EscapeMarkup() ?? "(missing)"}\"");
            AnsiConsole.MarkupLine($"    Remote: \"{conflict.RemoteValue?.EscapeMarkup() ?? "(deleted)"}\"");
        }
        if (conflicts.Count > 10)
        {
            AnsiConsole.MarkupLine($"  [dim]... and {conflicts.Count - 10} more[/]");
        }
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Use --force or --strategy to resolve conflicts[/]");
    }

    private bool ResolveConflictsInteractively(KeyLevelMerger merger, MergeResult mergeResult, List<LocalEntry> localEntries)
    {
        AnsiConsole.MarkupLine($"[yellow bold]Conflicts detected: {mergeResult.Conflicts.Count}[/]");
        AnsiConsole.WriteLine();

        // Offer batch resolution first
        var batchChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("How do you want to resolve conflicts?")
                .AddChoices(new[]
                {
                    "Resolve each interactively",
                    "Use remote for all",
                    "Keep local for all",
                    "Abort"
                }));

        if (batchChoice == "Abort")
        {
            return false;
        }

        var resolutions = new List<ConflictResolution>();

        if (batchChoice == "Use remote for all")
        {
            resolutions = mergeResult.Conflicts.Select(c => new ConflictResolution
            {
                Key = c.Key,
                Lang = c.Lang,
                TargetType = ResolutionTargetType.Entry,
                Resolution = ResolutionChoice.Remote
            }).ToList();
        }
        else if (batchChoice == "Keep local for all")
        {
            resolutions = mergeResult.Conflicts.Select(c => new ConflictResolution
            {
                Key = c.Key,
                Lang = c.Lang,
                TargetType = ResolutionTargetType.Entry,
                Resolution = ResolutionChoice.Local,
                EditedValue = c.LocalValue
            }).ToList();
        }
        else
        {
            // Interactive resolution
            for (int i = 0; i < mergeResult.Conflicts.Count; i++)
            {
                var conflict = mergeResult.Conflicts[i];

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[yellow]Conflict {i + 1}/{mergeResult.Conflicts.Count}:[/] {conflict.Key.EscapeMarkup()} ({conflict.Lang})");
                AnsiConsole.MarkupLine($"  [green]LOCAL:[/]  \"{conflict.LocalValue?.EscapeMarkup() ?? "(missing)"}\"");
                AnsiConsole.MarkupLine($"  [blue]REMOTE:[/] \"{conflict.RemoteValue?.EscapeMarkup() ?? "(deleted)"}\"");
                if (conflict.RemoteUpdatedAt.HasValue)
                {
                    AnsiConsole.MarkupLine($"  [dim]Remote updated: {conflict.RemoteUpdatedAt.Value:g}[/]");
                }

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Resolution:")
                        .AddChoices(new[]
                        {
                            "[R]emote - Use remote version",
                            "[L]ocal - Keep local version",
                            "[E]dit - Enter custom value",
                            "[S]kip - Abort entire pull"
                        }));

                var resolution = new ConflictResolution
                {
                    Key = conflict.Key,
                    Lang = conflict.Lang,
                    TargetType = ResolutionTargetType.Entry
                };

                if (choice.StartsWith("[R]"))
                {
                    resolution.Resolution = ResolutionChoice.Remote;
                }
                else if (choice.StartsWith("[L]"))
                {
                    resolution.Resolution = ResolutionChoice.Local;
                    resolution.EditedValue = conflict.LocalValue;
                }
                else if (choice.StartsWith("[E]"))
                {
                    var editedValue = AnsiConsole.Ask<string>("Enter new value:");
                    resolution.Resolution = ResolutionChoice.Edit;
                    resolution.EditedValue = editedValue;
                }
                else
                {
                    return false;
                }

                resolutions.Add(resolution);
            }
        }

        // Apply resolutions
        var localEntriesDict = localEntries.ToDictionary(e => (e.Key, e.Lang), e => e);
        var resolvedResult = merger.ApplyResolutions(mergeResult, resolutions, localEntriesDict);

        // Copy resolved entries to original result
        mergeResult.ToWrite.Clear();
        mergeResult.ToWrite.AddRange(resolvedResult.ToWrite);
        mergeResult.Conflicts.Clear();

        return true;
    }

    private (ResolutionChoice? Choice, bool IsValid) ParseStrategy(string strategy)
    {
        return strategy.ToLowerInvariant() switch
        {
            "local" => (ResolutionChoice.Local, true),
            "remote" => (ResolutionChoice.Remote, true),
            "prompt" => (null, true), // Interactive - null choice but valid
            "abort" => (ResolutionChoice.Skip, true),
            _ => (null, false) // Invalid strategy
        };
    }

    private SyncState BuildNewSyncState(MergeResult mergeResult, ConfigMergeResult? configMergeResult, List<LocalEntry> localEntries)
    {
        var newState = new SyncState
        {
            Version = 2,
            Timestamp = DateTime.UtcNow,
            Entries = new Dictionary<string, Dictionary<string, string>>(),
            ConfigProperties = new Dictionary<string, string>()
        };

        // Add hashes from merged entries
        foreach (var (key, lang, hash) in mergeResult.NewHashes.GetAllEntries())
        {
            if (!newState.Entries.ContainsKey(key))
            {
                newState.Entries[key] = new Dictionary<string, string>();
            }
            newState.Entries[key][lang] = hash;
        }

        // Add hashes from written entries (remote entries accepted)
        foreach (var entry in mergeResult.ToWrite)
        {
            if (!newState.Entries.ContainsKey(entry.Key))
            {
                newState.Entries[entry.Key] = new Dictionary<string, string>();
            }
            newState.Entries[entry.Key][entry.Lang] = entry.Hash;
        }

        // Add hashes from local entries that weren't changed
        foreach (var entry in localEntries)
        {
            if (!newState.Entries.ContainsKey(entry.Key))
            {
                newState.Entries[entry.Key] = new Dictionary<string, string>();
            }
            if (!newState.Entries[entry.Key].ContainsKey(entry.Lang))
            {
                newState.Entries[entry.Key][entry.Lang] = entry.Hash;
            }
        }

        // Add config hashes
        if (configMergeResult != null)
        {
            foreach (var (path, hash) in configMergeResult.NewHashes)
            {
                newState.ConfigProperties[path] = hash;
            }
        }

        return newState;
    }
}
