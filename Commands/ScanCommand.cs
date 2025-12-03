// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using LocalizationManager.Core;
using LocalizationManager.Core.Enums;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Output;
using LocalizationManager.Core.Scanning;
using LocalizationManager.Core.Scanning.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to scan source code for localization key references
/// </summary>
public class ScanCommand : Command<ScanCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandOption("--source-path <PATH>")]
        [Description("Path to source code directory to scan. Defaults to parent directory of resource path.")]
        public string? SourcePath { get; set; }

        [CommandOption("--exclude <PATTERNS>")]
        [Description("Glob patterns to exclude (comma-separated). Example: **/*.g.cs,**/bin/**")]
        public string? ExcludePatterns { get; set; }

        [CommandOption("--strict")]
        [Description("Strict mode - only detect high-confidence static references, ignore dynamic patterns")]
        public bool StrictMode { get; set; }

        [CommandOption("--show-unused")]
        [Description("Show only unused keys (in .resx but not in code)")]
        public bool ShowUnusedOnly { get; set; }

        [CommandOption("--show-missing")]
        [Description("Show only missing keys (in code but not in .resx)")]
        public bool ShowMissingOnly { get; set; }

        [CommandOption("--show-references")]
        [Description("Show detailed reference information for each key")]
        public bool ShowReferences { get; set; }

        [CommandOption("--resource-classes <NAMES>")]
        [Description("Resource class names to detect (comma-separated). Default: Resources,Strings,AppResources")]
        public string? ResourceClassNames { get; set; }

        [CommandOption("--localization-methods <NAMES>")]
        [Description("Localization method names to detect (comma-separated). Default: GetString,GetLocalizedString,Translate,L,T")]
        public string? LocalizationMethods { get; set; }

        [CommandOption("--file <PATH>")]
        [Description("Scan a single file instead of the entire codebase")]
        public string? FilePath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();
        var format = settings.GetOutputFormat();
        var isTableFormat = format == OutputFormat.Table;

        // Determine source path - convert to absolute path first to handle relative paths correctly
        // Trim trailing slashes to ensure Directory.GetParent works correctly
        var absoluteResourcePath = Path.GetFullPath(resourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string sourcePath;
        if (settings.SourcePath != null)
        {
            sourcePath = Path.GetFullPath(settings.SourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else
        {
            var parent = Directory.GetParent(absoluteResourcePath);
            sourcePath = parent?.FullName ?? absoluteResourcePath;
        }

        if (!Directory.Exists(sourcePath))
        {
            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[red]✗ Source path not found:[/] {sourcePath}");
            }
            else
            {
                Console.Error.WriteLine($"Source path not found: {sourcePath}");
            }
            return 1;
        }

        if (isTableFormat)
        {
            AnsiConsole.MarkupLine($"[blue]Scanning source:[/] {sourcePath}");
            AnsiConsole.MarkupLine($"[blue]Resource path:[/] {resourcePath}");
            if (settings.StrictMode)
            {
                AnsiConsole.MarkupLine("[yellow]Mode:[/] Strict (high-confidence only)");
            }
            AnsiConsole.WriteLine();
        }

        try
        {
            // Discover and parse resource files
            var languages = settings.DiscoverLanguages();
            var backendName = settings.GetBackendName();

            if (!languages.Any())
            {
                if (isTableFormat)
                {
                    AnsiConsole.MarkupLine($"[red]✗ No {backendName.ToUpper()} files found![/]");
                }
                else
                {
                    Console.Error.WriteLine($"No {backendName.ToUpper()} files found!");
                }
                return 1;
            }

            var resourceFiles = new List<ResourceFile>();

            foreach (var lang in languages)
            {
                try
                {
                    var resourceFile = settings.ReadResourceFile(lang);
                    resourceFiles.Add(resourceFile);
                }
                catch (Exception ex)
                {
                    if (isTableFormat)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Error parsing {lang.Name}: {ex.Message}[/]");
                    }
                    return 1;
                }
            }

            // Parse exclude patterns
            var excludePatterns = settings.ExcludePatterns?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // Resolve scanner configuration (priority: CLI → config file → defaults)
            var resourceClassNames = ScannerConfiguration.GetResourceClassNames(
                settings.ResourceClassNames,
                settings.LoadedConfiguration);

            var localizationMethods = ScannerConfiguration.GetLocalizationMethods(
                settings.LocalizationMethods,
                settings.LoadedConfiguration);

            // Scan code
            var scanner = new CodeScanner();

            // Check if scanning a single file
            if (settings.FilePath != null)
            {
                return ExecuteSingleFileScan(scanner, settings, resourceFiles, resourceClassNames, localizationMethods, format, isTableFormat);
            }

            // Full codebase scan
            ScanResult result;

            if (isTableFormat)
            {
                result = AnsiConsole.Status()
                    .Start("Scanning source files...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        return scanner.Scan(sourcePath, resourceFiles, settings.StrictMode, excludePatterns,
                            resourceClassNames, localizationMethods);
                    });
            }
            else
            {
                result = scanner.Scan(sourcePath, resourceFiles, settings.StrictMode, excludePatterns,
                    resourceClassNames, localizationMethods);
            }

            // Display results based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(result, settings);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(result, settings);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(result, settings);
                    break;
            }

            // Return exit code
            return result.HasIssues ? 1 : 0;
        }
        catch (Exception ex)
        {
            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            return 1;
        }
    }

    private int ExecuteSingleFileScan(
        CodeScanner scanner,
        Settings settings,
        List<ResourceFile> resourceFiles,
        List<string>? resourceClassNames,
        List<string>? localizationMethods,
        OutputFormat format,
        bool isTableFormat)
    {
        var filePath = Path.GetFullPath(settings.FilePath!);

        if (!File.Exists(filePath))
        {
            if (isTableFormat)
            {
                AnsiConsole.MarkupLine($"[red]✗ File not found:[/] {filePath}");
            }
            else
            {
                Console.Error.WriteLine($"File not found: {filePath}");
            }
            return 1;
        }

        if (isTableFormat)
        {
            AnsiConsole.MarkupLine($"[blue]Scanning file:[/] {filePath}");
            AnsiConsole.WriteLine();
        }

        // Scan the single file
        ScanResult result;

        if (isTableFormat)
        {
            result = AnsiConsole.Status()
                .Start("Scanning file...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return scanner.ScanSingleFile(filePath, resourceFiles, settings.StrictMode,
                        resourceClassNames, localizationMethods);
                });
        }
        else
        {
            result = scanner.ScanSingleFile(filePath, resourceFiles, settings.StrictMode,
                resourceClassNames, localizationMethods);
        }

        // Display results using existing display methods
        switch (format)
        {
            case OutputFormat.Json:
                DisplayJson(result, settings);
                break;
            case OutputFormat.Simple:
                DisplaySimple(result, settings);
                break;
            case OutputFormat.Table:
            default:
                DisplayTable(result, settings);
                break;
        }

        return result.HasIssues ? 1 : 0;
    }

    private void DisplayTable(ScanResult result, Settings settings)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Scanned {result.FilesScanned} files[/]");
        AnsiConsole.MarkupLine($"[blue]Found {result.TotalReferences} key references ({result.UniqueKeysFound} unique keys)[/]");

        if (result.WarningCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {result.WarningCount} low-confidence references (dynamic keys)[/]");
        }

        AnsiConsole.WriteLine();

        // Show missing keys
        if (!settings.ShowUnusedOnly && result.MissingKeys.Any())
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[red]Missing Keys[/] (in code, not in .resx)")
                .AddColumn("Key")
                .AddColumn("References")
                .AddColumn("Locations");

            foreach (var key in result.MissingKeys.OrderBy(k => k.Key))
            {
                // Group references by file and show line numbers
                var fileGroups = key.References
                    .GroupBy(r => r.FilePath)
                    .Take(3)
                    .Select(g =>
                    {
                        var fileName = Path.GetFileName(g.Key);
                        var lines = string.Join(",", g.Select(r => r.Line).Distinct().OrderBy(l => l).Take(3));
                        if (g.Count() > 3)
                            lines += "...";
                        return $"{fileName}:{lines}";
                    });

                var locations = string.Join(", ", fileGroups);

                if (key.References.Select(r => r.FilePath).Distinct().Count() > 3)
                {
                    locations += ", ...";
                }

                table.AddRow(
                    $"[yellow]{key.Key.EscapeMarkup()}[/]",
                    key.ReferenceCount.ToString(),
                    $"[dim]{locations.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Show unused keys
        if (!settings.ShowMissingOnly && result.UnusedKeys.Any())
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[yellow]Unused Keys[/] (in .resx, not in code)")
                .AddColumn("Key")
                .AddColumn("Count");

            var groupedUnused = result.UnusedKeys.Take(50).ToList();

            foreach (var key in groupedUnused)
            {
                table.AddRow($"[dim]{key.EscapeMarkup()}[/]", "-");
            }

            if (result.UnusedKeys.Count > 50)
            {
                table.AddRow($"[dim]... and {result.UnusedKeys.Count - 50} more[/]", "");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Show references if requested
        if (settings.ShowReferences && !settings.ShowUnusedOnly && !settings.ShowMissingOnly)
        {
            var usedKeys = result.AllKeyUsages.Where(k => k.ExistsInResources).OrderByDescending(k => k.ReferenceCount).Take(20);

            if (usedKeys.Any())
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[green]Key Usage[/] (top 20 by references)")
                    .AddColumn("Key")
                    .AddColumn("Refs")
                    .AddColumn("Used In");

                foreach (var key in usedKeys)
                {
                    var files = string.Join(", ", key.References
                        .Select(r => Path.GetFileName(r.FilePath))
                        .Distinct()
                        .Take(3));

                    if (key.References.Select(r => r.FilePath).Distinct().Count() > 3)
                    {
                        files += ", ...";
                    }

                    table.AddRow(
                        key.Key.EscapeMarkup(),
                        key.ReferenceCount.ToString(),
                        $"[dim]{files.EscapeMarkup()}[/]"
                    );
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
        }

        // Summary
        if (!settings.ShowMissingOnly && !settings.ShowUnusedOnly)
        {
            if (result.HasIssues)
            {
                AnsiConsole.MarkupLine($"[red]✗ Found {result.MissingKeys.Count} missing keys and {result.UnusedKeys.Count} unused keys[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✓ No issues found![/]");
            }
        }
    }

    private void DisplayJson(ScanResult result, Settings settings)
    {
        var output = new
        {
            summary = new
            {
                filesScanned = result.FilesScanned,
                totalReferences = result.TotalReferences,
                uniqueKeys = result.UniqueKeysFound,
                missingKeys = result.MissingKeys.Count,
                unusedKeys = result.UnusedKeys.Count,
                warnings = result.WarningCount,
                hasIssues = result.HasIssues
            },
            missingKeys = settings.ShowUnusedOnly ? null : result.MissingKeys.Select(k => new
            {
                key = k.Key,
                referenceCount = k.ReferenceCount,
                references = k.References.Select(r => new
                {
                    file = r.FilePath,
                    line = r.Line,
                    pattern = r.Pattern,
                    confidence = r.Confidence.ToString(),
                    warning = r.Warning
                })
            }).ToList(),
            unusedKeys = settings.ShowMissingOnly ? null : result.UnusedKeys,
            keyUsage = settings.ShowReferences && !settings.ShowMissingOnly && !settings.ShowUnusedOnly
                ? result.AllKeyUsages.Where(k => k.ExistsInResources).Select(k => new
                {
                    key = k.Key,
                    referenceCount = k.ReferenceCount,
                    references = k.References.Select(r => new
                    {
                        file = r.FilePath,
                        line = r.Line
                    })
                }).ToList()
                : null
        };

        var json = OutputFormatter.FormatJson(output);
        Console.WriteLine(json);
    }

    private void DisplaySimple(ScanResult result, Settings settings)
    {
        Console.WriteLine($"Scanned: {result.FilesScanned} files");
        Console.WriteLine($"Found: {result.TotalReferences} references ({result.UniqueKeysFound} unique keys)");

        if (result.WarningCount > 0)
        {
            Console.WriteLine($"Warnings: {result.WarningCount}");
        }

        Console.WriteLine();

        if (!settings.ShowUnusedOnly && result.MissingKeys.Any())
        {
            Console.WriteLine($"Missing Keys ({result.MissingKeys.Count}):");
            foreach (var key in result.MissingKeys.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  - {key.Key} ({key.ReferenceCount} references)");
                // Show first few locations
                var locations = key.References
                    .GroupBy(r => r.FilePath)
                    .Take(3)
                    .Select(g =>
                    {
                        var fileName = Path.GetFileName(g.Key);
                        var lines = string.Join(",", g.Select(r => r.Line).Distinct().OrderBy(l => l).Take(3));
                        return $"      {fileName}:{lines}";
                    });
                foreach (var location in locations)
                {
                    Console.WriteLine(location);
                }
            }
            Console.WriteLine();
        }

        if (!settings.ShowMissingOnly && result.UnusedKeys.Any())
        {
            Console.WriteLine($"Unused Keys ({result.UnusedKeys.Count}):");
            foreach (var key in result.UnusedKeys.Take(50))
            {
                Console.WriteLine($"  - {key}");
            }
            if (result.UnusedKeys.Count > 50)
            {
                Console.WriteLine($"  ... and {result.UnusedKeys.Count - 50} more");
            }
            Console.WriteLine();
        }

        if (result.HasIssues)
        {
            Console.WriteLine($"Issues: {result.MissingKeys.Count} missing, {result.UnusedKeys.Count} unused");
        }
        else
        {
            Console.WriteLine("No issues found!");
        }
    }
}
