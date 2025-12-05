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
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to update values for an existing localization key.
/// </summary>
public class UpdateCommand : Command<UpdateCommand.Settings>
{
    private static readonly string[] ValidPluralForms = { "zero", "one", "two", "few", "many", "other" };

    public class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("The key to update")]
        public required string Key { get; set; }

        [CommandOption("-l|--lang <LANGVALUE>")]
        [Description("Language value in format 'code:\"value\"' (e.g., --lang default:\"Save\" --lang el:\"Αποθήκευση\"). Use 'default' for default language. Can be used multiple times.")]
        public string[]? LanguageValues { get; set; }

        [CommandOption("--comment <COMMENT>")]
        [Description("Update the comment for this key")]
        public string? Comment { get; set; }

        [CommandOption("--no-backup")]
        [Description("Skip creating backup files before update")]
        public bool NoBackup { get; set; }

        [CommandOption("-i|--interactive")]
        [Description("Prompt for each language value interactively")]
        public bool Interactive { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool SkipConfirmation { get; set; }

        [CommandOption("--plural-form <FORM>")]
        [Description("Update plural form value in format 'form:\"value\"' (e.g., --plural-form one:\"{0} item\" --plural-form other:\"{0} items\"). Forms: zero, one, two, few, many, other.")]
        public string[]? PluralForms { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        try
        {
            AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
            AnsiConsole.WriteLine();

            // Load configuration and discover languages
            settings.LoadConfiguration();
            var languages = settings.DiscoverLanguages();
            var backendName = settings.GetBackendName();

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine($"[red]✗ No {backendName.ToUpper()} files found![/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓ Found {languages.Count} language(s)[/]");

            // Parse resource files
            var resourceFiles = new List<Core.Models.ResourceFile>();

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

            // Check if key exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                AnsiConsole.MarkupLine("[red]✗ No default language file found![/]");
                return 1;
            }

            var existingEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
            if (existingEntry == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key.EscapeMarkup()}' not found![/]");
                AnsiConsole.MarkupLine("[yellow]💡 Tip: Use 'add' command to create new keys[/]");
                return 1;
            }

            // Check if key is plural
            bool isPlural = existingEntry.IsPlural;

            // Show current values
            var currentTable = new Table();
            currentTable.AddColumn("Language");
            currentTable.AddColumn("Current Value");

            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                string value;
                if (entry?.IsPlural == true && entry.PluralForms != null)
                {
                    var formStrings = entry.PluralForms.Select(kv => $"{kv.Key}: {kv.Value}");
                    value = string.Join(", ", formStrings).EscapeMarkup();
                    if (string.IsNullOrEmpty(value)) value = "[dim](empty)[/]";
                }
                else
                {
                    value = entry?.Value?.EscapeMarkup() ?? "[dim](empty)[/]";
                }
                currentTable.AddRow(rf.Language.Name, value);
            }

            AnsiConsole.WriteLine();
            var pluralIndicator = isPlural ? " [cyan](plural)[/]" : "";
            AnsiConsole.MarkupLine($"[yellow]Updating key:[/] [bold]{settings.Key.EscapeMarkup()}[/]{pluralIndicator}");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(currentTable);
            AnsiConsole.WriteLine();

            // Collect new values (simple keys) or plural forms (plural keys)
            var updates = new Dictionary<string, string>();
            var pluralUpdates = new Dictionary<string, string>(); // For plural forms: form -> value

            if (isPlural)
            {
                // Handle plural key updates
                if (settings.Interactive)
                {
                    AnsiConsole.MarkupLine("[dim]Enter new plural form values (press Enter to keep current):[/]");
                    AnsiConsole.WriteLine();

                    foreach (var form in new[] { "one", "other", "zero" })
                    {
                        var currentValue = existingEntry.PluralForms?.GetValueOrDefault(form, "") ?? "";
                        var prompt = new TextPrompt<string>($"  [yellow]{form}:[/]")
                            .DefaultValue(currentValue)
                            .AllowEmpty();
                        var newValue = AnsiConsole.Prompt(prompt);
                        if (newValue != currentValue)
                        {
                            pluralUpdates[form] = newValue;
                        }
                    }
                }
                else if (settings.PluralForms != null && settings.PluralForms.Any())
                {
                    // Parse --plural-form arguments
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

                        pluralUpdates[form] = value;
                    }
                }
            }
            else
            {
                // Handle simple key updates
                if (settings.Interactive)
                {
                    // Interactive mode - prompt for each language
                    foreach (var rf in resourceFiles)
                    {
                        var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                        var currentValue = entry?.Value ?? "";

                        var newValue = AnsiConsole.Prompt(
                            new TextPrompt<string>($"[cyan]{rf.Language.Name}:[/]")
                                .DefaultValue(currentValue)
                                .AllowEmpty()
                        );

                        if (newValue != currentValue)
                        {
                            updates[rf.Language.Code] = newValue;
                        }
                    }
                }
                else
                {
                    // Command-line mode - parse --lang arguments
                    if (settings.LanguageValues != null && settings.LanguageValues.Any())
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

                            updates[matchingLang.Code] = value;
                        }
                    }
                }
            }

            // Check if any updates were provided
            if (!updates.Any() && !pluralUpdates.Any() && settings.Comment == null)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No updates provided![/]");
                if (isPlural)
                {
                    AnsiConsole.MarkupLine("[dim]Use --plural-form form:value, --comment, or -i for interactive mode[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]Use --lang code:value, --comment, or -i for interactive mode[/]");
                }
                return 0;
            }

            // Show preview of changes
            if (updates.Any() || pluralUpdates.Any())
            {
                AnsiConsole.MarkupLine("[yellow]Changes to be made:[/]");

                if (pluralUpdates.Any())
                {
                    var pluralTable = new Table();
                    pluralTable.AddColumn("Form");
                    pluralTable.AddColumn("Old Value");
                    pluralTable.AddColumn("New Value");

                    foreach (var kvp in pluralUpdates)
                    {
                        var oldValue = existingEntry.PluralForms?.GetValueOrDefault(kvp.Key, "")?.EscapeMarkup() ?? "[dim](empty)[/]";
                        if (string.IsNullOrEmpty(oldValue)) oldValue = "[dim](empty)[/]";
                        pluralTable.AddRow(kvp.Key, oldValue, kvp.Value.EscapeMarkup());
                    }

                    AnsiConsole.Write(pluralTable);
                }
                else
                {
                    var previewTable = new Table();
                    previewTable.AddColumn("Language");
                    previewTable.AddColumn("Old Value");
                    previewTable.AddColumn("New Value");

                    foreach (var kvp in updates)
                    {
                        var rf = resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                        if (rf != null)
                        {
                            var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                            var oldValue = entry?.Value?.EscapeMarkup() ?? "[dim](empty)[/]";
                            previewTable.AddRow(rf.Language.Name, oldValue, kvp.Value.EscapeMarkup());
                        }
                    }

                    AnsiConsole.Write(previewTable);
                }
                AnsiConsole.WriteLine();
            }

            // Confirmation
            if (!settings.SkipConfirmation)
            {
                if (!AnsiConsole.Confirm("Apply these changes?", true))
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Update cancelled[/]");
                    return 0;
                }
            }

            // Create backups
            if (!settings.NoBackup)
            {
                var backupManager = new BackupVersionManager(10);
                var filePaths = languages.Select(l => l.FilePath).ToList();
                foreach (var filePath in filePaths)
                {
                    backupManager.CreateBackupAsync(filePath, "update-key", resourcePath)
                        .GetAwaiter().GetResult();
                }
                AnsiConsole.MarkupLine("[dim]✓ Backups created[/]");
            }

            // Apply updates
            int updatedCount = 0;

            if (pluralUpdates.Any())
            {
                // Apply plural form updates to all languages
                foreach (var rf in resourceFiles)
                {
                    var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        entry.PluralForms ??= new Dictionary<string, string>();
                        foreach (var kvp in pluralUpdates)
                        {
                            entry.PluralForms[kvp.Key] = kvp.Value;
                        }
                        // Update Value to match 'other' form
                        entry.Value = entry.PluralForms.GetValueOrDefault("other") ?? entry.PluralForms.Values.FirstOrDefault();
                        updatedCount++;
                    }
                }
            }
            else
            {
                // Apply simple value updates
                foreach (var kvp in updates)
                {
                    var rf = resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                    if (rf != null)
                    {
                        var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            entry.Value = kvp.Value;
                            updatedCount++;
                        }
                    }
                }
            }

            // Update comment if provided
            if (settings.Comment != null)
            {
                foreach (var rf in resourceFiles)
                {
                    var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        entry.Comment = settings.Comment;
                    }
                }
            }

            // Save changes
            foreach (var rf in resourceFiles)
            {
                settings.WriteResourceFile(rf);
            }

            var pluralNote = isPlural ? " (plural forms)" : "";
            AnsiConsole.MarkupLine($"[green]✓ Successfully updated key '{settings.Key.EscapeMarkup()}'{pluralNote} in {updatedCount} language(s)[/]");
            return 0;
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
}
