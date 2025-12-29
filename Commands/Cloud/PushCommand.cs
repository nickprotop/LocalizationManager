// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

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

    [CommandOption("-i|--interactive")]
    [Description("Interactively resolve conflicts one by one")]
    [DefaultValue(false)]
    public bool Interactive { get; set; }
}

/// <summary>
/// Command to push local changes (resources + lrm.json) to the cloud using key-level sync.
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

            // Load and validate configuration (lrm.json)
            var config = Core.Configuration.ConfigurationManager.LoadConfigurationAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
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

            // Validate format compatibility
            var syncValidator = new CloudSyncValidator(projectDirectory);
            var syncValidation = syncValidator.ValidateForPush(config, remoteProject!);

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
                AnsiConsole.MarkupLine("[red]✗ Cannot push due to compatibility issues:[/]");
                foreach (var error in syncValidation.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]• {error.EscapeMarkup()}[/]");
                }
                return 1;
            }

            // Load sync state
            var syncStateResult = SyncStateManager.LoadAsync(projectDirectory, cancellationToken).GetAwaiter().GetResult();
            var syncState = syncStateResult.State;

            // Handle corrupted or legacy sync state
            if (syncStateResult.WasCorrupted)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Sync state was corrupted - treating as first push[/]");
                syncState = null;
            }
            else if (syncStateResult.NeedsMigration)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Upgrading from file-based sync to key-level sync[/]");
                // Legacy sync state will be migrated after successful push
            }

            // Get backend for the local format (client-agnostic: format is determined locally)
            var backendFactory = new Core.Backends.ResourceBackendFactory();
            Core.Abstractions.IResourceBackend backend;
            try
            {
                // Use local config format or auto-detect from files
                if (!string.IsNullOrEmpty(config.ResourceFormat))
                {
                    backend = backendFactory.GetBackend(config.ResourceFormat, config);
                }
                else
                {
                    backend = backendFactory.ResolveFromPath(projectDirectory, config);
                }
            }
            catch (NotSupportedException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ {ex.Message}[/]");
                AnsiConsole.MarkupLine("[dim]Set 'format' in lrm.json or ensure resource files exist.[/]");
                return 1;
            }

            // Extract local entries
            var extractor = new LocalEntryExtractor(backend);
            var languages = backend.Discovery.DiscoverLanguages(projectDirectory);

            List<LocalEntry> localEntries = new();
            AnsiConsole.Status()
                .Start("Reading local files...", ctx =>
                {
                    localEntries = extractor.ExtractEntriesAsync(languages, cancellationToken).GetAwaiter().GetResult();
                });

            AnsiConsole.MarkupLine($"[dim]Found {localEntries.Count} entries across {languages.Count()} language(s)[/]");

            // Compute push changes using KeyLevelMerger
            var merger = new KeyLevelMerger();
            var pushChanges = merger.ComputePushChanges(localEntries, syncState);

            // Show summary
            var hasEntryChanges = pushChanges.Entries.Any() || pushChanges.Deletions.Count > 0;

            if (!hasEntryChanges)
            {
                AnsiConsole.MarkupLine("[green]✓ Already up to date - nothing to push[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[blue]Changes to push:[/]");
            var entryCount = pushChanges.Entries.Count();
            if (entryCount > 0)
            {
                AnsiConsole.MarkupLine($"  [green]+ {entryCount} entry change(s)[/]");
            }
            if (pushChanges.Deletions.Count > 0)
            {
                AnsiConsole.MarkupLine($"  [red]- {pushChanges.Deletions.Count} deletion(s)[/]");
            }
            AnsiConsole.WriteLine();

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[yellow]Dry run - no changes will be made[/]");
                ShowDetailedChanges(pushChanges);
                return 0;
            }

            // Build push request
            var request = new KeySyncPushRequest
            {
                Message = settings.Message,
                Entries = pushChanges.Entries.ToList(),
                Deletions = pushChanges.Deletions
            };

            // Push to server
            KeySyncPushResponse? response = null;
            AnsiConsole.Status()
                .Start("Pushing changes...", ctx =>
                {
                    response = apiClient.KeySyncPushAsync(request, cancellationToken).GetAwaiter().GetResult();
                });

            // Handle conflicts
            if (response!.Conflicts.Count > 0)
            {
                if (settings.Force)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ {response.Conflicts.Count} conflict(s) - forcing local values[/]");

                    // Resolve all conflicts as local
                    var resolutionRequest = new ConflictResolutionRequest
                    {
                        Resolutions = response.Conflicts.Select(c => new ConflictResolution
                        {
                            Key = c.Key,
                            Lang = c.Lang,
                            TargetType = ResolutionTargetType.Entry,
                            Resolution = ResolutionChoice.Local,
                            EditedValue = c.LocalValue
                        }).ToList()
                    };

                    ConflictResolutionResponse? resolveResponse = null;
                    AnsiConsole.Status()
                        .Start("Resolving conflicts...", ctx =>
                        {
                            resolveResponse = apiClient.KeySyncResolveAsync(resolutionRequest, cancellationToken).GetAwaiter().GetResult();
                        });

                    response.Applied += resolveResponse!.Applied;

                    // Merge resolution hashes into response
                    foreach (var (key, langHashes) in resolveResponse.NewHashes)
                    {
                        if (!response.NewEntryHashes.ContainsKey(key))
                        {
                            response.NewEntryHashes[key] = new Dictionary<string, string>();
                        }
                        foreach (var (lang, hash) in langHashes)
                        {
                            response.NewEntryHashes[key][lang] = hash;
                        }
                    }
                }
                else if (settings.Interactive)
                {
                    // Interactive conflict resolution
                    var resolutions = ResolveConflictsInteractively(response.Conflicts, apiClient, cancellationToken);
                    if (resolutions == null)
                    {
                        AnsiConsole.MarkupLine("[dim]Operation cancelled.[/]");
                        return 1;
                    }

                    var resolutionRequest = new ConflictResolutionRequest { Resolutions = resolutions };

                    ConflictResolutionResponse? resolveResponse = null;
                    AnsiConsole.Status()
                        .Start("Applying resolutions...", ctx =>
                        {
                            resolveResponse = apiClient.KeySyncResolveAsync(resolutionRequest, cancellationToken).GetAwaiter().GetResult();
                        });

                    response.Applied += resolveResponse!.Applied;

                    // Merge resolution hashes into response
                    foreach (var (key, langHashes) in resolveResponse.NewHashes)
                    {
                        if (!response.NewEntryHashes.ContainsKey(key))
                        {
                            response.NewEntryHashes[key] = new Dictionary<string, string>();
                        }
                        foreach (var (lang, hash) in langHashes)
                        {
                            response.NewEntryHashes[key][lang] = hash;
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ {response.Conflicts.Count} conflict(s) detected[/]");
                    AnsiConsole.WriteLine();

                    foreach (var conflict in response.Conflicts)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]• {conflict.Key}[/] ({conflict.Lang})");
                        AnsiConsole.MarkupLine($"    Local:  \"{conflict.LocalValue?.EscapeMarkup() ?? "(deleted)"}\"");
                        AnsiConsole.MarkupLine($"    Remote: \"{conflict.RemoteValue?.EscapeMarkup() ?? "(deleted)"}\"");
                        if (conflict.RemoteUpdatedAt.HasValue)
                        {
                            AnsiConsole.MarkupLine($"    [dim]Remote updated: {conflict.RemoteUpdatedAt.Value:g}[/]");
                        }
                    }

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Use --interactive to resolve conflicts, --force to override all, or pull first to merge.[/]");
                    return 1;
                }
            }

            // Update sync state with new hashes
            var newSyncState = UpdateSyncState(syncState, response, localEntries);
            SyncStateManager.SaveAsync(projectDirectory, newSyncState, cancellationToken).GetAwaiter().GetResult();

            // Show success
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green bold]✓ Push completed successfully![/]");

            if (response.Applied > 0)
            {
                AnsiConsole.MarkupLine($"  [dim]Applied: {response.Applied} entries[/]");
            }
            if (response.Deleted > 0)
            {
                AnsiConsole.MarkupLine($"  [dim]Deleted: {response.Deleted} entries[/]");
            }

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

    private void ShowDetailedChanges(PushChanges pushChanges)
    {
        var entries = pushChanges.Entries.ToList();
        if (entries.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Entry changes:[/]");
            foreach (var entry in entries.Take(20))
            {
                var status = entry.BaseHash == null ? "[green]+[/]" : "[yellow]~[/]";
                AnsiConsole.MarkupLine($"  {status} {entry.Key.EscapeMarkup()} ({entry.Lang})");
            }
            if (entries.Count > 20)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {entries.Count - 20} more[/]");
            }
        }

        if (pushChanges.Deletions.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Deletions:[/]");
            foreach (var deletion in pushChanges.Deletions.Take(20))
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {deletion.Key.EscapeMarkup()} ({deletion.Lang ?? "all languages"})");
            }
            if (pushChanges.Deletions.Count > 20)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {pushChanges.Deletions.Count - 20} more[/]");
            }
        }
    }

    private SyncState UpdateSyncState(
        SyncState? existing,
        KeySyncPushResponse response,
        List<LocalEntry> localEntries)
    {
        var newState = new SyncState
        {
            Version = 2,
            Timestamp = DateTime.UtcNow,
            Entries = existing?.Entries ?? new Dictionary<string, Dictionary<string, string>>()
        };

        // Update entry hashes from response
        foreach (var (key, langHashes) in response.NewEntryHashes)
        {
            if (!newState.Entries.ContainsKey(key))
            {
                newState.Entries[key] = new Dictionary<string, string>();
            }
            foreach (var (lang, hash) in langHashes)
            {
                newState.Entries[key][lang] = hash;
            }
        }

        // For entries that were pushed but not modified on server (hash unchanged),
        // update with local hashes
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

        return newState;
    }

    private List<ConflictResolution>? ResolveConflictsInteractively(
        List<EntryConflict> conflicts,
        CloudApiClient apiClient,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[yellow bold]Conflicts detected: {conflicts.Count}[/]");
        AnsiConsole.WriteLine();

        // Offer batch resolution first
        var batchChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("How do you want to resolve conflicts?")
                .AddChoices(new[]
                {
                    "Resolve each interactively",
                    "Use local for all",
                    "Use remote for all",
                    "Abort"
                }));

        if (batchChoice == "Abort")
        {
            return null;
        }

        if (batchChoice == "Use local for all")
        {
            return conflicts.Select(c => new ConflictResolution
            {
                Key = c.Key,
                Lang = c.Lang,
                TargetType = ResolutionTargetType.Entry,
                Resolution = ResolutionChoice.Local,
                EditedValue = c.LocalValue
            }).ToList();
        }

        if (batchChoice == "Use remote for all")
        {
            return conflicts.Select(c => new ConflictResolution
            {
                Key = c.Key,
                Lang = c.Lang,
                TargetType = ResolutionTargetType.Entry,
                Resolution = ResolutionChoice.Remote
            }).ToList();
        }

        // Interactive resolution
        var resolutions = new List<ConflictResolution>();

        for (int i = 0; i < conflicts.Count; i++)
        {
            var conflict = conflicts[i];

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Conflict {i + 1}/{conflicts.Count}:[/] {conflict.Key.EscapeMarkup()} ({conflict.Lang})");
            AnsiConsole.MarkupLine($"  [green]LOCAL:[/]  \"{conflict.LocalValue?.EscapeMarkup() ?? "(deleted)"}\"");
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
                        "[L]ocal - Use local version",
                        "[R]emote - Use remote version",
                        "[E]dit - Enter custom value",
                        "[S]kip - Abort entire push"
                    }));

            var resolution = new ConflictResolution
            {
                Key = conflict.Key,
                Lang = conflict.Lang,
                TargetType = ResolutionTargetType.Entry
            };

            if (choice.StartsWith("[L]"))
            {
                resolution.Resolution = ResolutionChoice.Local;
                resolution.EditedValue = conflict.LocalValue;
            }
            else if (choice.StartsWith("[R]"))
            {
                resolution.Resolution = ResolutionChoice.Remote;
            }
            else if (choice.StartsWith("[E]"))
            {
                var editedValue = AnsiConsole.Ask<string>("Enter new value:");
                resolution.Resolution = ResolutionChoice.Edit;
                resolution.EditedValue = editedValue;
            }
            else
            {
                return null;
            }

            resolutions.Add(resolution);
        }

        return resolutions;
    }
}
