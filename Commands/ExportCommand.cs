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
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to export resource files to various formats.
/// </summary>
public class ExportCommandSettings : BaseFormattableCommandSettings
{
    [CommandOption("-o|--output <FILE>")]
    [Description("Output file path (default: resources.csv for csv, resources.json for json, resources.txt for simple)")]
    public string? OutputFile { get; set; }

    [CommandOption("--include-status")]
    [Description("Include validation status in export")]
    public bool IncludeStatus { get; set; }

    /// <summary>
    /// Gets the output file path with appropriate extension based on format.
    /// </summary>
    public string GetOutputFilePath()
    {
        if (!string.IsNullOrEmpty(OutputFile))
            return OutputFile;

        var format = GetOutputFormat();
        return format switch
        {
            OutputFormat.Json => "resources.json",
            OutputFormat.Simple => "resources.txt",
            _ => "resources.csv"
        };
    }
}

public class ExportCommand : Command<ExportCommandSettings>
{
    public override int Execute(CommandContext context, ExportCommandSettings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();
        var format = settings.GetOutputFormat();
        var isTableFormat = format == OutputFormat.Table;

        if (isTableFormat)
        {
            if (!string.IsNullOrEmpty(settings.LoadedConfigurationPath))
            {
                AnsiConsole.MarkupLine($"[dim]Using configuration from: {settings.LoadedConfigurationPath}[/]");
                AnsiConsole.WriteLine();
            }
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
            var resourceFiles = new List<ResourceFile>();

            foreach (var lang in languages)
            {
                try
                {
                    var resourceFile = parser.Parse(lang);
                    resourceFiles.Add(resourceFile);
                    if (isTableFormat)
                    {
                        AnsiConsole.MarkupLine($"[dim]Parsed {lang.Name}: {resourceFile.Count} entries[/]");
                    }
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

            if (isTableFormat)
            {
                AnsiConsole.WriteLine();
            }

            // Validate if status requested
            Core.Models.ValidationResult? validationResult = null;
            if (settings.IncludeStatus)
            {
                var validator = new ResourceValidator();
                validationResult = validator.Validate(resourceFiles);
            }

            // Export based on format
            var outputFile = settings.GetOutputFilePath();

            switch (format)
            {
                case OutputFormat.Json:
                    ExportToJson(resourceFiles, outputFile, validationResult);
                    break;
                case OutputFormat.Simple:
                    ExportToSimple(resourceFiles, outputFile, validationResult);
                    break;
                case OutputFormat.Table:
                default:
                    ExportToCsv(resourceFiles, outputFile, validationResult);
                    break;
            }

            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[green]✓ Exported {resourceFiles.First().Count} keys to:[/]");
                AnsiConsole.MarkupLine($"  [cyan]{Path.GetFullPath(outputFile)}[/]");
            }
            else
            {
                Console.Error.WriteLine($"Exported {resourceFiles.First().Count} keys to: {Path.GetFullPath(outputFile)}");
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

    private void ExportToCsv(List<ResourceFile> resourceFiles, string outputFile, Core.Models.ValidationResult? validationResult)
    {
        var sb = new StringBuilder();

        // Build header
        var header = new List<string> { "Key" };
        header.AddRange(resourceFiles.Select(rf => rf.Language.Name));
        if (validationResult != null)
        {
            header.Add("Status");
        }
        header.Add("Comment");

        sb.AppendLine(EscapeCsvRow(header));

        // Get all unique keys from default language
        var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null)
        {
            throw new InvalidOperationException("No default language found");
        }

        var allKeys = defaultFile.Entries.Select(e => e.Key).OrderBy(k => k).ToList();

        // Build rows
        foreach (var key in allKeys)
        {
            var row = new List<string> { key };

            // Add values for each language
            foreach (var resourceFile in resourceFiles)
            {
                var entry = resourceFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                row.Add(entry?.Value ?? string.Empty);
            }

            // Add status if requested
            if (validationResult != null)
            {
                var status = GetKeyStatus(key, resourceFiles, validationResult);
                row.Add(status);
            }

            // Add comment (from default language)
            var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            row.Add(defaultEntry?.Comment ?? string.Empty);

            sb.AppendLine(EscapeCsvRow(row));
        }

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
    }

    private string GetKeyStatus(string key, List<ResourceFile> resourceFiles, Core.Models.ValidationResult validationResult)
    {
        var statuses = new List<string>();

        foreach (var rf in resourceFiles.Where(r => !r.Language.IsDefault))
        {
            var langCode = rf.Language.Code;

            if (validationResult.MissingKeys.ContainsKey(langCode) &&
                validationResult.MissingKeys[langCode].Contains(key))
            {
                statuses.Add($"Missing in {langCode}");
            }

            if (validationResult.EmptyValues.ContainsKey(langCode) &&
                validationResult.EmptyValues[langCode].Contains(key))
            {
                statuses.Add($"Empty in {langCode}");
            }

            if (validationResult.DuplicateKeys.ContainsKey(langCode) &&
                validationResult.DuplicateKeys[langCode].Contains(key))
            {
                statuses.Add($"Duplicate in {langCode}");
            }
        }

        return statuses.Any() ? string.Join("; ", statuses) : "OK";
    }

    private string EscapeCsvRow(List<string> fields)
    {
        var escapedFields = fields.Select(f =>
        {
            if (string.IsNullOrEmpty(f))
            {
                return string.Empty;
            }

            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (f.Contains(',') || f.Contains('"') || f.Contains('\n') || f.Contains('\r'))
            {
                return $"\"{f.Replace("\"", "\"\"")}\"";
            }

            return f;
        });

        return string.Join(",", escapedFields);
    }

    private void ExportToJson(List<ResourceFile> resourceFiles, string outputFile, Core.Models.ValidationResult? validationResult)
    {
        var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null)
        {
            throw new InvalidOperationException("No default language found");
        }

        var allKeys = defaultFile.Entries.Select(e => e.Key).OrderBy(k => k).ToList();

        var exportData = allKeys.Select(key =>
        {
            var translations = new Dictionary<string, string?>();
            foreach (var resourceFile in resourceFiles)
            {
                var entry = resourceFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                translations[resourceFile.Language.Name] = entry?.Value;
            }

            var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            var item = new Dictionary<string, object?>
            {
                ["key"] = key,
                ["translations"] = translations,
                ["comment"] = defaultEntry?.Comment
            };

            if (validationResult != null)
            {
                item["status"] = GetKeyStatus(key, resourceFiles, validationResult);
            }

            return item;
        }).ToList();

        var output = new
        {
            languages = resourceFiles.Select(rf => rf.Language.Name).ToList(),
            totalKeys = allKeys.Count,
            entries = exportData
        };

        File.WriteAllText(outputFile, OutputFormatter.FormatJson(output), Encoding.UTF8);
    }

