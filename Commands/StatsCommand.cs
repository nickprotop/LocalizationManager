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

namespace LocalizationManager.Commands;

/// <summary>
/// Command to display statistics about resource files and translation coverage.
/// </summary>
public class StatsCommand : Command<BaseFormattableCommandSettings>
{
    public override int Execute(CommandContext context, BaseFormattableCommandSettings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();
        var format = settings.GetOutputFormat();
        var isTableFormat = format == OutputFormat.Table;

        if (isTableFormat)
        {
            AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
            AnsiConsole.WriteLine();
        }

        try
        {
            // Discover languages
            var discovery = new ResourceDiscovery();
            var languages = discovery.DiscoverLanguages(resourcePath);

            if (!languages.Any())
            {
                if (isTableFormat)
                {
                    AnsiConsole.MarkupLine("[red]✗ No .resx files found![/]");
                }
                else
                {
                    Console.Error.WriteLine("No .resx files found!");
                }
                return 1;
            }

            // Parse resource files
            var parser = new ResourceFileParser();
            var resourceFiles = new List<LocalizationManager.Core.Models.ResourceFile>();

            foreach (var lang in languages)
            {
                try
                {
                    var resourceFile = parser.Parse(lang);
                    resourceFiles.Add(resourceFile);
                }
                catch (Exception ex)
                {
                    if (isTableFormat)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Error parsing {lang.Name}: {ex.Message}[/]");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error parsing {lang.Name}: {ex.Message}");
                    }
                    return 1;
                }
            }

            // Display statistics based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(resourceFiles);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(resourceFiles, settings);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(resourceFiles, settings);
                    break;
            }

            return 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[red]✗ {ex.Message}[/]");
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[red]✗ Unexpected error: {ex.Message}[/]");
            }
            else
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            }
            return 1;
        }
    }

    private void DisplayConfigNotice(BaseFormattableCommandSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.LoadedConfigurationPath))
        {
            AnsiConsole.MarkupLine($"[dim]Using configuration from: {settings.LoadedConfigurationPath}[/]");
            AnsiConsole.WriteLine();
        }
    }

    private void DisplayTable(List<LocalizationManager.Core.Models.ResourceFile> resourceFiles, BaseFormattableCommandSettings settings)
    {
        DisplayConfigNotice(settings);

        // Create statistics table
        var table = new Table();
        table.Title = new TableTitle("[bold]Localization Statistics[/]");
        table.AddColumn("Language");
        table.AddColumn("Total Keys");
        table.AddColumn("Completed");
        table.AddColumn("Empty");
        table.AddColumn("Coverage");
        table.AddColumn("File Size");

        foreach (var rf in resourceFiles)
        {
            var fileInfo = new FileInfo(rf.Language.FilePath);
            var fileSizeKb = (fileInfo.Length / 1024.0).ToString("F1");
            var emptyCount = rf.Count - rf.CompletedCount;

            // Color code coverage
            var coverageColor = rf.CompletionPercentage >= 100 ? "green" :
                               rf.CompletionPercentage >= 80 ? "yellow" :
                               "red";

            var languageDisplay = rf.Language.IsDefault
                ? $"[yellow]{rf.Language.Name}[/]"
                : rf.Language.Name;

            table.AddRow(
                languageDisplay,
                rf.Count.ToString(),
                rf.CompletedCount.ToString(),
                emptyCount.ToString(),
                $"[{coverageColor}]{rf.CompletionPercentage:F1}%[/]",
                $"{fileSizeKb} KB"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Display coverage chart
        if (resourceFiles.Count > 1)
        {
            AnsiConsole.MarkupLine("[bold]Translation Coverage:[/]");
            foreach (var rf in resourceFiles.Where(r => !r.Language.IsDefault))
            {
                var barChart = new BarChart()
                    .Width(60)
                    .Label($"[bold]{rf.Language.Name}[/]")
                    .AddItem("Completed", rf.CompletedCount, Color.Green)
                    .AddItem("Empty", rf.Count - rf.CompletedCount, Color.Red);

                AnsiConsole.Write(barChart);
            }
        }
    }

    private void DisplayJson(List<LocalizationManager.Core.Models.ResourceFile> resourceFiles)
    {
        var stats = resourceFiles.Select(rf => new
        {
            language = rf.Language.Name,
            isDefault = rf.Language.IsDefault,
            totalKeys = rf.Count,
            completedKeys = rf.CompletedCount,
            emptyKeys = rf.Count - rf.CompletedCount,
            coveragePercentage = rf.CompletionPercentage,
            filePath = rf.Language.FilePath,
            fileSizeBytes = new FileInfo(rf.Language.FilePath).Length
        }).ToList();

        var output = new
        {
            totalLanguages = resourceFiles.Count,
            statistics = stats
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplaySimple(List<LocalizationManager.Core.Models.ResourceFile> resourceFiles, BaseFormattableCommandSettings settings)
    {
        Console.WriteLine("Localization Statistics");
        Console.WriteLine("======================");
        Console.WriteLine();

        foreach (var rf in resourceFiles)
        {
            var fileInfo = new FileInfo(rf.Language.FilePath);
            var fileSizeKb = (fileInfo.Length / 1024.0).ToString("F1");
            var emptyCount = rf.Count - rf.CompletedCount;
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            var defaultMarker = rf.Language.IsDefault ? $" ({defaultCode})" : "";

            Console.WriteLine($"{rf.Language.Name}{defaultMarker}");
            Console.WriteLine($"  Total Keys:    {rf.Count}");
            Console.WriteLine($"  Completed:     {rf.CompletedCount}");
            Console.WriteLine($"  Empty:         {emptyCount}");
            Console.WriteLine($"  Coverage:      {rf.CompletionPercentage:F1}%");
            Console.WriteLine($"  File Size:     {fileSizeKb} KB");
            Console.WriteLine();
        }
    }
}
