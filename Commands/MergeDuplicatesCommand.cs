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
/// Command to merge duplicate occurrences of a key into a single entry.
/// </summary>
public class MergeDuplicatesCommand : Command<MergeDuplicatesCommand.Settings>
{
    public class Settings : BaseCommandSettings
    {
        [CommandArgument(0, "[KEY]")]
        [Description("The key to merge. If not provided with --all, will show error.")]
        public string? Key { get; set; }

        [CommandOption("--all")]
        [Description("Merge all duplicate keys in the resource files")]
        public bool MergeAll { get; set; }

        [CommandOption("--auto-first")]
        [Description("Automatically select first occurrence from each language without prompting")]
        public bool AutoFirst { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip final confirmation prompt")]
        public bool SkipConfirmation { get; set; }

        [CommandOption("--no-backup")]
        [Description("Skip creating backup files before merging")]
        public bool NoBackup { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        // Validate arguments
        if (string.IsNullOrWhiteSpace(settings.Key) && !settings.MergeAll)
        {
            AnsiConsole.MarkupLine("[red]✗ You must specify a KEY or use --all to merge all duplicate keys[/]");
            return 1;
        }

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

            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                AnsiConsole.MarkupLine("[red]✗ No default language file found![/]");
                return 1;
            }

            // Determine which keys to merge
            List<string> keysToMerge;
            if (settings.MergeAll)
            {
                // Find all keys with duplicates
                keysToMerge = defaultFile.Entries
                    .GroupBy(e => e.Key)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (keysToMerge.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No duplicate keys found[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine($"[yellow]Found {keysToMerge.Count} key(s) with duplicates[/]");
                AnsiConsole.WriteLine();
            }
            else
            {
                // Single key mode
                var occurrenceCount = defaultFile.Entries.Count(e => e.Key == settings.Key);

                if (occurrenceCount == 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key!.EscapeMarkup()}' not found[/]");
                    return 1;
                }

                if (occurrenceCount == 1)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key!.EscapeMarkup()}' has only 1 occurrence, nothing to merge[/]");
                    return 1;
                }

                keysToMerge = new List<string> { settings.Key! };
                AnsiConsole.MarkupLine($"[yellow]Key '{settings.Key!.EscapeMarkup()}' has {occurrenceCount} occurrences[/]");
                AnsiConsole.WriteLine();
            }

            // Process each key
            int totalMerged = 0;
            bool needsBackup = !settings.NoBackup;
            bool backupCreated = false;

            foreach (var key in keysToMerge)
            {
                bool shouldMerge = false;

                if (settings.AutoFirst)
                {
                    // Auto mode: merge immediately
                    shouldMerge = true;
                }
                else
                {
                    // Interactive mode: ask user for each language, returns true if user confirms
                    shouldMerge = MergeKeyInteractive(resourceFiles, key, settings.SkipConfirmation, out var userConfirmed);
                    if (!userConfirmed)
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠ Skipped merging '{key.EscapeMarkup()}'[/]");
                        if (keysToMerge.Count > 1)
                        {
                            AnsiConsole.WriteLine();
                        }
                        continue;
                    }
                }

                // Create backup only when first merge is confirmed
                if (needsBackup && !backupCreated && shouldMerge)
                {
                    var backupManager = new BackupVersionManager(10);
                    var filePaths = languages.Select(l => l.FilePath).ToList();
                    foreach (var filePath in filePaths)
                    {
                        backupManager.CreateBackupAsync(filePath, "merge-duplicates", resourcePath)
                            .GetAwaiter().GetResult();
                    }
                    AnsiConsole.MarkupLine("[dim]✓ Backups created[/]");
                    backupCreated = true;
                }

                if (shouldMerge)
                {
                    if (settings.AutoFirst)
                    {
                        MergeKeyAutomatic(resourceFiles, key);
                    }
                    else
                    {
                        ApplyMerge(resourceFiles, key);
                    }
                    totalMerged++;
                }

                if (keysToMerge.Count > 1)
                {
                    AnsiConsole.WriteLine();
                }
            }

            // Save changes only if something was merged
            if (totalMerged > 0)
            {
                foreach (var rf in resourceFiles)
                {
                    parser.Write(rf);
                }
            }

            if (totalMerged > 0)
            {
                var message = totalMerged == 1
                    ? $"[green]✓ Successfully merged 1 key[/]"
                    : $"[green]✓ Successfully merged {totalMerged} keys[/]";
                AnsiConsole.MarkupLine(message);
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

    private bool MergeKeyAutomatic(List<Core.Models.ResourceFile> resourceFiles, string key)
    {
        // For each language, keep the first occurrence and remove the rest
        foreach (var rf in resourceFiles)
        {
            var occurrences = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key == key)
                .ToList();

            if (occurrences.Count <= 1)
                continue;

            // Remove all occurrences except the first (in reverse order to maintain indices)
            for (int i = occurrences.Count - 1; i >= 1; i--)
            {
                rf.Entries.RemoveAt(occurrences[i].Index);
            }
        }

        AnsiConsole.MarkupLine($"[green]✓ Merged '{key.EscapeMarkup()}' (kept first occurrence from each language)[/]");
        return true;
    }

