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
/// Command to manually create a backup of resource files.
/// </summary>
public class BackupCreateCommand : AsyncCommand<BackupCreateCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandOption("--file <FILE>")]
        [Description("Resource file name to backup (e.g., SharedResource.resx)")]
        public string? FileName { get; set; }

        [CommandOption("--all")]
        [Description("Create backups for all resource files")]
        public bool AllFiles { get; set; }

        [CommandOption("--operation <DESCRIPTION>")]
        [Description("Description of why this backup is being created")]
        [DefaultValue("manual-backup")]
        public string Operation { get; set; } = "manual-backup";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = settings.GetResourcePath();
            var manager = new ResourceDiscovery();

            if (!settings.AllFiles && string.IsNullOrWhiteSpace(settings.FileName))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Either --file or --all must be specified.");
                return 1;
            }

            var backupManager = new BackupVersionManager(10);

            if (settings.AllFiles)
            {
                var languages = manager.DiscoverLanguages(basePath);
                if (!languages.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No resource files found.[/]");
                    return 1;
                }

                AnsiConsole.MarkupLine($"[blue]Creating backups for {languages.Count} file(s)...[/]");
                AnsiConsole.WriteLine();

                var succeeded = 0;
                var failed = 0;

                foreach (var lang in languages)
                {
                    try
                    {
                        var metadata = await backupManager.CreateBackupAsync(
                            lang.FilePath,
                            settings.Operation,
                            basePath);

                        AnsiConsole.MarkupLine($"[green]✓[/] {Path.GetFileName(lang.FilePath)} → v{metadata.Version:D3} ({metadata.KeyCount} keys)");
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] {Path.GetFileName(lang.FilePath)}: {ex.Message}");
                        failed++;
                    }
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]Succeeded: {succeeded}[/], [red]Failed: {failed}[/]");
            }
            else
            {
                var filePath = FindResourceFile(settings.FileName!, basePath);
                if (filePath == null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Resource file '{settings.FileName}' not found.");
                    return 1;
                }

                var metadata = await backupManager.CreateBackupAsync(
                    filePath,
                    settings.Operation,
                    basePath);

                AnsiConsole.MarkupLine($"[green]Backup created successfully![/]");
                AnsiConsole.WriteLine();
                DisplayBackupInfo(metadata);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private string? FindResourceFile(string fileName, string basePath)
    {
        var manager = new ResourceDiscovery();
        var languages = manager.DiscoverLanguages(basePath);

        var file = languages.FirstOrDefault(l =>
            Path.GetFileName(l.FilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        return file?.FilePath;
    }

    private void DisplayBackupInfo(LocalizationManager.Shared.Models.BackupMetadata metadata)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Version", $"v{metadata.Version:D3}");
        table.AddRow("Timestamp", metadata.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        table.AddRow("Operation", metadata.Operation);
        table.AddRow("User", metadata.User ?? "-");
        table.AddRow("Key Count", metadata.KeyCount.ToString());
        table.AddRow("Changed Keys", metadata.ChangedKeys > 0 ? metadata.ChangedKeys.ToString() : "-");
        table.AddRow("Hash", metadata.Hash[..16] + "...");

        AnsiConsole.Write(table);
    }
}
