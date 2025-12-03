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
/// Command to list all backups for a resource file.
/// </summary>
public class BackupListCommand : AsyncCommand<BackupListCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandOption("--file <FILE>")]
        [Description("Resource file name to list backups for (e.g., SharedResource.resx)")]
        public string? FileName { get; set; }

        [CommandOption("--all")]
        [Description("List backups for all resource files")]
        public bool AllFiles { get; set; }

        [CommandOption("--limit <COUNT>")]
        [Description("Maximum number of backups to display (default: 20, 0 for no limit)")]
        [DefaultValue(20)]
        public int Limit { get; set; } = 20;

        [CommandOption("--show-details")]
        [Description("Show detailed information for each backup")]
        public bool ShowDetails { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            settings.LoadConfiguration();
            var basePath = settings.GetResourcePath();

            if (!settings.AllFiles && string.IsNullOrWhiteSpace(settings.FileName))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Either --file or --all must be specified.");
                return 1;
            }

            var backupManager = new BackupVersionManager(10); // Max versions doesn't matter for listing

            if (settings.AllFiles)
            {
                await ListAllBackupsAsync(backupManager, basePath, settings);
            }
            else
            {
                await ListFileBackupsAsync(backupManager, settings.FileName!, basePath, settings);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task ListAllBackupsAsync(BackupVersionManager manager, string basePath, Settings settings)
    {
        var backupDir = Path.Combine(basePath, ".lrm", "backups");
        if (!Directory.Exists(backupDir))
        {
            AnsiConsole.MarkupLine("[yellow]No backups found.[/]");
            return;
        }

        var directories = Directory.GetDirectories(backupDir);
        var fileNames = directories
            .Select(d => Path.GetFileName(d))
            .OrderBy(f => f)
            .ToList();

        if (!fileNames.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No backups found.[/]");
            return;
        }

        foreach (var fileName in fileNames)
        {
            await ListFileBackupsAsync(manager, fileName, basePath, settings);
            AnsiConsole.WriteLine();
        }
    }

    private async Task ListFileBackupsAsync(BackupVersionManager manager, string fileName, string basePath, Settings settings)
    {
        var backups = await manager.ListBackupsAsync(fileName, basePath);

        if (!backups.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]No backups found for {fileName}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Backups for {fileName}:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Version");
        table.AddColumn("Timestamp");
        table.AddColumn("Operation");
        table.AddColumn("Keys");
        table.AddColumn("Changed");

        if (settings.ShowDetails)
        {
            table.AddColumn("Changed Keys");
        }

        var displayBackups = settings.Limit > 0
            ? backups.OrderByDescending(b => b.Version).Take(settings.Limit)
            : backups.OrderByDescending(b => b.Version);

        foreach (var backup in displayBackups)
        {
            var row = new List<string>
            {
                $"v{backup.Version:D3}",
                backup.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                backup.Operation,
                backup.KeyCount.ToString(),
                backup.ChangedKeys > 0 ? $"[yellow]{backup.ChangedKeys}[/]" : "-"
            };

            if (settings.ShowDetails)
            {
                if (backup.ChangedKeyNames != null && backup.ChangedKeyNames.Any())
                {
                    var keysList = string.Join(", ", backup.ChangedKeyNames.Take(5));
                    if (backup.ChangedKeyNames.Count > 5)
                    {
                        keysList += $" (+{backup.ChangedKeyNames.Count - 5} more)";
                    }
                    row.Add(keysList);
                }
                else
                {
                    row.Add("-");
                }
            }

            table.AddRow(row.ToArray());
        }

        AnsiConsole.Write(table);

        if (settings.Limit > 0 && backups.Count > settings.Limit)
        {
            AnsiConsole.MarkupLine($"[dim]Showing {settings.Limit} of {backups.Count} backups (use --limit 0 to show all)[/]");
        }
    }
}
