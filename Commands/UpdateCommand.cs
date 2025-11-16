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
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        try
        {
            AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
            AnsiConsole.WriteLine();

            // Discover languages
            var discovery = new ResourceDiscovery();
            var languages = discovery.DiscoverLanguages(resourcePath);

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine("[red]✗ No .resx files found![/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓ Found {languages.Count} language(s)[/]");

            // Parse resource files
            var parser = new ResourceFileParser();
            var resourceFiles = new List<Core.Models.ResourceFile>();

            foreach (var lang in languages)
            {
                try
                {
                    var resourceFile = parser.Parse(lang);
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

            var existingEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == settings.Key);
            if (existingEntry == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key.EscapeMarkup()}' not found![/]");
                AnsiConsole.MarkupLine("[yellow]💡 Tip: Use 'add' command to create new keys[/]");
                return 1;
            }

            // Show current values
            var currentTable = new Table();
            currentTable.AddColumn("Language");
            currentTable.AddColumn("Current Value");

            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
                var value = entry?.Value?.EscapeMarkup() ?? "[dim](empty)[/]";
                currentTable.AddRow(rf.Language.Name, value);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Updating key:[/] [bold]{settings.Key.EscapeMarkup()}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(currentTable);
            AnsiConsole.WriteLine();

            // Collect new values
            var updates = new Dictionary<string, string>();

            if (settings.Interactive)
            {
                // Interactive mode - prompt for each language
                foreach (var rf in resourceFiles)
                {
                    var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
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

            // Check if any updates were provided
            if (!updates.Any() && settings.Comment == null)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No updates provided![/]");
                AnsiConsole.MarkupLine("[dim]Use --lang code:value, --comment, or -i for interactive mode[/]");
                return 0;
            }

            // Show preview of changes
            if (updates.Any())
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
                        var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
                        var oldValue = entry?.Value?.EscapeMarkup() ?? "[dim](empty)[/]";
                        previewTable.AddRow(rf.Language.Name, oldValue, kvp.Value.EscapeMarkup());
                    }
                }

                AnsiConsole.MarkupLine("[yellow]Changes to be made:[/]");
                AnsiConsole.Write(previewTable);
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
            foreach (var kvp in updates)
            {
                var rf = resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                if (rf != null)
                {
                    var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
                    if (entry != null)
                    {
                        entry.Value = kvp.Value;
                        updatedCount++;
                    }
                }
            }

            // Update comment if provided
            if (settings.Comment != null)
            {
                foreach (var rf in resourceFiles)
                {
                    var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
                    if (entry != null)
                    {
                        entry.Comment = settings.Comment;
                    }
                }
            }

            // Save changes
            foreach (var rf in resourceFiles)
            {
                parser.Write(rf);
            }

            AnsiConsole.MarkupLine($"[green]✓ Successfully updated key '{settings.Key.EscapeMarkup()}' in {updatedCount} language(s)[/]");
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
