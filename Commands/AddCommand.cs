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

using LocalizationManager.Core;
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to add a new localization key to all language files.
/// </summary>
public class AddCommandSettings : BaseCommandSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("The resource key to add")]
    public required string Key { get; set; }

    [CommandOption("-l|--lang <LANGVALUE>")]
    [Description("Language value in format 'code:\"value\"' (e.g., --lang default:\"Save\" --lang el:\"Αποθήκευση\"). Use 'default' for default language. Can be used multiple times.")]
    public string[]? LanguageValues { get; set; }

    [CommandOption("-i|--interactive")]
    [Description("Interactive mode - prompts for all language values even if some are provided")]
    public bool Interactive { get; set; }

    [CommandOption("--comment <COMMENT>")]
    [Description("Optional comment for the resource entry")]
    public string? Comment { get; set; }

    [CommandOption("--no-backup")]
    [Description("Skip creating backups before modifying files")]
    public bool NoBackup { get; set; }

    [CommandOption("--ask-missing")]
    [Description("Prompt for missing language values. By default, unspecified languages will have empty values.")]
    public bool AskMissing { get; set; }

    [CommandOption("--plural")]
    [Description("Create a plural key with multiple forms (JSON only). Use with -i for interactive plural form entry.")]
    public bool IsPlural { get; set; }

    [CommandOption("--plural-form <FORM>")]
    [Description("Plural form value in format 'form:\"value\"' (e.g., --plural-form one:\"{0} item\" --plural-form other:\"{0} items\"). Forms: zero, one, two, few, many, other.")]
    public string[]? PluralForms { get; set; }
}

public class AddCommand : Command<AddCommandSettings>
{
    private static readonly string[] ValidPluralForms = { "zero", "one", "two", "few", "many", "other" };

    public override int Execute(CommandContext context, AddCommandSettings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
        AnsiConsole.WriteLine();

        try
        {
            // Load configuration and discover languages
            settings.LoadConfiguration();
            var languages = settings.DiscoverLanguages();
            var backendName = settings.GetBackendName();

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine($"[red]✗ No {backendName.ToUpper()} files found![/]");
                return 1;
            }

            // Warn if using plural with non-JSON backend
            if (settings.IsPlural && backendName != "json")
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Plural forms are only supported with JSON backend. The --plural flag will be ignored.[/]");
                settings.IsPlural = false;
            }

            // Parse resource files
            var resourceFiles = new List<ResourceFile>();

            foreach (var lang in languages)
            {
                try
                {
                    var resourceFile = settings.ReadResourceFile(lang);
                    resourceFiles.Add(resourceFile);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Error parsing {lang.Name}: {ex.Message}[/]");
                    return 1;
                }
            }

