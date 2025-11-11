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
/// Command to validate resource files for missing keys, duplicates, and empty values.
/// </summary>
public class ValidateCommand : Command<BaseFormattableCommandSettings>
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

            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[green]✓ Found {languages.Count} language(s):[/]");
                foreach (var lang in languages)
                {
                    var marker = lang.IsDefault ? "[yellow](default)[/]" : "";
                    AnsiConsole.MarkupLine($"  • {lang.Name} {marker}");
                }
                AnsiConsole.WriteLine();
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

            // Validate
            var validator = new ResourceValidator();
            var validationResult = validator.Validate(resourceFiles);

            // Display results based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(validationResult);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(validationResult, settings);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(validationResult, settings);
                    break;
            }

            return validationResult.IsValid ? 0 : 1;
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

    private void DisplayTable(LocalizationManager.Core.Models.ValidationResult result, BaseFormattableCommandSettings settings)
    {
        DisplayConfigNotice(settings);

        if (result.IsValid)
        {
            AnsiConsole.MarkupLine("[green bold]✓ All validations passed![/]");
            AnsiConsole.MarkupLine("[green]No issues found.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow bold]⚠ Validation found {result.TotalIssues} issue(s)[/]");
        AnsiConsole.WriteLine();

        // Missing keys
        if (result.MissingKeys.Any(kv => kv.Value.Any()))
        {
            var table = new Table();
            table.Title = new TableTitle("[red]Missing Translations[/]");
            table.AddColumn("Language");
            table.AddColumn("Missing Keys");

            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.MissingKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                table.AddRow(
                    langDisplay,
                    string.Join(", ", kvp.Value.Take(10)) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Extra keys
        if (result.ExtraKeys.Any(kv => kv.Value.Any()))
        {
            var table = new Table();
            table.Title = new TableTitle("[yellow]Extra Keys (not in default)[/]");
            table.AddColumn("Language");
            table.AddColumn("Extra Keys");

            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.ExtraKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                table.AddRow(
                    langDisplay,
                    string.Join(", ", kvp.Value.Take(10)) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Duplicate keys
        if (result.DuplicateKeys.Any(kv => kv.Value.Any()))
        {
            var table = new Table();
            table.Title = new TableTitle("[red]Duplicate Keys[/]");
            table.AddColumn("Language");
            table.AddColumn("Duplicate Keys");

            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.DuplicateKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                table.AddRow(
                    langDisplay,
                    string.Join(", ", kvp.Value)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Empty values
        if (result.EmptyValues.Any(kv => kv.Value.Any()))
        {
            var table = new Table();
            table.Title = new TableTitle("[yellow]Empty Values[/]");
            table.AddColumn("Language");
            table.AddColumn("Empty Keys");

            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.EmptyValues.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                table.AddRow(
                    langDisplay,
                    string.Join(", ", kvp.Value.Take(10)) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
                );
            }

            AnsiConsole.Write(table);
        }
    }

    private void DisplayJson(LocalizationManager.Core.Models.ValidationResult result)
    {
        // Normalize language codes for display (empty string -> "default")
        var normalizedMissingKeys = result.MissingKeys.ToDictionary(
            kvp => string.IsNullOrEmpty(kvp.Key) ? "default" : kvp.Key,
            kvp => kvp.Value
        );
        var normalizedExtraKeys = result.ExtraKeys.ToDictionary(
            kvp => string.IsNullOrEmpty(kvp.Key) ? "default" : kvp.Key,
            kvp => kvp.Value
        );
        var normalizedDuplicateKeys = result.DuplicateKeys.ToDictionary(
            kvp => string.IsNullOrEmpty(kvp.Key) ? "default" : kvp.Key,
            kvp => kvp.Value
        );
        var normalizedEmptyValues = result.EmptyValues.ToDictionary(
            kvp => string.IsNullOrEmpty(kvp.Key) ? "default" : kvp.Key,
            kvp => kvp.Value
        );

        var output = new
        {
            isValid = result.IsValid,
            totalIssues = result.TotalIssues,
            missingKeys = normalizedMissingKeys,
            extraKeys = normalizedExtraKeys,
            duplicateKeys = normalizedDuplicateKeys,
            emptyValues = normalizedEmptyValues
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplaySimple(LocalizationManager.Core.Models.ValidationResult result, BaseFormattableCommandSettings settings)
    {
        if (result.IsValid)
        {
            Console.WriteLine("✓ All validations passed!");
            Console.WriteLine("No issues found.");
            return;
        }

        Console.WriteLine($"⚠ Validation found {result.TotalIssues} issue(s)");
        Console.WriteLine();

        // Missing keys
        if (result.MissingKeys.Any(kv => kv.Value.Any()))
        {
            Console.WriteLine("Missing Translations:");
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.MissingKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                Console.WriteLine($"  {langDisplay}: {string.Join(", ", kvp.Value)}");
            }
            Console.WriteLine();
        }

        // Extra keys
        if (result.ExtraKeys.Any(kv => kv.Value.Any()))
        {
            Console.WriteLine("Extra Keys (not in default):");
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.ExtraKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                Console.WriteLine($"  {langDisplay}: {string.Join(", ", kvp.Value)}");
            }
            Console.WriteLine();
        }

        // Duplicate keys
        if (result.DuplicateKeys.Any(kv => kv.Value.Any()))
        {
            Console.WriteLine("Duplicate Keys:");
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.DuplicateKeys.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                Console.WriteLine($"  {langDisplay}: {string.Join(", ", kvp.Value)}");
            }
            Console.WriteLine();
        }

        // Empty values
        if (result.EmptyValues.Any(kv => kv.Value.Any()))
        {
            Console.WriteLine("Empty Values:");
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.EmptyValues.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                Console.WriteLine($"  {langDisplay}: {string.Join(", ", kvp.Value)}");
            }
        }
    }
}
