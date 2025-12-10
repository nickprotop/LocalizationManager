// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Reflection;
using LocalizationManager.Commands;
using LocalizationManager.Commands.Backup;
using LocalizationManager.Commands.Cloud;
using LocalizationManager.Commands.Config;
using LocalizationManager.Commands.Remote;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("lrm");
    config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

    // Propagate exceptions so we can handle them intelligently
    config.PropagateExceptions();

    // Validate presence of commands (Spectre.Console.Cli default behavior is to show help on errors)
    config.ValidateExamples();

    // Add helpful description
    config.AddExample(new[] { "validate", "--help" });
    config.AddExample(new[] { "view", "SaveButton", "--format", "json" });
    config.AddExample(new[] { "add", "NewKey", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" });

    config.AddCommand<InitCommand>("init")
        .WithDescription("Initialize a new localization project")
        .WithExample(new[] { "init" })
        .WithExample(new[] { "init", "-i" })
        .WithExample(new[] { "init", "--format", "json", "--default-lang", "en" })
        .WithExample(new[] { "init", "--format", "resx", "--languages", "fr,de,el" });

    config.AddCommand<ConvertCommand>("convert")
        .WithDescription("Convert resource files between formats")
        .WithExample(new[] { "convert", "--to", "json" })
        .WithExample(new[] { "convert", "--to", "resx" })
        .WithExample(new[] { "convert", "--to", "json", "--nested" })
        .WithExample(new[] { "convert", "--from", "resx", "--to", "json", "-o", "./JsonResources" });

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate resource files for missing keys, duplicates, and empty values")
        .WithExample(new[] { "validate" })
        .WithExample(new[] { "validate", "--path", "../Resources" })
        .WithExample(new[] { "validate", "--format", "json" })
        .WithExample(new[] { "validate", "--format", "simple" });

    config.AddCommand<StatsCommand>("stats")
        .WithDescription("Display statistics about resource files and translation coverage")
        .WithExample(new[] { "stats" })
        .WithExample(new[] { "stats", "--path", "./Resources" })
        .WithExample(new[] { "stats", "--format", "json" })
        .WithExample(new[] { "stats", "--format", "simple" });

    config.AddCommand<ViewCommand>("view")
        .WithDescription("View details of a specific localization key")
        .WithExample(new[] { "view", "SaveButton" })
        .WithExample(new[] { "view", "SaveButton", "--show-comments" })
        .WithExample(new[] { "view", "SaveButton", "--format", "json" })
        .WithExample(new[] { "view", "SaveButton", "--format", "simple" });

    config.AddCommand<AddCommand>("add")
        .WithDescription("Add a new localization key to all language files")
        .WithExample(new[] { "add", "NewKey", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" })
        .WithExample(new[] { "add", "SaveButton", "-i" })
        .WithExample(new[] { "add", "SaveButton", "--lang", "default:\"Save\"", "--comment", "Button label for save action" })
        .WithExample(new[] { "add", "NewKey", "-l", "default:\"English value\"", "--no-backup" });

    config.AddCommand<UpdateCommand>("update")
        .WithDescription("Update values for an existing localization key")
        .WithExample(new[] { "update", "SaveButton", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" })
        .WithExample(new[] { "update", "SaveButton", "-i" })
        .WithExample(new[] { "update", "SaveButton", "--comment", "Updated comment" })
        .WithExample(new[] { "update", "SaveButton", "-l", "default:\"New value\"", "-y", "--no-backup" });

    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete a localization key from all language files")
        .WithExample(new[] { "delete", "OldKey" })
        .WithExample(new[] { "delete", "OldKey", "-y" })
        .WithExample(new[] { "delete", "DuplicateKey", "--all-duplicates", "-y" })
        .WithExample(new[] { "delete", "OldKey", "-y", "--no-backup" });

    config.AddCommand<MergeDuplicatesCommand>("merge-duplicates")
        .WithDescription("Merge duplicate occurrences of a key into a single entry")
        .WithExample(new[] { "merge-duplicates", "DuplicateKey" })
        .WithExample(new[] { "merge-duplicates", "DuplicateKey", "--auto-first" })
        .WithExample(new[] { "merge-duplicates", "--all", "--auto-first" })
        .WithExample(new[] { "merge-duplicates", "DuplicateKey", "-y", "--no-backup" });

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Export resource files to various formats (CSV, JSON, or simple text)")
        .WithExample(new[] { "export" })
        .WithExample(new[] { "export", "-o", "translations.csv" })
        .WithExample(new[] { "export", "--format", "json", "-o", "translations.json" })
        .WithExample(new[] { "export", "--format", "simple", "--include-status" });

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import translations from CSV format")
        .WithExample(new[] { "import", "translations.csv" })
        .WithExample(new[] { "import", "translations.csv", "--overwrite" })
        .WithExample(new[] { "import", "translations.csv", "--no-backup" });

    config.AddCommand<EditCommand>("edit")
        .WithDescription("Launch interactive TUI editor for resource files")
        .WithExample(new[] { "edit" })
        .WithExample(new[] { "edit", "--path", "../Resources" })
        .WithExample(new[] { "edit", "-p", "./Resources" });

    config.AddCommand<WebCommand>("web")
        .WithDescription("Start web server hosting API and Blazor WASM UI")
        .WithExample(new[] { "web" })
        .WithExample(new[] { "web", "--path", "../Resources" })
        .WithExample(new[] { "web", "--port", "8080" })
        .WithExample(new[] { "web", "--bind-address", "0.0.0.0", "--port", "5000" })
        .WithExample(new[] { "web", "--source-path", "./src" });

    config.AddCommand<AddLanguageCommand>("add-language")
        .WithDescription("Create a new language resource file")
        .WithExample(new[] { "add-language", "--culture", "fr" })
        .WithExample(new[] { "add-language", "-c", "fr-CA", "--copy-from", "fr" })
        .WithExample(new[] { "add-language", "-c", "de", "--empty" });

    config.AddCommand<RemoveLanguageCommand>("remove-language")
        .WithDescription("Delete a language resource file")
        .WithExample(new[] { "remove-language", "--culture", "fr" })
        .WithExample(new[] { "remove-language", "-c", "fr", "-y" });

    config.AddCommand<ListLanguagesCommand>("list-languages")
        .WithDescription("List all available language files")
        .WithExample(new[] { "list-languages" })
        .WithExample(new[] { "list-languages", "--format", "json" });

    config.AddCommand<TranslateCommand>("translate")
        .WithDescription("Translate resource keys using translation providers (Google, DeepL, LibreTranslate)")
        .WithExample(new[] { "translate" })
        .WithExample(new[] { "translate", "Welcome*" })
        .WithExample(new[] { "translate", "--provider", "deepl", "--target-languages", "fr,de,es" })
        .WithExample(new[] { "translate", "--only-missing", "--dry-run" })
        .WithExample(new[] { "translate", "Error*", "--provider", "google", "--source-language", "en" });

    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Scan source code for localization key references and detect missing or unused keys")
        .WithExample(new[] { "scan" })
        .WithExample(new[] { "scan", "--source-path", "./src" })
        .WithExample(new[] { "scan", "--strict" })
        .WithExample(new[] { "scan", "--show-missing" })
        .WithExample(new[] { "scan", "--show-unused" })
        .WithExample(new[] { "scan", "--format", "json" })
        .WithExample(new[] { "scan", "--exclude", "**/*.g.cs,**/bin/**" });

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Run both validation and code scanning (validate + scan)")
        .WithExample(new[] { "check" })
        .WithExample(new[] { "check", "--source-path", "./src" })
        .WithExample(new[] { "check", "--strict" })
        .WithExample(new[] { "check", "--format", "json" });

    config.AddCommand<ChainCommand>("chain")
        .WithDescription("Execute multiple commands sequentially in a single invocation")
        .WithExample(new[] { "chain", "validate --format json -- translate --only-missing -- export -o output.csv" })
        .WithExample(new[] { "chain", "validate -- scan" })
        .WithExample(new[] { "chain", "import file.csv -- validate -- translate --provider google", "--continue-on-error" })
        .WithExample(new[] { "chain", "validate -- translate --only-missing", "--dry-run" });

    config.AddBranch("config", cfg =>
    {
        cfg.SetDescription("Configuration commands for API keys and settings");

        cfg.AddCommand<LocalizationManager.Commands.Config.SetApiKeyCommand>("set-api-key")
            .WithDescription("Store an API key in the secure credential store")
            .WithExample(new[] { "config", "set-api-key", "--provider", "google", "--key", "your-api-key" })
            .WithExample(new[] { "config", "set-api-key", "-p", "deepl", "-k", "your-api-key" });

        cfg.AddCommand<GetApiKeyCommand>("get-api-key")
            .WithDescription("Check where an API key is configured from")
            .WithExample(new[] { "config", "get-api-key", "--provider", "google" })
            .WithExample(new[] { "config", "get-api-key", "--provider", "deepl" });

        cfg.AddCommand<DeleteApiKeyCommand>("delete-api-key")
            .WithDescription("Delete an API key from the secure credential store")
            .WithExample(new[] { "config", "delete-api-key", "--provider", "google" })
            .WithExample(new[] { "config", "delete-api-key", "-p", "deepl" });

        cfg.AddCommand<ListProvidersCommand>("list-providers")
            .WithDescription("List all translation providers and their configuration status")
            .WithExample(new[] { "config", "list-providers" });
    });

    config.AddBranch("backup", cfg =>
    {
        cfg.SetDescription("Backup management commands for resource files");

        cfg.AddCommand<BackupListCommand>("list")
            .WithDescription("List all backups for resource files")
            .WithExample(new[] { "backup", "list", "--file", "SharedResource.resx" })
            .WithExample(new[] { "backup", "list", "--all" })
            .WithExample(new[] { "backup", "list", "--file", "SharedResource.resx", "--show-details" });

        cfg.AddCommand<BackupCreateCommand>("create")
            .WithDescription("Manually create a backup of resource files")
            .WithExample(new[] { "backup", "create", "--file", "SharedResource.resx" })
            .WithExample(new[] { "backup", "create", "--all" })
            .WithExample(new[] { "backup", "create", "--file", "SharedResource.resx", "--operation", "pre-release" });

        cfg.AddCommand<BackupRestoreCommand>("restore")
            .WithDescription("Restore resource files from a backup")
            .WithExample(new[] { "backup", "restore", "SharedResource.resx", "--version", "3" })
            .WithExample(new[] { "backup", "restore", "SharedResource.resx", "--version", "3", "--preview" })
            .WithExample(new[] { "backup", "restore", "SharedResource.resx", "--version", "3", "--keys", "SaveButton,CancelButton" })
            .WithExample(new[] { "backup", "restore", "SharedResource.resx", "--version", "3", "-y" });

        cfg.AddCommand<BackupDiffCommand>("diff")
            .WithDescription("Show differences between backup versions")
            .WithExample(new[] { "backup", "diff", "SharedResource.resx" })
            .WithExample(new[] { "backup", "diff", "SharedResource.resx", "--from", "2", "--to", "3" })
            .WithExample(new[] { "backup", "diff", "SharedResource.resx", "--format", "json", "--output", "diff.json" })
            .WithExample(new[] { "backup", "diff", "SharedResource.resx", "--show-unchanged" });

        cfg.AddCommand<BackupInfoCommand>("info")
            .WithDescription("Display detailed information about a specific backup")
            .WithExample(new[] { "backup", "info", "SharedResource.resx", "3" });

        cfg.AddCommand<BackupPruneCommand>("prune")
            .WithDescription("Remove old backups based on retention policy")
            .WithExample(new[] { "backup", "prune", "--file", "SharedResource.resx", "--keep", "5" })
            .WithExample(new[] { "backup", "prune", "--file", "SharedResource.resx", "--older-than", "30" })
            .WithExample(new[] { "backup", "prune", "--all", "--keep", "10", "--dry-run" })
            .WithExample(new[] { "backup", "prune", "--file", "SharedResource.resx", "--version", "1", "-y" });
    });

    config.AddBranch("cloud", cfg =>
    {
        cfg.SetDescription("Cloud synchronization commands");

        cfg.AddCommand<CloudInitCommand>("init")
            .WithDescription("Connect local project to cloud (authenticate and link/create project)")
            .WithExample(new[] { "cloud", "init" })
            .WithExample(new[] { "cloud", "init", "--host", "staging.lrm.cloud" })
            .WithExample(new[] { "cloud", "init", "--name", "my-project" });

        cfg.AddCommand<PushCommand>("push")
            .WithDescription("Push local changes (resources + lrm.json) to the cloud")
            .WithExample(new[] { "cloud", "push" })
            .WithExample(new[] { "cloud", "push", "-m", "Update translations" })
            .WithExample(new[] { "cloud", "push", "--dry-run" })
            .WithExample(new[] { "cloud", "push", "--config-only" })
            .WithExample(new[] { "cloud", "push", "--resources-only" });

        cfg.AddCommand<PullCommand>("pull")
            .WithDescription("Pull remote changes (resources + lrm.json) from the cloud")
            .WithExample(new[] { "cloud", "pull" })
            .WithExample(new[] { "cloud", "pull", "--dry-run" })
            .WithExample(new[] { "cloud", "pull", "--force" })
            .WithExample(new[] { "cloud", "pull", "--strategy", "remote" })
            .WithExample(new[] { "cloud", "pull", "--config-only" })
            .WithExample(new[] { "cloud", "pull", "--resources-only" });

        cfg.AddCommand<StatusCommand>("status")
            .WithDescription("Show cloud sync status and recent activity")
            .WithExample(new[] { "cloud", "status" })
            .WithExample(new[] { "cloud", "status", "--format", "json" });

        cfg.AddCommand<LoginCommand>("login")
            .WithDescription("Authenticate with the cloud using email and password")
            .WithExample(new[] { "cloud", "login" })
            .WithExample(new[] { "cloud", "login", "--email", "user@example.com" })
            .WithExample(new[] { "cloud", "login", "--host", "staging.lrm.cloud" });

        cfg.AddCommand<LogoutCommand>("logout")
            .WithDescription("Clear stored authentication tokens")
            .WithExample(new[] { "cloud", "logout" })
            .WithExample(new[] { "cloud", "logout", "--host", "staging.lrm.cloud" })
            .WithExample(new[] { "cloud", "logout", "--all" });

        cfg.AddCommand<SetTokenCommand>("set-token")
            .WithDescription("Manually set an authentication token for cloud access")
            .WithExample(new[] { "cloud", "set-token", "--host", "lrm.cloud", "--token", "your-token" })
            .WithExample(new[] { "cloud", "set-token" });

        cfg.AddCommand<LocalizationManager.Commands.Cloud.SetApiKeyCommand>("set-api-key")
            .WithDescription("Store a CLI API key for cloud authentication")
            .WithExample(new[] { "cloud", "set-api-key" })
            .WithExample(new[] { "cloud", "set-api-key", "--key", "lrm_abc123..." })
            .WithExample(new[] { "cloud", "set-api-key", "--host", "staging.lrm.cloud" })
            .WithExample(new[] { "cloud", "set-api-key", "--remove" });

        cfg.AddBranch("remote", remoteCfg =>
        {
            remoteCfg.SetDescription("Remote URL configuration for cloud synchronization");

            remoteCfg.AddCommand<RemoteSetCommand>("set")
                .WithDescription("Set the remote URL for cloud synchronization")
                .WithExample(new[] { "cloud", "remote", "set", "https://lrm.cloud/acme-corp/mobile-app" })
                .WithExample(new[] { "cloud", "remote", "set", "https://lrm.cloud/@john/personal-project" })
                .WithExample(new[] { "cloud", "remote", "set", "https://lrm.cloud/my-org/project", "--enable" })
                .WithExample(new[] { "cloud", "remote", "set", "https://staging.lrm.cloud/test-org/app" });

            remoteCfg.AddCommand<RemoteGetCommand>("get")
                .WithDescription("Display the current remote URL configuration")
                .WithExample(new[] { "cloud", "remote", "get" })
                .WithExample(new[] { "cloud", "remote", "get", "--format", "json" })
                .WithExample(new[] { "cloud", "remote", "get", "--format", "simple" });

            remoteCfg.AddCommand<RemoteUnsetCommand>("unset")
                .WithDescription("Remove the remote URL configuration")
                .WithExample(new[] { "cloud", "remote", "unset" })
                .WithExample(new[] { "cloud", "remote", "unset", "-y" });
        });
    });
});

int result;

try
{
    result = app.Run(args);
}
catch (CommandParseException ex)
{
    // Parse error - could be unknown command or argument error
    AnsiConsole.MarkupLine($"[red]{ex.Message.Replace("[", "[[").Replace("]", "]]")}[/]");
    AnsiConsole.WriteLine();

    // Check if this is a command-not-found error vs argument error
    // If the error mentions "Unknown command", show main help
    // Otherwise, show command-specific help if we can detect the command name
    if (ex.Message.Contains("Unknown command") || ex.Message.Contains("No such command"))
    {
        AnsiConsole.MarkupLine("[dim]Run 'lrm --help' to see available commands.[/]");
    }
    else
    {
        // Try to extract command name from args to show command-specific help
        var commandName = args.FirstOrDefault(a => !a.StartsWith("-"));
        if (!string.IsNullOrEmpty(commandName))
        {
            AnsiConsole.MarkupLine($"[dim]Run 'lrm {commandName} --help' for command-specific usage information.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Run 'lrm --help' for usage information.[/]");
        }
    }

    Console.WriteLine();
    return 1;
}
catch (Exception ex)
{
    // Other exceptions
    AnsiConsole.MarkupLine($"[red]Error: {ex.Message.Replace("[", "[[").Replace("]", "]]")}[/]");
    AnsiConsole.WriteLine();
    Console.WriteLine();
    return 1;
}

// Show help hint on error (common CLI practice - like git, docker, etc.)
if (result != 0)
{
    AnsiConsole.WriteLine();

    // Try to detect which command was run for contextual help
    var commandName = args.FirstOrDefault(a => !a.StartsWith("-"));
    if (!string.IsNullOrEmpty(commandName))
    {
        AnsiConsole.MarkupLine($"[dim]Run 'lrm {commandName} --help' for more information.[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[dim]Run 'lrm --help' for usage information.[/]");
    }
}

// Add newline after output (better UX - prompt not right after output)
Console.WriteLine();

return result;
