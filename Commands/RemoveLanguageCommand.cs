// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Core;
using LocalizationManager.Core.Backup;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the remove-language command.
/// </summary>
public class RemoveLanguageCommandSettings : BaseCommandSettings
{
    [CommandOption("-c|--culture <CODE>")]
    [Description("Culture code to remove")]
    public required string Culture { get; set; }

    [CommandOption("--base-name <NAME>")]
    [Description("Base resource file name (auto-detected if not specified)")]
    public string? BaseName { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt")]
    public bool SkipConfirmation { get; set; }

    [CommandOption("--no-backup")]
    [Description("Skip creating backups")]
    public bool NoBackup { get; set; }
}

/// <summary>
/// Command to delete a language resource file.
/// </summary>
public class RemoveLanguageCommand : Command<RemoveLanguageCommandSettings>
{
    public override int Execute(CommandContext context, RemoveLanguageCommandSettings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        try
        {
            var manager = new LanguageFileManager();
            manager.SetBackend(settings.GetBackend());

            // Step 1: Validate culture code
            AnsiConsole.MarkupLine($"[yellow]►[/] Validating culture code '{settings.Culture}'...");
            if (!manager.IsValidCultureCode(settings.Culture, out var culture))
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid culture code: {settings.Culture}[/]");
                AnsiConsole.MarkupLine("[grey]Valid examples: en, fr, fr-FR, de-DE, el, ja[/]");
                return 1;
            }

            // Step 2: Discover existing languages
            var languages = settings.DiscoverLanguages();
            if (languages.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]✗ No resource files found in the specified path[/]");
                return 1;
            }

            // Step 3: Determine base name
            string baseName;
            if (!string.IsNullOrEmpty(settings.BaseName))
            {
                baseName = settings.BaseName;
            }
            else
            {
                var baseNames = languages.Select(l => l.BaseName).Distinct().ToList();
                if (baseNames.Count > 1)
                {
                    AnsiConsole.MarkupLine("[red]✗ Multiple resource files found. Please specify --base-name[/]");
                    AnsiConsole.MarkupLine("[grey]Available base names:[/]");
                    foreach (var bn in baseNames)
                    {
                        AnsiConsole.MarkupLine($"  [grey]- {bn}[/]");
                    }
                    return 1;
                }
                baseName = baseNames[0];
            }

            // Step 4: Find the language file to delete
            var targetLanguage = languages.FirstOrDefault(l =>
                l.BaseName == baseName && l.Code == settings.Culture);

            if (targetLanguage == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Language file not found for culture '{settings.Culture}'[/]");
                AnsiConsole.MarkupLine($"[grey]Base name: {baseName}[/]");
                AnsiConsole.MarkupLine("[grey]Available languages:[/]");
                var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
                foreach (var lang in languages.Where(l => l.BaseName == baseName))
                {
                    var code = string.IsNullOrEmpty(lang.Code) ? $"({defaultCode})" : lang.Code;
                    var name = string.IsNullOrEmpty(lang.Code) ? "Default" : lang.Name;
                    AnsiConsole.MarkupLine($"  [grey]- {code} ({name})[/]");
                }
                return 1;
            }

            // Step 5: Parse file to get entry count
            var resourceFile = settings.ReadResourceFile(targetLanguage);
            var entryCount = resourceFile.Entries.Count;

            // Step 6: Show preview and confirm
            var table = new Table();
            table.AddColumn("Property");
            table.AddColumn("Value");
            table.AddRow("File", Path.GetFileName(targetLanguage.FilePath));
            table.AddRow("Language", $"{culture!.DisplayName} ({settings.Culture})");
            table.AddRow("Entries", entryCount.ToString());
            table.AddRow("Path", targetLanguage.FilePath);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]⚠ Warning: This will permanently delete the following file:[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            if (!settings.SkipConfirmation)
            {
                if (!AnsiConsole.Confirm("Delete this language file?", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled[/]");
                    return 0;
                }
            }

            // Step 7: Create backup
            if (!settings.NoBackup)
            {
                var backup = new BackupVersionManager(10);
                var resourcePath = settings.GetResourcePath();
                var metadata = backup.CreateBackupAsync(targetLanguage.FilePath, "remove-language", resourcePath)
                    .GetAwaiter().GetResult();
                AnsiConsole.MarkupLine($"[green]✓[/] Backup created: v{metadata.Version:D3}");
            }

            // Step 8: Delete the file
            manager.DeleteLanguageFile(targetLanguage);

            AnsiConsole.MarkupLine($"[green]✓[/] Deleted {Path.GetFileName(targetLanguage.FilePath)}");
            AnsiConsole.MarkupLine($"[green]✓[/] Removed {culture.DisplayName} ({settings.Culture}) language");

            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("default language"))
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: Cannot delete default language file[/]");
            AnsiConsole.MarkupLine("[grey]Default language files have no culture code in filename[/]");
            AnsiConsole.MarkupLine("[grey]These files serve as fallback for all languages[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message}[/]");
            return 1;
        }
    }
}