            // Check if key already exists (case-insensitive per ResX specification)
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase)))
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key.EscapeMarkup()}' already exists![/]");
                return 1;
            }

            // Handle plural vs non-plural key creation
            if (settings.IsPlural)
            {
                return ExecutePluralAdd(settings, languages, resourceFiles, resourcePath);
            }
            else
            {
                return ExecuteSimpleAdd(settings, languages, resourceFiles, resourcePath);
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ {ex.Message}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Unexpected error: {ex.Message}[/]");
            return 1;
        }
    }

    private int ExecuteSimpleAdd(AddCommandSettings settings, List<LanguageInfo> languages, List<ResourceFile> resourceFiles, string resourcePath)
    {
        // Collect values for each language
        var values = new Dictionary<string, string?>();

        // Parse --lang arguments (unless in pure interactive mode)
        if (!settings.Interactive && settings.LanguageValues != null && settings.LanguageValues.Any())
        {
            foreach (var langValue in settings.LanguageValues)
            {
                var parts = langValue.Split(':', 2);
                if (parts.Length != 2)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid format: '{langValue}'. Expected format: 'code:value' (e.g., en:Save)[/]");
                    return 1;
                }

                var code = parts[0].Trim();
                var value = parts[1];

                // Normalize "default" alias to empty string
                if (code.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    code = "";
                }

                // Validate language code exists
                var matchingLang = languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (matchingLang == null)
                {
                    var availableCodes = string.Join(", ", languages.Select(l => l.Code));
                    AnsiConsole.MarkupLine($"[red]✗ Unknown language code: '{code}'[/]");
                    AnsiConsole.MarkupLine($"[yellow]Available languages: {availableCodes}[/]");
                    return 1;
                }

                values[matchingLang.Code] = value;
            }
        }

        // Interactive mode or prompt for missing values
        if (settings.Interactive)
        {
            // Interactive mode: prompt for all languages
            AnsiConsole.MarkupLine("[dim]Interactive mode: Enter values for all languages[/]");
            AnsiConsole.WriteLine();

            foreach (var lang in languages)
            {
                var prompt = $"[yellow]{lang.Name}:[/]";
                var value = AnsiConsole.Ask<string>(prompt);
                values[lang.Code] = value;
            }
        }
        else if (settings.AskMissing)
        {
            // Ask for missing: prompt only for missing values
            foreach (var lang in languages)
            {
                if (!values.ContainsKey(lang.Code))
                {
                    var value = AnsiConsole.Ask<string>($"[yellow]Enter value for {lang.Name}:[/]");
                    values[lang.Code] = value;
                }
            }
        }
        else
        {
            // Default: use empty values for unprovided languages (non-interactive)
            foreach (var lang in languages)
            {
                if (!values.ContainsKey(lang.Code))
                {
                    values[lang.Code] = string.Empty;
                }
            }
        }

        // Create backups and add entries
        return AddEntriesToFiles(settings, resourceFiles, resourcePath, rf =>
        {
            var value = values.GetValueOrDefault(rf.Language.Code, string.Empty);
            return new ResourceEntry
            {
                Key = settings.Key,
                Value = value,
                Comment = settings.Comment
            };
        });
    }

    private int ExecutePluralAdd(AddCommandSettings settings, List<LanguageInfo> languages, List<ResourceFile> resourceFiles, string resourcePath)
    {
        // Collect plural forms for each language
        var pluralValuesByLang = new Dictionary<string, Dictionary<string, string>>();

        // Parse --plural-form arguments
        var defaultPluralForms = new Dictionary<string, string>();
        if (settings.PluralForms != null && settings.PluralForms.Any())
        {
            foreach (var formValue in settings.PluralForms)
            {
                var parts = formValue.Split(':', 2);
                if (parts.Length != 2)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid plural format: '{formValue}'. Expected format: 'form:value' (e.g., one:\"{{0}} item\")[/]");
                    return 1;
                }

                var form = parts[0].Trim().ToLowerInvariant();
                var value = parts[1];

                if (!ValidPluralForms.Contains(form))
                {
                    AnsiConsole.MarkupLine($"[red]✗ Invalid plural form: '{form}'. Valid forms: {string.Join(", ", ValidPluralForms)}[/]");
                    return 1;
                }

                defaultPluralForms[form] = value;
            }
        }

        // Interactive mode for plural forms
        if (settings.Interactive)
        {
            AnsiConsole.MarkupLine("[dim]Interactive mode: Enter plural forms for each language[/]");
            AnsiConsole.MarkupLine("[dim]Common forms: one (singular), other (plural). Leave empty to skip a form.[/]");
            AnsiConsole.WriteLine();

            foreach (var lang in languages)
            {
                AnsiConsole.MarkupLine($"[cyan]{lang.Name}:[/]");
                var forms = new Dictionary<string, string>();

                foreach (var form in new[] { "one", "other", "zero" }) // Most common forms
                {
                    var defaultVal = defaultPluralForms.GetValueOrDefault(form, "");
                    var prompt = new TextPrompt<string>($"  [yellow]{form}:[/]")
                        .DefaultValue(defaultVal)
                        .AllowEmpty();
                    var value = AnsiConsole.Prompt(prompt);
                    if (!string.IsNullOrEmpty(value))
                    {
                        forms[form] = value;
                    }
                }

                if (forms.Count > 0)
                {
                    pluralValuesByLang[lang.Code] = forms;
                }
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            // Non-interactive: apply default plural forms to all languages
            if (!defaultPluralForms.Any())
            {
                AnsiConsole.MarkupLine("[red]✗ No plural forms provided. Use --plural-form or -i for interactive mode.[/]");
                return 1;
            }

            foreach (var lang in languages)
            {
                pluralValuesByLang[lang.Code] = new Dictionary<string, string>(defaultPluralForms);
            }
        }

        // Create backups and add entries
        return AddEntriesToFiles(settings, resourceFiles, resourcePath, rf =>
        {
            var forms = pluralValuesByLang.GetValueOrDefault(rf.Language.Code);
            if (forms == null || forms.Count == 0)
            {
                forms = new Dictionary<string, string> { ["other"] = "" };
            }

            return new ResourceEntry
            {
                Key = settings.Key,
                Value = forms.GetValueOrDefault("other") ?? forms.Values.FirstOrDefault(),
                Comment = settings.Comment,
                IsPlural = true,
                PluralForms = forms
            };
        });
    }

    private int AddEntriesToFiles(AddCommandSettings settings, List<ResourceFile> resourceFiles, string resourcePath, Func<ResourceFile, ResourceEntry> createEntry)
    {
        // Create backups
        if (!settings.NoBackup)
        {
            AnsiConsole.MarkupLine("[dim]Creating backups...[/]");
            var backupManager = new BackupVersionManager(10);
            var filePaths = resourceFiles.Select(rf => rf.Language.FilePath).ToList();

            try
            {
                foreach (var filePath in filePaths)
                {
                    backupManager.CreateBackupAsync(filePath, "add-key", resourcePath)
                        .GetAwaiter().GetResult();
                }
                AnsiConsole.MarkupLine("[dim green]✓ Backups created[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Backup failed: {ex.Message}[/]");
                if (!AnsiConsole.Confirm("Continue without backup?"))
                {
                    return 1;
                }
            }
        }

        // Add the key to all resource files
        foreach (var resourceFile in resourceFiles)
        {
            var entry = createEntry(resourceFile);
            resourceFile.Entries.Add(entry);

            // Save the file
            try
            {
                settings.WriteResourceFile(resourceFile);
                AnsiConsole.MarkupLine($"[green]✓ Added to {resourceFile.Language.Name}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to write {resourceFile.Language.Name}: {ex.Message}[/]");
                return 1;
            }
        }

        AnsiConsole.WriteLine();
        var pluralNote = settings.IsPlural ? " (plural)" : "";
        AnsiConsole.MarkupLine($"[green bold]✓ Successfully added key '{settings.Key.EscapeMarkup()}'{pluralNote} to {resourceFiles.Count} file(s)[/]");

        return 0;
    }
}
