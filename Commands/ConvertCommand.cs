// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the convert command.
/// </summary>
public class ConvertCommandSettings : BaseFormattableCommandSettings
{
    [CommandOption("--from <FORMAT>")]
    [Description("Source format: resx or json (auto-detected if not specified)")]
    public string? SourceFormat { get; set; }

    [CommandOption("--to <FORMAT>")]
    [Description("Target format: resx or json")]
    public string TargetFormat { get; set; } = "json";

    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory (default: same as source)")]
    public string? OutputPath { get; set; }

    [CommandOption("--nested")]
    [Description("Convert dot-separated keys to nested structure (JSON only)")]
    public bool Nested { get; set; }

    [CommandOption("--include-comments")]
    [Description("Preserve comments in output (default: true)")]
    public bool IncludeComments { get; set; } = true;

    [CommandOption("--no-backup")]
    [Description("Skip backup creation")]
    public bool NoBackup { get; set; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts")]
    public bool SkipConfirmation { get; set; }
}

/// <summary>
/// Command to convert resource files between formats.
/// </summary>
public class ConvertCommand : Command<ConvertCommandSettings>
{
    public override int Execute(CommandContext context, ConvertCommandSettings settings, CancellationToken cancellationToken = default)
    {
        settings.LoadConfiguration();

        try
        {
            var sourcePath = settings.GetResourcePath();
            var outputPath = settings.OutputPath ?? sourcePath;

            // Validate target format
            if (settings.TargetFormat != "json" && settings.TargetFormat != "resx")
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid target format '{settings.TargetFormat}'. Must be 'json' or 'resx'.");
                return 1;
            }

            // Determine source format
            var sourceFormat = settings.SourceFormat;
            if (string.IsNullOrEmpty(sourceFormat))
            {
                // Auto-detect
                var factory = new ResourceBackendFactory();
                var detectedBackend = factory.ResolveFromPath(sourcePath);
                sourceFormat = detectedBackend.Name;
                AnsiConsole.MarkupLine($"[grey]Auto-detected source format: {sourceFormat}[/]");
            }

            // Validate source format
            if (sourceFormat != "json" && sourceFormat != "resx")
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid source format '{sourceFormat}'. Must be 'json' or 'resx'.");
                return 1;
            }

            // Check if conversion is needed
            if (sourceFormat == settings.TargetFormat)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Source and target formats are the same ({sourceFormat}).");
                if (!settings.SkipConfirmation && !AnsiConsole.Confirm("Continue anyway?", false))
                {
                    return 0;
                }
            }

            // Get source backend
            var sourceBackend = sourceFormat == "json"
                ? (Core.Abstractions.IResourceBackend)new JsonResourceBackend()
                : new ResxResourceBackend();

            // Discover source files
            var languages = sourceBackend.Discovery.DiscoverLanguages(sourcePath);

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]No {sourceFormat} files found in[/] {sourcePath}");
                return 0;
            }

            AnsiConsole.MarkupLine($"Found [green]{languages.Count}[/] {sourceFormat} file(s) to convert.");
            AnsiConsole.WriteLine();

            // Confirm
            if (!settings.SkipConfirmation)
            {
                var table = new Table();
                table.AddColumn("Source File");
                table.AddColumn("Target File");

                foreach (var lang in languages)
                {
                    var targetFileName = GetTargetFileName(lang, settings.TargetFormat);
                    table.AddRow(
                        Path.GetFileName(lang.FilePath),
                        targetFileName);
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();

                if (!AnsiConsole.Confirm($"Convert {languages.Count} file(s) to {settings.TargetFormat}?"))
                {
                    AnsiConsole.MarkupLine("[grey]Conversion cancelled.[/]");
                    return 0;
                }
            }

            // Create output directory
            Directory.CreateDirectory(outputPath);

            // Configure target backend
            var targetBackend = settings.TargetFormat == "json"
                ? (Core.Abstractions.IResourceBackend)new JsonResourceBackend(new Core.Configuration.JsonFormatConfiguration
                {
                    UseNestedKeys = settings.Nested,
                    PreserveComments = settings.IncludeComments,
                    IncludeMeta = true
                })
                : new ResxResourceBackend();

            // Convert each file
            var successCount = 0;
            var failCount = 0;

            foreach (var lang in languages)
            {
                try
                {
                    var targetFileName = GetTargetFileName(lang, settings.TargetFormat);
                    var targetFilePath = Path.Combine(outputPath, targetFileName);

                    AnsiConsole.MarkupLine($"[yellow]Converting[/] {Path.GetFileName(lang.FilePath)}...");

                    // Read source file
                    var resourceFile = sourceBackend.Reader.Read(lang);

                    // Update language info with new path and extension
                    resourceFile.Language = new LanguageInfo
                    {
                        BaseName = lang.BaseName,
                        Code = lang.Code,
                        Name = lang.Name,
                        IsDefault = lang.IsDefault,
                        FilePath = targetFilePath
                    };

                    // Write target file
                    targetBackend.Writer.Write(resourceFile);

                    AnsiConsole.MarkupLine($"  [green]->[/] {targetFileName} ({resourceFile.Entries.Count} entries)");
                    successCount++;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]Error:[/] {ex.Message}");
                    failCount++;
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Converted:[/] {successCount} file(s)");
            if (failCount > 0)
            {
                AnsiConsole.MarkupLine($"[red]Failed:[/] {failCount} file(s)");
            }

            return failCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string GetTargetFileName(LanguageInfo lang, string targetFormat)
    {
        var extension = targetFormat == "json" ? ".json" : ".resx";
        return lang.IsDefault || string.IsNullOrEmpty(lang.Code)
            ? $"{lang.BaseName}{extension}"
            : $"{lang.BaseName}.{lang.Code}{extension}";
    }
}
