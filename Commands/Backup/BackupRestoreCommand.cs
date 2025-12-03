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
using LocalizationManager.Shared.Enums;
using System.ComponentModel;

namespace LocalizationManager.Commands.Backup;

/// <summary>
/// Command to restore a resource file from a backup.
/// </summary>
public class BackupRestoreCommand : AsyncCommand<BackupRestoreCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "<FILE>")]
        [Description("Resource file name to restore (e.g., SharedResource.resx)")]
        public required string FileName { get; set; }

        [CommandOption("--version <VERSION>")]
        [Description("Backup version to restore from (e.g., 3)")]
        public int? Version { get; set; }

        [CommandOption("--keys <KEYS>")]
        [Description("Comma-separated list of specific keys to restore (partial restore)")]
        public string? Keys { get; set; }

        [CommandOption("--preview")]
        [Description("Preview changes without actually restoring")]
        public bool Preview { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool SkipConfirmation { get; set; }

        [CommandOption("--no-backup")]
        [Description("Don't create a backup before restoring")]
        public bool NoBackup { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            settings.LoadConfiguration();
            var basePath = settings.GetResourcePath();

            // Find the resource file
            var filePath = FindResourceFile(settings.FileName, basePath, settings);
            if (filePath == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Resource file '{settings.FileName}' not found.");
                return 1;
            }

            var backupManager = new BackupVersionManager(10);
            var restoreService = new BackupRestoreService(backupManager);

            // List backups if version not specified
            if (!settings.Version.HasValue)
            {
                var backups = await backupManager.ListBackupsAsync(settings.FileName, basePath);
                if (!backups.Any())
                {
                    AnsiConsole.MarkupLine($"[yellow]No backups found for {settings.FileName}[/]");
                    return 1;
                }

                AnsiConsole.MarkupLine($"[blue]Available backups for {settings.FileName}:[/]");
                AnsiConsole.WriteLine();

                var table = new Table();
                table.AddColumn("Version");
                table.AddColumn("Timestamp");
                table.AddColumn("Operation");
                table.AddColumn("Keys");

                foreach (var backup in backups.OrderByDescending(b => b.Version))
                {
                    table.AddRow(
                        $"v{backup.Version:D3}",
                        backup.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        backup.Operation,
                        backup.KeyCount.ToString());
                }

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine("[yellow]Please specify --version to restore[/]");
                return 1;
            }

            // Get backup file path
            var backupFilePath = await backupManager.GetBackupFilePathAsync(
                settings.FileName,
                settings.Version.Value,
                basePath);

            if (backupFilePath == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Backup version {settings.Version} not found.");
                return 1;
            }

            // Determine if full or partial restore
            var isPartialRestore = !string.IsNullOrWhiteSpace(settings.Keys);

            if (isPartialRestore)
            {
                var keys = settings.Keys!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToList();

                // Preview (PreviewRestoreAsync doesn't filter by keys, shows full diff)
                var diff = await restoreService.PreviewRestoreAsync(
                    settings.FileName,
                    settings.Version.Value,
                    filePath,
                    basePath);

                DisplayRestoreDiff(diff);

                if (settings.Preview)
                {
                    return 0;
                }

                if (!settings.SkipConfirmation &&
                    !AnsiConsole.Confirm($"Restore {keys.Count} key(s) from v{settings.Version:D3}?", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Restore cancelled.[/]");
                    return 0;
                }

                var restoredCount = await restoreService.RestoreKeysAsync(
                    settings.FileName,
                    settings.Version.Value,
                    keys,
                    filePath,
                    basePath,
                    !settings.NoBackup);

                AnsiConsole.MarkupLine($"[green]Successfully restored {restoredCount} key(s)![/]");
            }
            else
            {
                // Full restore
                if (!settings.SkipConfirmation &&
                    !AnsiConsole.Confirm($"[yellow]This will completely replace {settings.FileName} with v{settings.Version:D3}. Continue?[/]", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Restore cancelled.[/]");
                    return 0;
                }

                var success = await restoreService.RestoreAsync(
                    settings.FileName,
                    settings.Version.Value,
                    filePath,
                    basePath,
                    !settings.NoBackup);

                if (success)
                {
                    AnsiConsole.MarkupLine($"[green]Successfully restored {settings.FileName} from v{settings.Version:D3}![/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Restore failed.[/]");
                    return 1;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private string? FindResourceFile(string fileName, string basePath, Settings settings)
    {
        var languages = settings.DiscoverLanguages();

        var file = languages.FirstOrDefault(l =>
            Path.GetFileName(l.FilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        return file?.FilePath;
    }

    private void DisplayRestoreDiff(BackupDiffResult diff)
    {
        AnsiConsole.MarkupLine($"[blue]Preview of changes:[/]");
        AnsiConsole.WriteLine();

        var stats = diff.Statistics;
        AnsiConsole.MarkupLine($"[green]Added:[/] {stats.AddedCount}");
        AnsiConsole.MarkupLine($"[yellow]Modified:[/] {stats.ModifiedCount}");
        AnsiConsole.MarkupLine($"[red]Deleted:[/] {stats.DeletedCount}");
        AnsiConsole.WriteLine();

        if (diff.Changes.Any())
        {
            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Change");
            table.AddColumn("Old Value");
            table.AddColumn("New Value");

            foreach (var change in diff.Changes.Take(20))
            {
                var changeType = change.Type switch
                {
                    Shared.Enums.ChangeType.Added => "[green]+[/]",
                    Shared.Enums.ChangeType.Modified => "[yellow]~[/]",
                    Shared.Enums.ChangeType.Deleted => "[red]-[/]",
                    _ => " "
                };

                table.AddRow(
                    change.Key,
                    changeType,
                    change.OldValue ?? "[dim]-[/]",
                    change.NewValue ?? "[dim]-[/]");
            }

            AnsiConsole.Write(table);

            if (diff.Changes.Count > 20)
            {
                AnsiConsole.MarkupLine($"[dim]... and {diff.Changes.Count - 20} more changes[/]");
            }
        }
    }
}
