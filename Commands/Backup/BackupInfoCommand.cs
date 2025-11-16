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

using LocalizationManager.Core.Backup;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Shared.Enums;
using System.ComponentModel;

namespace LocalizationManager.Commands.Backup;

/// <summary>
/// Command to display detailed information about a specific backup.
/// </summary>
public class BackupInfoCommand : AsyncCommand<BackupInfoCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "<FILE>")]
        [Description("Resource file name (e.g., SharedResource.resx)")]
        public required string FileName { get; set; }

        [CommandArgument(1, "<VERSION>")]
        [Description("Backup version number")]
        public int Version { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = settings.GetResourcePath();
            var backupManager = new BackupVersionManager(10);

            var backup = await backupManager.GetBackupAsync(settings.FileName, settings.Version, basePath);
            if (backup == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Backup v{settings.Version} not found for {settings.FileName}");
                return 1;
            }

            var backupPath = await backupManager.GetBackupFilePathAsync(settings.FileName, settings.Version, basePath);

            AnsiConsole.Write(new Rule($"[blue]Backup v{settings.Version:D3} - {settings.FileName}[/]")
                .LeftJustified());
            AnsiConsole.WriteLine();

            var table = new Table();
            table.Border = TableBorder.Rounded;
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("Version", $"v{backup.Version:D3}");
            table.AddRow("Timestamp", backup.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            table.AddRow("Operation", backup.Operation);
            table.AddRow("User", backup.User ?? "-");
            table.AddRow("Key Count", backup.KeyCount.ToString());
            table.AddRow("Changed Keys", backup.ChangedKeys > 0 ? backup.ChangedKeys.ToString() : "-");
            table.AddRow("Hash (SHA256)", backup.Hash);
            table.AddRow("File Path", backupPath ?? "[dim]Not found[/]");

            if (backupPath != null && File.Exists(backupPath))
            {
                var fileInfo = new FileInfo(backupPath);
                table.AddRow("File Size", $"{fileInfo.Length / 1024.0:F2} KB");
            }

            AnsiConsole.Write(table);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
