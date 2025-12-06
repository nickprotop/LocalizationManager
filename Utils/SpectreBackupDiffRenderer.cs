using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Enums;
using Spectre.Console;

namespace LocalizationManager.Utils;

/// <summary>
/// Renders backup diff results to the console using Spectre.Console.
/// </summary>
public static class SpectreBackupDiffRenderer
{
    /// <summary>
    /// Displays diff result in the console using Spectre.Console.
    /// </summary>
    public static void DisplayInConsole(BackupDiffResult diff, bool showUnchanged = false)
    {
        var formatter = new BackupDiffFormatter();

        // Header
        var rule = new Rule($"[blue]Diff: Version {diff.VersionA.Version} → Version {diff.VersionB.Version}[/]");
        rule.LeftJustified();
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        // Statistics table
        var statsTable = new Table();
        statsTable.Border(TableBorder.Rounded);
        statsTable.AddColumn("Metric");
        statsTable.AddColumn(new TableColumn("Count").RightAligned());

        statsTable.AddRow("Total keys", $"{diff.Statistics.TotalKeys}");
        statsTable.AddRow("[green]Added[/]", $"[green]{diff.Statistics.AddedCount}[/]");
        statsTable.AddRow("[yellow]Modified[/]", $"[yellow]{diff.Statistics.ModifiedCount}[/]");
        statsTable.AddRow("[red]Deleted[/]", $"[red]{diff.Statistics.DeletedCount}[/]");
        statsTable.AddRow("[cyan]Comment changes[/]", $"[cyan]{diff.Statistics.CommentChangedCount}[/]");
        statsTable.AddRow("Unchanged", $"{diff.Statistics.UnchangedCount}");

        AnsiConsole.Write(statsTable);
        AnsiConsole.WriteLine();

        // Changes
        if (diff.Changes.Any())
        {
            AnsiConsole.MarkupLine("[bold]Changes:[/]");
            AnsiConsole.WriteLine();

            foreach (var change in diff.Changes)
            {
                if (change.Type == ChangeType.Unchanged && !showUnchanged)
                {
                    continue;
                }

                var (color, symbol) = change.Type switch
                {
                    ChangeType.Added => ("green", "+"),
                    ChangeType.Modified => ("yellow", "~"),
                    ChangeType.Deleted => ("red", "-"),
                    ChangeType.CommentChanged => ("cyan", "#"),
                    _ => ("gray", " ")
                };

                AnsiConsole.MarkupLine($"[{color}]{symbol}[/] [bold]{Markup.Escape(change.Key)}[/]");

                if (change.Type == ChangeType.Added)
                {
                    AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(formatter.TruncateValue(change.NewValue))}[/]");
                }
                else if (change.Type == ChangeType.Deleted)
                {
                    AnsiConsole.MarkupLine($"  [red]- {Markup.Escape(formatter.TruncateValue(change.OldValue))}[/]");
                }
                else if (change.Type == ChangeType.Modified)
                {
                    AnsiConsole.MarkupLine($"  [red]- {Markup.Escape(formatter.TruncateValue(change.OldValue))}[/]");
                    AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(formatter.TruncateValue(change.NewValue))}[/]");
                }
                else if (change.Type == ChangeType.CommentChanged)
                {
                    AnsiConsole.MarkupLine($"  [cyan]Comment changed[/]");
                }

                AnsiConsole.WriteLine();
            }
        }
    }
}
