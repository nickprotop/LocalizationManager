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
using LocalizationManager.Core.Scanning;
using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the validate command.
/// </summary>
public class ValidateCommandSettings : BaseFormattableCommandSettings
{
    [CommandOption("--placeholder-types <TYPES>")]
    [Description("Placeholder types to validate (dotnet, printf, icu, template, all). Comma-separated. Default: dotnet")]
    [DefaultValue(null)]
    public string? PlaceholderTypes { get; set; }

    [CommandOption("--no-placeholder-validation")]
    [Description("Disable placeholder validation")]
    [DefaultValue(false)]
    public bool NoPlaceholderValidation { get; set; }

    [CommandOption("--no-scan-code")]
    [Description("Disable code scanning when duplicates are found")]
    [DefaultValue(false)]
    public bool NoScanCode { get; set; }

    [CommandOption("--source-path <PATH>")]
    [Description("Source code path to scan for duplicate key usage (defaults to parent of resource path)")]
    public string? SourcePath { get; set; }

    /// <summary>
    /// Gets the enabled placeholder types based on CLI options and configuration.
    /// CLI options override configuration settings.
    /// </summary>
    public PlaceholderType GetEnabledPlaceholderTypes()
    {
        // If validation is disabled, return None
        if (NoPlaceholderValidation)
        {
            return PlaceholderType.None;
        }

        // If CLI option is provided, use it (highest priority)
        if (!string.IsNullOrEmpty(PlaceholderTypes))
        {
            return ParsePlaceholderTypes(PlaceholderTypes);
        }

        // If configuration has validation settings, use them
        if (LoadedConfiguration?.Validation != null)
        {
            if (!LoadedConfiguration.Validation.EnablePlaceholderValidation)
            {
                return PlaceholderType.None;
            }

            if (LoadedConfiguration.Validation.PlaceholderTypes != null &&
                LoadedConfiguration.Validation.PlaceholderTypes.Any())
            {
                return ParsePlaceholderTypes(string.Join(",", LoadedConfiguration.Validation.PlaceholderTypes));
            }
        }

        // Default: .NET format only
        return PlaceholderType.DotNetFormat;
    }

    private static PlaceholderType ParsePlaceholderTypes(string types)
    {
        var result = PlaceholderType.None;
        var typeList = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var type in typeList)
        {
            switch (type.ToLowerInvariant())
            {
                case "dotnet":
                    result |= PlaceholderType.DotNetFormat;
                    break;
                case "printf":
                    result |= PlaceholderType.PrintfStyle;
                    break;
                case "icu":
                    result |= PlaceholderType.IcuMessageFormat;
                    break;
                case "template":
                    result |= PlaceholderType.TemplateLiteral;
                    break;
                case "all":
                    return PlaceholderType.All;
                default:
                    throw new ArgumentException($"Unknown placeholder type: {type}. Valid values: dotnet, printf, icu, template, all");
            }
        }

        return result;
    }
}

/// <summary>
/// Command to validate resource files for missing keys, duplicates, and empty values.
/// </summary>
public class ValidateCommand : Command<ValidateCommandSettings>
{
    public override int Execute(CommandContext context, ValidateCommandSettings settings, CancellationToken cancellationToken = default)
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
            var enabledPlaceholderTypes = settings.GetEnabledPlaceholderTypes();
            var validationResult = validator.Validate(resourceFiles, enabledPlaceholderTypes);

            // Scan code for duplicate key usage if duplicates found and scanning not disabled
            if (validationResult.DuplicateKeys.Any(kv => kv.Value.Any()) && !settings.NoScanCode)
            {
                if (isTableFormat)
                {
                    AnsiConsole.MarkupLine("[dim]Scanning code for duplicate key usage...[/]");
                }

                ScanCodeForDuplicates(validationResult, resourceFiles, settings, resourcePath);

                if (isTableFormat)
                {
                    AnsiConsole.WriteLine();
                }
            }

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