    private bool MergeKeyInteractive(List<Core.Models.ResourceFile> resourceFiles, string key, bool skipFinalConfirmation, out bool userConfirmed)
    {
        userConfirmed = false;
        var selections = new Dictionary<string, int>(); // language name -> selected occurrence index (1-based)

        AnsiConsole.MarkupLine($"[cyan]Merging key:[/] [bold]{key.EscapeMarkup()}[/]");
        AnsiConsole.WriteLine();

        // For each language, ask which occurrence to keep
        foreach (var rf in resourceFiles)
        {
            var occurrences = rf.Entries
                .Where(e => e.Key == key)
                .ToList();

            if (occurrences.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [dim]{rf.Language.Name}: (not found)[/]");
                continue;
            }

            if (occurrences.Count == 1)
            {
                selections[rf.Language.Name] = 1;
                var value = occurrences[0].Value ?? "";
                var preview = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
                AnsiConsole.MarkupLine($"  [dim]{rf.Language.Name}: \"{preview.EscapeMarkup()}\" (only 1 occurrence)[/]");
                continue;
            }

            // Multiple occurrences: ask user
            AnsiConsole.MarkupLine($"  [yellow]{rf.Language.Name} has {occurrences.Count} occurrences:[/]");

            var choices = new List<string>();
            for (int i = 0; i < occurrences.Count; i++)
            {
                var entry = occurrences[i];
                var value = entry.Value ?? "";
                var commentText = entry.Comment ?? "";
                var comment = !string.IsNullOrWhiteSpace(commentText) ? $" [dim]// {commentText.EscapeMarkup()}[/]" : "";
                var preview = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                var choiceLabel = $"[[{i + 1}]] \"{preview.EscapeMarkup()}\"{comment}";
                choices.Add(choiceLabel);
            }

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"  Which occurrence to keep for [yellow]{rf.Language.Name}[/]?")
                    .AddChoices(choices));

            // Extract number from "[[N]] ..."
            var selectedIndex = int.Parse(selection.Substring(2, selection.IndexOf("]]") - 2));
            selections[rf.Language.Name] = selectedIndex;

            AnsiConsole.WriteLine();
        }

        // Show preview
        if (!skipFinalConfirmation)
        {
            AnsiConsole.MarkupLine("[cyan]Preview of merged entry:[/]");
            AnsiConsole.WriteLine();

            var previewTable = new Table();
            previewTable.AddColumn("Language");
            previewTable.AddColumn("Selected Value");

            foreach (var rf in resourceFiles)
            {
                if (!selections.ContainsKey(rf.Language.Name))
                {
                    previewTable.AddRow(rf.Language.Name, "[dim](not found)[/]");
                    continue;
                }

                var occurrenceIndex = selections[rf.Language.Name] - 1;
                var occurrences = rf.Entries.Where(e => e.Key == key).ToList();
                if (occurrenceIndex < occurrences.Count)
                {
                    var selectedEntry = occurrences[occurrenceIndex];
                    var value = selectedEntry.Value?.EscapeMarkup() ?? "";
                    previewTable.AddRow(rf.Language.Name, value);
                }
            }

            AnsiConsole.Write(previewTable);
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm($"Apply merge for key '{key.EscapeMarkup()}'?", true))
            {
                // User cancelled
                userConfirmed = false;
                return false;
            }
        }
        else
        {
            // No confirmation needed, auto-confirm
            AnsiConsole.WriteLine();
        }

        // User confirmed (or skipFinalConfirmation was true)
        userConfirmed = true;

        // Store selections for ApplyMerge to use
        _pendingSelections = selections;
        _pendingKey = key;

        return true;
    }

    private Dictionary<string, int>? _pendingSelections;
    private string? _pendingKey;

    private void ApplyMerge(List<Core.Models.ResourceFile> resourceFiles, string key)
    {
        if (_pendingSelections == null || _pendingKey != key)
        {
            throw new InvalidOperationException("No pending merge selections found");
        }

        var selections = _pendingSelections;

        // Apply merge: keep selected occurrence, remove others
        foreach (var rf in resourceFiles)
        {
            if (!selections.ContainsKey(rf.Language.Name))
                continue;

            var selectedOccurrence = selections[rf.Language.Name];
            var indices = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key == key)
                .Select(x => x.Index)
                .ToList();

            if (indices.Count <= 1)
                continue;

            // Remove all except the selected one (in reverse to maintain indices)
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                if (i + 1 != selectedOccurrence)
                {
                    rf.Entries.RemoveAt(indices[i]);
                }
            }
        }

        AnsiConsole.MarkupLine($"[green]✓ Merged '{key.EscapeMarkup()}'[/]");

        // Clear pending state
        _pendingSelections = null;
        _pendingKey = null;
    }
}
