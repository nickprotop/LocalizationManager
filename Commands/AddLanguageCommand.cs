// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Core;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the add-language command.
/// </summary>
public class AddLanguageCommandSettings : BaseCommandSettings
{
    [CommandOption("-c|--culture <CODE>")]
    [Description("Culture code (e.g., fr, fr-FR, de, el)")]
    public required string Culture { get; set; }

    [CommandOption("--base-name <NAME>")]
    [Description("Base resource file name (auto-detected if not specified)")]
    public string? BaseName { get; set; }

    [CommandOption("--copy-from <CODE>")]
    [Description("Copy entries from specific language (default: default language)")]
    public string? CopyFrom { get; set; }

    [CommandOption("--empty")]
    [Description("Create empty language file with no entries")]
    public bool Empty { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts")]
    public bool SkipConfirmation { get; set; }
}

/// <summary>
/// Command to create a new language resource file.
/// </summary>
public class AddLanguageCommand : Command<AddLanguageCommandSettings>
{
    public override int Execute(CommandContext context, AddLanguageCommandSettings settings, CancellationToken cancellationToken = default)
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
            AnsiConsole.MarkupLine($"[green]✓[/] Culture code valid: {culture!.DisplayName}");

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
                // Verify base name exists
                if (!languages.Any(l => l.BaseName == baseName))
                {
                    AnsiConsole.MarkupLine($"[red]✗ Base name '{baseName}' not found[/]");
                    AnsiConsole.MarkupLine("[grey]Available base names:[/]");
                    foreach (var bn in languages.Select(l => l.BaseName).Distinct())
                    {
                        AnsiConsole.MarkupLine($"  [grey]- {bn}[/]");
                    }
                    return 1;
                }
            }
            else
            {
                // Auto-detect base name
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

            AnsiConsole.MarkupLine($"[green]✓[/] Using base name: {baseName}");

            // Step 4: Check if language already exists
            if (manager.LanguageFileExists(baseName, settings.Culture, settings.GetResourcePath()))
            {
                AnsiConsole.MarkupLine($"[red]✗ Language '{settings.Culture}' already exists[/]");
                AnsiConsole.MarkupLine($"[grey]File: {baseName}.{settings.Culture}.resx[/]");
                AnsiConsole.MarkupLine($"[grey]Use 'lrm update' to modify existing languages[/]");
                return 1;
            }

            // Step 5: Load source language file
            string sourceCultureCode;
            if (!string.IsNullOrEmpty(settings.CopyFrom))
            {
                sourceCultureCode = settings.CopyFrom;
            }
            else
            {
                // Use default language (no culture code)
                sourceCultureCode = "";
            }

            var sourceLanguage = languages.FirstOrDefault(l =>
                l.BaseName == baseName && l.Code == sourceCultureCode);

            if (sourceLanguage == null && !settings.Empty)
            {
                if (!string.IsNullOrEmpty(settings.CopyFrom))
                {
                    AnsiConsole.MarkupLine($"[red]✗ Source language '{settings.CopyFrom}' not found[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Default language file not found ({baseName}.resx)[/]");
                }

                AnsiConsole.MarkupLine("[grey]Available languages:[/]");
                var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
                foreach (var lang in languages.Where(l => l.BaseName == baseName))
                {
                    var code = string.IsNullOrEmpty(lang.Code) ? $"({defaultCode})" : lang.Code;
                    AnsiConsole.MarkupLine($"  [grey]- {code}[/]");
                }
                return 1;
            }

            // Parse source file if copying
            var sourceFile = sourceLanguage != null ? settings.ReadResourceFile(sourceLanguage) : null;

            // Step 6: Create new language file
            var copyEntries = !settings.Empty && sourceFile != null;
            var entryCount = sourceFile?.Entries.Count ?? 0;

            if (copyEntries)
            {
                var sourceName = string.IsNullOrEmpty(sourceCultureCode)
                    ? "default language"
                    : $"{sourceCultureCode} language";
                AnsiConsole.MarkupLine($"[yellow]►[/] Copying {entryCount} entries from {sourceName}...");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]►[/] Creating empty language file...");
            }

            var newFile = manager.CreateLanguageFile(
                baseName,
                settings.Culture,
                settings.GetResourcePath(),
                sourceFile,
                copyEntries);

            AnsiConsole.MarkupLine($"[green]✓[/] Created: {Path.GetFileName(newFile.Language.FilePath)}");
            AnsiConsole.MarkupLine($"[green]✓[/] Added {culture.DisplayName} ({settings.Culture}) language");

            // Show tip
            if (copyEntries)
            {
                AnsiConsole.MarkupLine("[grey]Tip: Use 'lrm update' or 'lrm edit' to add translations[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Tip: Use 'lrm add' or 'lrm edit' to add entries[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message}[/]");
            return 1;
        }
    }
}
