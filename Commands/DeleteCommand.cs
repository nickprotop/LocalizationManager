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
using LocalizationManager.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to delete a localization key from all language files.
/// </summary>
public class DeleteCommand : Command<DeleteCommand.Settings>
{
    public class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("The key to delete from all language files")]
        public required string Key { get; set; }

        [CommandOption("--no-backup")]
        [Description("Skip creating backup files before deletion")]
        public bool NoBackup { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool SkipConfirmation { get; set; }

        [CommandOption("--occurrence <NUMBER>")]
        [Description("Delete specific occurrence of a duplicate key (1-based index)")]
        public int? Occurrence { get; set; }

        [CommandOption("--all")]
        [Description("Delete all occurrences of a duplicate key without prompting")]
        public bool DeleteAll { get; set; }
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

            // Check for duplicates in default file
            var occurrences = defaultFile?.Entries
                .Select((e, i) => (e, i))
                .Where(x => x.e.Key == settings.Key)
                .ToList() ?? new List<(Core.Models.ResourceEntry e, int i)>();

            if (occurrences.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Key '{settings.Key.EscapeMarkup()}' not found[/]");
                return 1;
            }

            // Show current values
            var table = new Table();
            table.AddColumn("Language");
            table.AddColumn("Value");

            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == settings.Key);
                var value = entry?.Value?.EscapeMarkup() ?? "[dim](not found)[/]";
                table.AddRow(rf.Language.Name, value);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Key to delete:[/] [bold]{settings.Key.EscapeMarkup()}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            int? occurrenceToDelete = null;
            bool deleteAllOccurrences = false;

            // Handle duplicate key scenarios
            if (occurrences.Count > 1)
            {
                AnsiConsole.MarkupLine($"[yellow]Found {occurrences.Count} occurrences of key '{settings.Key.EscapeMarkup()}':[/]");
                AnsiConsole.WriteLine();

                for (int i = 0; i < occurrences.Count; i++)
                {
                    var (entry, index) = occurrences[i];
                    var value = entry.Value ?? string.Empty;
                    var preview = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
                    AnsiConsole.MarkupLine($"  [[{i + 1}]] \"{preview.EscapeMarkup()}\"");
                }
                AnsiConsole.WriteLine();

                if (settings.DeleteAll)
                {
                    // --all flag: delete all occurrences
                    deleteAllOccurrences = true;
                    if (!settings.SkipConfirmation)
                    {
                        if (!AnsiConsole.Confirm($"Delete ALL {occurrences.Count} occurrences?", false))
                        {
                            AnsiConsole.MarkupLine("[yellow]⚠ Deletion cancelled[/]");
                            return 0;
                        }
                    }
                }
                else if (settings.Occurrence.HasValue)
                {
                    // --occurrence flag: delete specific occurrence
                    if (settings.Occurrence.Value < 1 || settings.Occurrence.Value > occurrences.Count)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Invalid occurrence number. Must be between 1 and {occurrences.Count}[/]");
                        return 1;
                    }
                    occurrenceToDelete = settings.Occurrence.Value;
                }
                else
                {
                    // Interactive prompt: ask user which to delete
                    var choices = new List<string>();
                    for (int i = 1; i <= occurrences.Count; i++)
                    {
                        choices.Add($"[{i}]");
                    }
                    choices.Add("All");
                    choices.Add("Cancel");

                    var selection = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Which occurrence do you want to delete?")
                            .AddChoices(choices));

                    if (selection == "Cancel")
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠ Deletion cancelled[/]");
                        return 0;
                    }
                    else if (selection == "All")
                    {
                        deleteAllOccurrences = true;
                    }
                    else
                    {
                        // Extract number from "[N]"
                        occurrenceToDelete = int.Parse(selection.Trim('[', ']'));
                    }
                }
            }
            else
            {
                // Single occurrence: normal confirmation
                if (!settings.SkipConfirmation)
                {
                    if (!AnsiConsole.Confirm($"Delete key '{settings.Key.EscapeMarkup()}' from all languages?", false))
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠ Deletion cancelled[/]");
                        return 0;
                    }
                }
                occurrenceToDelete = 1;
            }

            // Create backups
            if (!settings.NoBackup)
            {
                var backupManager = new BackupManager();
                var filePaths = languages.Select(l => l.FilePath).ToList();
                backupManager.CreateBackups(filePaths);
                AnsiConsole.MarkupLine("[dim]✓ Backups created[/]");
            }

            // Delete key from all languages
            int deletedCount = 0;

            if (deleteAllOccurrences)
            {
                // Delete all occurrences
                foreach (var rf in resourceFiles)
                {
                    var removed = rf.Entries.RemoveAll(e => e.Key == settings.Key);
                    if (removed > 0) deletedCount++;
                }
                AnsiConsole.MarkupLine($"[green]✓ Successfully deleted all {occurrences.Count} occurrences from {deletedCount} language file(s)[/]");
            }
            else if (occurrenceToDelete.HasValue)
            {
                // Delete specific occurrence from each file
                foreach (var rf in resourceFiles)
                {
                    var indices = rf.Entries
                        .Select((e, i) => (e, i))
                        .Where(x => x.e.Key == settings.Key)
                        .Select(x => x.i)
                        .ToList();

                    if (indices.Count >= occurrenceToDelete.Value)
                    {
                        rf.Entries.RemoveAt(indices[occurrenceToDelete.Value - 1]);
                        deletedCount++;
                    }
                }
                AnsiConsole.MarkupLine($"[green]✓ Successfully deleted occurrence #{occurrenceToDelete} from {deletedCount} language file(s)[/]");
            }

            // Save changes
            foreach (var rf in resourceFiles)
            {
                parser.Write(rf);
            }

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
