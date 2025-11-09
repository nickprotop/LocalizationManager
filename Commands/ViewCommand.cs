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
using LocalizationManager.Core.Enums;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to view details of a specific localization key.
/// </summary>
public class ViewCommand : Command<ViewCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("The key to view")]
        public required string Key { get; set; }

        [CommandOption("--show-comments")]
        [Description("Show comments for the key")]
        public bool ShowComments { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        try
        {
            var format = settings.GetOutputFormat();
            if (format == OutputFormat.Table)
            {
                AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
                AnsiConsole.WriteLine();
            }

            // Discover languages
            var discovery = new ResourceDiscovery();
            var languages = discovery.DiscoverLanguages(resourcePath);

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine("[red]✗ No .resx files found![/]");
                return 1;
            }

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

            // Check if key exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                AnsiConsole.MarkupLine("[red]✗ No default language file found![/]");
                return 1;
            }

            var existingEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == settings.Key);
            if (existingEntry == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key}' not found![/]");
                return 1;
            }

            // Display based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(settings.Key, resourceFiles, settings.ShowComments);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(settings.Key, resourceFiles, settings.ShowComments);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(settings.Key, resourceFiles, settings.ShowComments);
                    break;
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

    private void DisplayTable(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        AnsiConsole.MarkupLine($"[yellow]Key:[/] [bold]{key}[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Language");
        table.AddColumn("Value");
        if (showComments)
        {
            table.AddColumn("Comment");
        }

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
            if (entry != null)
            {
                var langName = rf.Language.IsDefault
                    ? $"{rf.Language.Name} [yellow](default)[/]"
                    : rf.Language.Name;

                var value = entry.IsEmpty
                    ? "[dim](empty)[/]"
                    : entry.Value;

                if (showComments)
                {
                    var comment = string.IsNullOrWhiteSpace(entry.Comment)
                        ? "[dim](no comment)[/]"
                        : entry.Comment;
                    table.AddRow(langName, value ?? "", comment);
                }
                else
                {
                    table.AddRow(langName, value ?? "");
                }
            }
            else
            {
                var langName = rf.Language.IsDefault
                    ? $"{rf.Language.Name} [yellow](default)[/]"
                    : rf.Language.Name;

                if (showComments)
                {
                    table.AddRow(langName, "[red](missing)[/]", "[dim](no comment)[/]");
                }
                else
                {
                    table.AddRow(langName, "[red](missing)[/]");
                }
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Show statistics
        var total = resourceFiles.Count;
        var present = resourceFiles.Count(rf => rf.Entries.Any(e => e.Key == key));
        var empty = resourceFiles.Count(rf => rf.Entries.Any(e => e.Key == key && e.IsEmpty));

        AnsiConsole.MarkupLine($"[dim]Present in {present}/{total} language(s), {empty} empty value(s)[/]");
    }

    private void DisplayJson(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        var translations = new Dictionary<string, object?>();

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
            var langCode = rf.Language.GetDisplayCode();

            if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
            {
                translations[langCode] = new
                {
                    value = entry.Value,
                    comment = entry.Comment
                };
            }
            else
            {
                translations[langCode] = entry?.Value;
            }
        }

        var output = new
        {
            key = key,
            translations = translations
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplaySimple(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        Console.WriteLine($"Key: {key}");
        Console.WriteLine();

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
            var langLabel = rf.Language.IsDefault ? $"{rf.Language.Name} (default)" : rf.Language.Name;
            var value = entry?.Value ?? "(missing)";

            Console.WriteLine($"{langLabel}: {value}");

            if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
            {
                Console.WriteLine($"  Comment: {entry.Comment}");
            }
        }
    }
}
