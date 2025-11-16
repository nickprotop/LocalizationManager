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
using LocalizationManager.Core.Enums;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Shared.Enums;
using System.ComponentModel;

namespace LocalizationManager.Commands.Backup;

/// <summary>
/// Command to show differences between backup versions.
/// </summary>
public class BackupDiffCommand : AsyncCommand<BackupDiffCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "<FILE>")]
        [Description("Resource file name (e.g., SharedResource.resx)")]
        public required string FileName { get; set; }

        [CommandOption("--from <VERSION>")]
        [Description("Source backup version (default: previous version)")]
        public int? FromVersion { get; set; }

        [CommandOption("--to <VERSION>")]
        [Description("Target backup version (default: current file)")]
        public int? ToVersion { get; set; }


        [CommandOption("--output <FILE>")]
        [Description("Save diff to file")]
        public string? OutputFile { get; set; }

        [CommandOption("--show-unchanged")]
        [Description("Include unchanged keys in the diff")]
        public bool ShowUnchanged { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = settings.GetResourcePath();
            var backupManager = new BackupVersionManager(10);
            var diffService = new BackupDiffService();
            var formatter = new BackupDiffFormatter();

            // List available backups
            var backups = await backupManager.ListBackupsAsync(settings.FileName, basePath);
            if (!backups.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]No backups found for {settings.FileName}[/]");
                return 1;
            }

            // Determine versions to compare
            int fromVer;
            int toVer = 0;  // Will be assigned when not comparing with current
            bool compareWithCurrent = false;

            if (!settings.FromVersion.HasValue && !settings.ToVersion.HasValue)
            {
                // Default: compare latest backup with current
                fromVer = backups.Max(b => b.Version);
                compareWithCurrent = true;
            }
            else if (settings.FromVersion.HasValue && !settings.ToVersion.HasValue)
            {
                // Compare specified version with current
                fromVer = settings.FromVersion.Value;
                compareWithCurrent = true;
            }
            else if (!settings.FromVersion.HasValue && settings.ToVersion.HasValue)
            {
                // Compare previous version with specified version
                toVer = settings.ToVersion.Value;
                var previousBackup = backups
                    .Where(b => b.Version < toVer)
                    .OrderByDescending(b => b.Version)
                    .FirstOrDefault();

                if (previousBackup == null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] No previous version found before v{toVer}");
                    return 1;
                }

                fromVer = previousBackup.Version;
            }
            else
            {
                fromVer = settings.FromVersion!.Value;
                toVer = settings.ToVersion!.Value;
            }

            // Get backup metadata and paths
            var fromBackup = backups.FirstOrDefault(b => b.Version == fromVer);
            if (fromBackup == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Backup version {fromVer} not found.");
                return 1;
            }

            var fromPath = await backupManager.GetBackupFilePathAsync(settings.FileName, fromVer, basePath);
            if (fromPath == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Backup file for v{fromVer} not found.");
                return 1;
            }

            BackupDiffResult diff;

            if (compareWithCurrent)
            {
                // Find current file
                var manager = new ResourceDiscovery();
                var languages = manager.DiscoverLanguages(basePath);
                var currentFile = languages.FirstOrDefault(l =>
                    Path.GetFileName(l.FilePath).Equals(settings.FileName, StringComparison.OrdinalIgnoreCase));

                if (currentFile == null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Current file '{settings.FileName}' not found.");
                    return 1;
                }

                diff = await diffService.CompareWithCurrentAsync(
                    fromBackup,
                    fromPath,
                    currentFile.FilePath,
                    settings.ShowUnchanged);
            }
            else
            {
                var toBackup = backups.FirstOrDefault(b => b.Version == toVer);
                if (toBackup == null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Backup version {toVer} not found.");
                    return 1;
                }

                var toPath = await backupManager.GetBackupFilePathAsync(settings.FileName, toVer, basePath);
                if (toPath == null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Backup file for v{toVer} not found.");
                    return 1;
                }

                diff = await diffService.CompareAsync(fromBackup, toBackup, fromPath, toPath, settings.ShowUnchanged);
            }

            // Format and output
            string output;
            switch (settings.GetOutputFormat())
            {
                case OutputFormat.Table:
                    formatter.DisplayInConsole(diff, settings.ShowUnchanged);
                    return 0;

                case OutputFormat.Json:
                    output = formatter.FormatAsJson(diff);
                    break;

                case OutputFormat.Simple:
                    output = formatter.FormatAsText(diff, colorize: false);
                    break;

                default:
                    output = formatter.FormatAsText(diff, colorize: true);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(settings.OutputFile))
            {
                await File.WriteAllTextAsync(settings.OutputFile, output);
                AnsiConsole.MarkupLine($"[green]Diff saved to {settings.OutputFile}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(output);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
