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

using System.Text;
using System.Text.Json;
using LocalizationManager.Shared.Enums;
using Spectre.Console;

namespace LocalizationManager.Core.Backup;

/// <summary>
/// Formats backup diff results in various output formats.
/// </summary>
public class BackupDiffFormatter
{
    /// <summary>
    /// Formats diff result as plain text.
    /// </summary>
    public string FormatAsText(BackupDiffResult diff, bool colorize = false)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"Diff: Version {diff.VersionA.Version} → Version {diff.VersionB.Version}");
        sb.AppendLine($"Time: {diff.VersionA.Timestamp:yyyy-MM-dd HH:mm:ss} → {diff.VersionB.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Statistics
        sb.AppendLine("Statistics:");
        sb.AppendLine($"  Total keys: {diff.Statistics.TotalKeys}");
        sb.AppendLine($"  Added:      {diff.Statistics.AddedCount}");
        sb.AppendLine($"  Modified:   {diff.Statistics.ModifiedCount}");
        sb.AppendLine($"  Deleted:    {diff.Statistics.DeletedCount}");
        sb.AppendLine($"  Comments:   {diff.Statistics.CommentChangedCount}");
        sb.AppendLine($"  Unchanged:  {diff.Statistics.UnchangedCount}");
        sb.AppendLine();

        // Changes
        if (diff.Changes.Any())
        {
            sb.AppendLine("Changes:");
            sb.AppendLine();

            foreach (var change in diff.Changes)
            {
                var prefix = change.Type switch
                {
                    ChangeType.Added => "+ ",
                    ChangeType.Modified => "~ ",
                    ChangeType.Deleted => "- ",
                    ChangeType.CommentChanged => "# ",
                    ChangeType.Unchanged => "  ",
                    _ => "  "
                };

                sb.AppendLine($"{prefix}{change.Key}");

                if (change.Type == ChangeType.Added)
                {
                    sb.AppendLine($"    New: {TruncateValue(change.NewValue)}");
                    if (!string.IsNullOrEmpty(change.NewComment))
                    {
                        sb.AppendLine($"    Comment: {change.NewComment}");
                    }
                }
                else if (change.Type == ChangeType.Deleted)
                {
                    sb.AppendLine($"    Old: {TruncateValue(change.OldValue)}");
                }
                else if (change.Type == ChangeType.Modified)
                {
                    sb.AppendLine($"    Old: {TruncateValue(change.OldValue)}");
                    sb.AppendLine($"    New: {TruncateValue(change.NewValue)}");
                }
                else if (change.Type == ChangeType.CommentChanged)
                {
                    sb.AppendLine($"    Old comment: {change.OldComment}");
                    sb.AppendLine($"    New comment: {change.NewComment}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats diff result as JSON.
    /// </summary>
    public string FormatAsJson(BackupDiffResult diff)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(diff, options);
    }

    /// <summary>
    /// Formats diff result as HTML.
    /// </summary>
    public string FormatAsHtml(BackupDiffResult diff)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <title>Backup Diff Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("    h1 { color: #333; }");
        sb.AppendLine("    .stats { background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0; }");
        sb.AppendLine("    .stats table { border-collapse: collapse; }");
        sb.AppendLine("    .stats td { padding: 5px 15px; }");
        sb.AppendLine("    .change { margin: 10px 0; padding: 10px; border-left: 4px solid #ccc; }");
        sb.AppendLine("    .added { border-color: #28a745; background: #d4edda; }");
        sb.AppendLine("    .modified { border-color: #ffc107; background: #fff3cd; }");
        sb.AppendLine("    .deleted { border-color: #dc3545; background: #f8d7da; }");
        sb.AppendLine("    .comment { border-color: #17a2b8; background: #d1ecf1; }");
        sb.AppendLine("    .key { font-weight: bold; color: #333; }");
        sb.AppendLine("    .value { font-family: monospace; color: #666; margin: 5px 0; }");
        sb.AppendLine("    .old { text-decoration: line-through; color: #999; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine($"  <h1>Backup Diff Report</h1>");
        sb.AppendLine($"  <p><strong>Version {diff.VersionA.Version}</strong> ({diff.VersionA.Timestamp:yyyy-MM-dd HH:mm:ss}) → <strong>Version {diff.VersionB.Version}</strong> ({diff.VersionB.Timestamp:yyyy-MM-dd HH:mm:ss})</p>");

        // Statistics
        sb.AppendLine("  <div class=\"stats\">");
        sb.AppendLine("    <h2>Statistics</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine($"      <tr><td>Total keys:</td><td><strong>{diff.Statistics.TotalKeys}</strong></td></tr>");
        sb.AppendLine($"      <tr><td>Added:</td><td style=\"color: #28a745;\"><strong>{diff.Statistics.AddedCount}</strong></td></tr>");
        sb.AppendLine($"      <tr><td>Modified:</td><td style=\"color: #ffc107;\"><strong>{diff.Statistics.ModifiedCount}</strong></td></tr>");
        sb.AppendLine($"      <tr><td>Deleted:</td><td style=\"color: #dc3545;\"><strong>{diff.Statistics.DeletedCount}</strong></td></tr>");
        sb.AppendLine($"      <tr><td>Comment changes:</td><td style=\"color: #17a2b8;\"><strong>{diff.Statistics.CommentChangedCount}</strong></td></tr>");
        sb.AppendLine($"      <tr><td>Unchanged:</td><td>{diff.Statistics.UnchangedCount}</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </div>");

        // Changes
        if (diff.Changes.Any())
        {
            sb.AppendLine("  <h2>Changes</h2>");

            foreach (var change in diff.Changes)
            {
                var cssClass = change.Type switch
                {
                    ChangeType.Added => "added",
                    ChangeType.Modified => "modified",
                    ChangeType.Deleted => "deleted",
                    ChangeType.CommentChanged => "comment",
                    _ => ""
                };

                sb.AppendLine($"  <div class=\"change {cssClass}\">");
                sb.AppendLine($"    <div class=\"key\">{EscapeHtml(change.Key)}</div>");

                if (change.Type == ChangeType.Added)
                {
                    sb.AppendLine($"    <div class=\"value\">+ {EscapeHtml(change.NewValue)}</div>");
                    if (!string.IsNullOrEmpty(change.NewComment))
                    {
                        sb.AppendLine($"    <div><em>Comment: {EscapeHtml(change.NewComment)}</em></div>");
                    }
                }
                else if (change.Type == ChangeType.Deleted)
                {
                    sb.AppendLine($"    <div class=\"value old\">- {EscapeHtml(change.OldValue)}</div>");
                }
                else if (change.Type == ChangeType.Modified)
                {
                    sb.AppendLine($"    <div class=\"value old\">- {EscapeHtml(change.OldValue)}</div>");
                    sb.AppendLine($"    <div class=\"value\">+ {EscapeHtml(change.NewValue)}</div>");
                }
                else if (change.Type == ChangeType.CommentChanged)
                {
                    sb.AppendLine($"    <div class=\"value old\"><em>- {EscapeHtml(change.OldComment)}</em></div>");
                    sb.AppendLine($"    <div class=\"value\"><em>+ {EscapeHtml(change.NewComment)}</em></div>");
                }

                sb.AppendLine("  </div>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Displays diff result in the console using Spectre.Console.
    /// </summary>
    public void DisplayInConsole(BackupDiffResult diff, bool showUnchanged = false)
    {
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
                    AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(TruncateValue(change.NewValue))}[/]");
                }
                else if (change.Type == ChangeType.Deleted)
                {
                    AnsiConsole.MarkupLine($"  [red]- {Markup.Escape(TruncateValue(change.OldValue))}[/]");
                }
                else if (change.Type == ChangeType.Modified)
                {
                    AnsiConsole.MarkupLine($"  [red]- {Markup.Escape(TruncateValue(change.OldValue))}[/]");
                    AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(TruncateValue(change.NewValue))}[/]");
                }
                else if (change.Type == ChangeType.CommentChanged)
                {
                    AnsiConsole.MarkupLine($"  [cyan]Comment changed[/]");
                }

                AnsiConsole.WriteLine();
            }
        }
    }

    private string TruncateValue(string? value, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }

    private string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
