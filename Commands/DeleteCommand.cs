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

        [CommandOption("--all-duplicates")]
        [Description("Delete all occurrences of a duplicate key")]
        public bool AllDuplicates { get; set; }
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

            // Check if key exists and count occurrences
            var occurrenceCount = defaultFile.Entries.Count(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));

            if (occurrenceCount == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Key '{settings.Key.EscapeMarkup()}' not found[/]");
                return 1;
            }

            // Check for duplicates
            if (occurrenceCount > 1 && !settings.AllDuplicates)
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key.EscapeMarkup()}' has {occurrenceCount} occurrences.[/]");
                AnsiConsole.MarkupLine($"[yellow]Use --all-duplicates to delete all occurrences, or use 'merge-duplicates' to consolidate them.[/]");
                return 1;
            }

            // Show current values
            var table = new Table();
            table.AddColumn("Language");
            table.AddColumn("Value");

            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                var value = entry?.Value?.EscapeMarkup() ?? "[dim](not found)[/]";
                table.AddRow(rf.Language.Name, value);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Key to delete:[/] [bold]{settings.Key.EscapeMarkup()}[/]");
            if (occurrenceCount > 1)
            {
                AnsiConsole.MarkupLine($"[yellow]Occurrences:[/] {occurrenceCount}");
            }
            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Confirmation
            if (!settings.SkipConfirmation)
            {
                var confirmMessage = occurrenceCount > 1
                    ? $"Delete ALL {occurrenceCount} occurrences of '{settings.Key.EscapeMarkup()}' from all languages?"
                    : $"Delete key '{settings.Key.EscapeMarkup()}' from all languages?";

                if (!AnsiConsole.Confirm(confirmMessage, false))
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Deletion cancelled[/]");
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
                    backupManager.CreateBackupAsync(filePath, "delete-key", resourcePath)
                        .GetAwaiter().GetResult();
                }
                AnsiConsole.MarkupLine("[dim]✓ Backups created[/]");
            }

            // Delete key from all languages (removes all occurrences)
            int deletedCount = 0;

            foreach (var rf in resourceFiles)
            {
                var removed = rf.Entries.RemoveAll(e => e.Key.Equals(settings.Key, StringComparison.OrdinalIgnoreCase));
                if (removed > 0) deletedCount++;
            }

            var successMessage = occurrenceCount > 1
                ? $"[green]✓ Successfully deleted all {occurrenceCount} occurrences from {deletedCount} language file(s)[/]"
                : $"[green]✓ Successfully deleted key from {deletedCount} language file(s)[/]";

            AnsiConsole.MarkupLine(successMessage);

            // Save changes
            foreach (var rf in resourceFiles)
            {
                settings.WriteResourceFile(rf);
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