    private void ExportToSimple(List<ResourceFile> resourceFiles, string outputFile, Core.Models.ValidationResult? validationResult)
    {
        var sb = new StringBuilder();
        var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null)
        {
            throw new InvalidOperationException("No default language found");
        }

        var allKeys = defaultFile.Entries.Select(e => e.Key).OrderBy(k => k).ToList();

        // Header
        sb.AppendLine("Resource Export");
        sb.AppendLine($"Languages: {string.Join(", ", resourceFiles.Select(rf => rf.Language.Name))}");
        sb.AppendLine($"Total Keys: {allKeys.Count}");
        sb.AppendLine();
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        // Keys
        foreach (var key in allKeys)
        {
            sb.AppendLine($"Key: {key}");

            // Add translations for each language
            foreach (var resourceFile in resourceFiles)
            {
                var entry = resourceFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                var value = entry?.Value ?? "(empty)";
                sb.AppendLine($"  {resourceFile.Language.Name}: {value}");
            }

            // Add comment
            var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(defaultEntry?.Comment))
            {
                sb.AppendLine($"  Comment: {defaultEntry.Comment}");
            }

            // Add status if requested
            if (validationResult != null)
            {
                var status = GetKeyStatus(key, resourceFiles, validationResult);
                sb.AppendLine($"  Status: {status}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
    }
}