    private void DisplayConfigNotice(ValidateCommandSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.LoadedConfigurationPath))
        {
            AnsiConsole.MarkupLine($"[dim]Using configuration from: {settings.LoadedConfigurationPath}[/]");
            AnsiConsole.WriteLine();
        }
    }

    private void DisplayTable(LocalizationManager.Core.Models.ValidationResult result, ValidateCommandSettings settings)
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
                    string.Join(", ", kvp.Value.Take(10).Select(k => k.EscapeMarkup())) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
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
                    string.Join(", ", kvp.Value.Take(10).Select(k => k.EscapeMarkup())) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
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
                    string.Join(", ", kvp.Value.Select(k => k.EscapeMarkup()))
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Display code usage for duplicates if scanned
            if (result.DuplicateKeyCodeUsages.Any())
            {
                AnsiConsole.MarkupLine("[yellow]Code Usage for Duplicate Keys:[/]");
                AnsiConsole.WriteLine();

                foreach (var kvp in result.DuplicateKeyCodeUsages)
                {
                    var usage = kvp.Value;
                    var variantsDisplay = string.Join(", ", usage.ResourceVariants.Select(v => $"[white]{v.EscapeMarkup()}[/]"));
                    AnsiConsole.MarkupLine($"  [yellow]•[/] Variants in resources: {variantsDisplay}");

                    if (usage.CodeScanned)
                    {
                        foreach (var variant in usage.ResourceVariants)
                        {
                            var refs = usage.CodeReferences.GetValueOrDefault(variant, new List<KeyReference>());
                            if (refs.Any())
                            {
                                var refLocations = refs.Take(3).Select(r => $"{Path.GetFileName(r.FilePath)}:{r.Line}");
                                var moreCount = refs.Count > 3 ? $" (+{refs.Count - 3} more)" : "";
                                AnsiConsole.MarkupLine($"    [green]✓[/] \"{variant.EscapeMarkup()}\" found in code: {string.Join(", ", refLocations)}{moreCount}");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"    [red]✗[/] \"{variant.EscapeMarkup()}\" [dim]not found in code[/]");
                            }
                        }

                        // Add guidance based on usage
                        if (usage.UsedVariants.Count > 1)
                        {
                            AnsiConsole.MarkupLine($"    [yellow]⚠ Multiple variants used in code! Standardize casing in code first.[/]");
                        }
                        else if (usage.UsedVariants.Count == 1 && usage.UnusedVariants.Any())
                        {
                            AnsiConsole.MarkupLine($"    [green]💡 Use 'lrm merge-duplicates {usage.UsedVariants.First().EscapeMarkup()}' to keep the used variant.[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("    [dim]Code scanning disabled[/]");
                    }

                    AnsiConsole.WriteLine();
                }
            }
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
                    string.Join(", ", kvp.Value.Take(10).Select(k => k.EscapeMarkup())) + (kvp.Value.Count > 10 ? $" ... ({kvp.Value.Count - 10} more)" : "")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Placeholder mismatches
        if (result.PlaceholderMismatches.Any(kv => kv.Value.Any()))
        {
            var table = new Table();
            table.Title = new TableTitle("[red]Placeholder Mismatches[/]");
            table.AddColumn("Language");
            table.AddColumn("Key");
            table.AddColumn("Error");

            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.PlaceholderMismatches.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                foreach (var error in kvp.Value.Take(10))
                {
                    table.AddRow(
                        langDisplay,
                        error.Key.EscapeMarkup(),
                        error.Value.EscapeMarkup()
                    );
                }
                if (kvp.Value.Count > 10)
                {
                    table.AddRow(
                        langDisplay,
                        "[dim]...[/]",
                        $"[dim]({kvp.Value.Count - 10} more errors)[/]"
                    );
                }
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
        var normalizedPlaceholderMismatches = result.PlaceholderMismatches.ToDictionary(
            kvp => string.IsNullOrEmpty(kvp.Key) ? "default" : kvp.Key,
            kvp => kvp.Value
        );

        // Normalize duplicate code usages for JSON output
        var normalizedDuplicateCodeUsages = result.DuplicateKeyCodeUsages.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                normalizedKey = kvp.Value.NormalizedKey,
                resourceVariants = kvp.Value.ResourceVariants,
                codeScanned = kvp.Value.CodeScanned,
                codeReferences = kvp.Value.CodeReferences.ToDictionary(
                    cr => cr.Key,
                    cr => cr.Value.Select(r => new { file = r.FilePath, line = r.Line }).ToList()
                ),
                usedVariants = kvp.Value.UsedVariants,
                unusedVariants = kvp.Value.UnusedVariants
            }
        );

        var output = new
        {
            isValid = result.IsValid,
            totalIssues = result.TotalIssues,
            missingKeys = normalizedMissingKeys,
            extraKeys = normalizedExtraKeys,
            duplicateKeys = normalizedDuplicateKeys,
            duplicateKeyCodeUsages = normalizedDuplicateCodeUsages,
            emptyValues = normalizedEmptyValues,
            placeholderMismatches = normalizedPlaceholderMismatches
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplaySimple(LocalizationManager.Core.Models.ValidationResult result, ValidateCommandSettings settings)
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

            // Display code usage for duplicates if scanned
            if (result.DuplicateKeyCodeUsages.Any())
            {
                Console.WriteLine("Code Usage for Duplicate Keys:");
                foreach (var kvp in result.DuplicateKeyCodeUsages)
                {
                    var usage = kvp.Value;
                    Console.WriteLine($"  Variants: {string.Join(", ", usage.ResourceVariants)}");

                    if (usage.CodeScanned)
                    {
                        foreach (var variant in usage.ResourceVariants)
                        {
                            var refs = usage.CodeReferences.GetValueOrDefault(variant, new List<KeyReference>());
                            if (refs.Any())
                            {
                                var refLocations = refs.Take(3).Select(r => $"{Path.GetFileName(r.FilePath)}:{r.Line}");
                                var moreCount = refs.Count > 3 ? $" (+{refs.Count - 3} more)" : "";
                                Console.WriteLine($"    \"{variant}\" found in: {string.Join(", ", refLocations)}{moreCount}");
                            }
                            else
                            {
                                Console.WriteLine($"    \"{variant}\" not found in code");
                            }
                        }

                        // Add guidance based on usage
                        if (usage.UsedVariants.Count > 1)
                        {
                            Console.WriteLine($"    Warning: Multiple variants used in code! Standardize casing in code first.");
                        }
                        else if (usage.UsedVariants.Count == 1 && usage.UnusedVariants.Any())
                        {
                            Console.WriteLine($"    Tip: Use 'lrm merge-duplicates {usage.UsedVariants.First()}' to keep the used variant.");
                        }
                    }
                }
                Console.WriteLine();
            }
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
            Console.WriteLine();
        }

        // Placeholder mismatches
        if (result.PlaceholderMismatches.Any(kv => kv.Value.Any()))
        {
            Console.WriteLine("Placeholder Mismatches:");
            var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
            foreach (var kvp in result.PlaceholderMismatches.Where(kv => kv.Value.Any()))
            {
                var langDisplay = string.IsNullOrEmpty(kvp.Key) ? defaultCode : kvp.Key;
                Console.WriteLine($"  {langDisplay}:");
                foreach (var error in kvp.Value)
                {
                    Console.WriteLine($"    {error.Key}: {error.Value}");
                }
            }
        }
    }

    private void ScanCodeForDuplicates(
        LocalizationManager.Core.Models.ValidationResult validationResult,
        List<ResourceFile> resourceFiles,
        ValidateCommandSettings settings,
        string resourcePath)
    {
        // Collect all unique duplicate keys across all languages
        var allDuplicateKeys = validationResult.DuplicateKeys
            .SelectMany(kv => kv.Value)
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (!allDuplicateKeys.Any())
            return;

        // Determine source path
        var sourcePath = settings.SourcePath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            // Default to parent directory of resource path
            sourcePath = Directory.GetParent(resourcePath)?.FullName ?? resourcePath;
        }

        if (!Directory.Exists(sourcePath))
            return;

        // Create code scanner
        var codeScanner = new CodeScanner();

        // Scan the code
        var scanResult = codeScanner.Scan(
            sourcePath,
            resourceFiles,
            false); // strictMode

        // For each duplicate key, find all case variants in resource files and their code usage
        foreach (var normalizedKey in allDuplicateKeys)
        {
            var usage = new DuplicateKeyCodeUsage
            {
                NormalizedKey = normalizedKey,
                CodeScanned = true
            };

            // Find all case variants in resource files
            var variants = resourceFiles
                .SelectMany(rf => rf.Entries)
                .Where(e => e.Key.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Key)
                .Distinct()
                .ToList();

            usage.ResourceVariants = variants;

            // Find code references for each variant
            foreach (var variant in variants)
            {
                var references = scanResult.AllKeyUsages
                    .Where(ku => ku.Key == variant) // Exact match for code references
                    .SelectMany(ku => ku.References)
                    .ToList();

                usage.CodeReferences[variant] = references;
            }

            validationResult.DuplicateKeyCodeUsages[normalizedKey] = usage;
        }
    }
}
