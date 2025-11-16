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
/// Command to remove old backups based on retention policy.
/// </summary>
public class BackupPruneCommand : AsyncCommand<BackupPruneCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandOption("--file <FILE>")]
        [Description("Resource file name to prune backups for (e.g., SharedResource.resx)")]
        public string? FileName { get; set; }

        [CommandOption("--all")]
        [Description("Prune backups for all resource files")]
        public bool AllFiles { get; set; }

        [CommandOption("--version <VERSION>")]
        [Description("Delete a specific backup version")]
        public int? Version { get; set; }

        [CommandOption("--older-than <DAYS>")]
        [Description("Delete backups older than N days")]
        public int? OlderThanDays { get; set; }

        [CommandOption("--keep <COUNT>")]
        [Description("Keep only the N most recent backups")]
        public int? KeepCount { get; set; }

        [CommandOption("--dry-run")]
        [Description("Preview what would be deleted without actually deleting")]
        public bool DryRun { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool SkipConfirmation { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = settings.GetResourcePath();

            if (!settings.AllFiles && string.IsNullOrWhiteSpace(settings.FileName))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Either --file or --all must be specified.");
                return 1;
            }

            var backupManager = new BackupVersionManager(10);

            if (settings.AllFiles)
            {
                await PruneAllFilesAsync(backupManager, basePath, settings);
            }
            else
            {
                await PruneFileBackupsAsync(backupManager, settings.FileName!, basePath, settings);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task PruneAllFilesAsync(BackupVersionManager manager, string basePath, Settings settings)
    {
        var backupDir = Path.Combine(basePath, ".lrm", "backups");
        if (!Directory.Exists(backupDir))
        {
            AnsiConsole.MarkupLine("[yellow]No backups found.[/]");
            return;
        }

        var manifestFiles = Directory.GetFiles(backupDir, "manifest.json", SearchOption.AllDirectories);
        var fileNames = manifestFiles
            .Select(f => Path.GetFileName(Path.GetDirectoryName(f)))
            .Distinct()
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .OfType<string>()
            .ToList();

        foreach (var fileName in fileNames)
        {
            await PruneFileBackupsAsync(manager, fileName, basePath, settings);
            AnsiConsole.WriteLine();
        }
    }

    private async Task PruneFileBackupsAsync(BackupVersionManager manager, string fileName, string basePath, Settings settings)
    {
        var backups = await manager.ListBackupsAsync(fileName, basePath);

        if (!backups.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]No backups found for {fileName}[/]");
            return;
        }

        List<int> versionsToDelete = new();

        // Determine which backups to delete
        if (settings.Version.HasValue)
        {
            // Delete specific version
            if (backups.Any(b => b.Version == settings.Version.Value))
            {
                versionsToDelete.Add(settings.Version.Value);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Version {settings.Version} not found for {fileName}");
                return;
            }
        }
        else if (settings.OlderThanDays.HasValue)
        {
            // Delete backups older than N days
            var cutoffDate = DateTime.UtcNow.AddDays(-settings.OlderThanDays.Value);
            versionsToDelete = backups
                .Where(b => b.Timestamp < cutoffDate)
                .Select(b => b.Version)
                .ToList();
        }
        else if (settings.KeepCount.HasValue)
        {
            // Keep only N most recent
            var toDelete = backups
                .OrderByDescending(b => b.Version)
                .Skip(settings.KeepCount.Value)
                .Select(b => b.Version)
                .ToList();
            versionsToDelete = toDelete;
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Specify --version, --older-than, or --keep");
            return;
        }

        if (!versionsToDelete.Any())
        {
            AnsiConsole.MarkupLine($"[green]No backups to delete for {fileName}[/]");
            return;
        }

        // Display what will be deleted
        AnsiConsole.MarkupLine($"[blue]Backups to delete for {fileName}:[/]");

        var table = new Table();
        table.AddColumn("Version");
        table.AddColumn("Timestamp");
        table.AddColumn("Operation");
        table.AddColumn("Keys");

        foreach (var version in versionsToDelete.OrderBy(v => v))
        {
            var backup = backups.First(b => b.Version == version);
            table.AddRow(
                $"v{backup.Version:D3}",
                backup.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                backup.Operation,
                backup.KeyCount.ToString());
        }

        AnsiConsole.Write(table);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[yellow]Dry run - no backups were deleted[/]");
            return;
        }

        if (!settings.SkipConfirmation &&
            !AnsiConsole.Confirm($"Delete {versionsToDelete.Count} backup(s)?", false))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return;
        }

        // Delete backups
        var deleted = 0;
        var failed = 0;

        foreach (var version in versionsToDelete)
        {
            try
            {
                var success = await manager.DeleteBackupAsync(fileName, version, basePath);
                if (success)
                {
                    deleted++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        AnsiConsole.MarkupLine($"[green]Deleted: {deleted}[/], [red]Failed: {failed}[/]");
    }
}
