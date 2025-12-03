// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Commands;

/// <summary>
/// Settings for the init command.
/// </summary>
public class InitCommandSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Path to create resource files (default: current directory)")]
    public string? ResourcePath { get; set; }

    [CommandOption("-i|--interactive")]
    [Description("Run interactive setup wizard")]
    public bool Interactive { get; set; }

    [CommandOption("--format <FORMAT>")]
    [Description("Resource format: resx or json (default: json)")]
    public string Format { get; set; } = "json";

    [CommandOption("--default-lang <CODE>")]
    [Description("Default language code (e.g., en, en-US)")]
    public string DefaultLanguage { get; set; } = "en";

    [CommandOption("--languages <CODES>")]
    [Description("Additional language codes, comma-separated (e.g., fr,de,el)")]
    public string? Languages { get; set; }

    [CommandOption("--base-name <NAME>")]
    [Description("Base filename for resources (default: strings)")]
    public string BaseName { get; set; } = "strings";

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts")]
    public bool SkipConfirmation { get; set; }

    public string GetResourcePath() => ResourcePath ?? Directory.GetCurrentDirectory();
}

/// <summary>
/// Command to initialize a new localization project.
/// </summary>
public class InitCommand : Command<InitCommandSettings>
{
    public override int Execute(CommandContext context, InitCommandSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var resourcePath = settings.GetResourcePath();

            if (settings.Interactive)
            {
                return RunWizard(settings, resourcePath);
            }

            return CreateResources(settings, resourcePath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private int RunWizard(InitCommandSettings settings, string resourcePath)
    {
        AnsiConsole.Write(new FigletText("LRM Init").Color(Color.Blue));
        AnsiConsole.WriteLine();

        // 1. Select format
        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]resource format[/]:")
                .AddChoices("json", "resx"));

        // 2. Resource path
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]resource path[/]:")
                .DefaultValue(resourcePath)
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(path))
        {
            path = resourcePath;
        }

        // 3. Default language
        var defaultLang = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]default language code[/]:")
                .DefaultValue("en"));

        // 4. Base name
        var baseName = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]base filename[/]:")
                .DefaultValue("strings"));

        // 5. Additional languages
        var additionalLangs = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]additional languages[/] (comma-separated, or empty):")
                .AllowEmpty());

        // Update settings
        settings.Format = format;
        settings.ResourcePath = path;
        settings.DefaultLanguage = defaultLang;
        settings.Languages = additionalLangs;
        settings.BaseName = baseName;

        // Confirm
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Configuration:[/]");
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Format", format);
        table.AddRow("Path", path);
        table.AddRow("Default Language", defaultLang);
        table.AddRow("Base Name", baseName);
        table.AddRow("Additional Languages", string.IsNullOrEmpty(additionalLangs) ? "(none)" : additionalLangs);
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Proceed with initialization?"))
        {
            AnsiConsole.MarkupLine("[grey]Initialization cancelled.[/]");
            return 0;
        }

        return CreateResources(settings, path);
    }

    private int CreateResources(InitCommandSettings settings, string resourcePath)
    {
        // Validate format
        if (settings.Format != "json" && settings.Format != "resx")
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid format '{settings.Format}'. Must be 'json' or 'resx'.");
            return 1;
        }

        // Check if directory exists with resources
        if (Directory.Exists(resourcePath))
        {
            var existingFiles = Directory.GetFiles(resourcePath, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
                .Concat(Directory.GetFiles(resourcePath, "*.resx", SearchOption.TopDirectoryOnly))
                .ToList();

            if (existingFiles.Any() && !settings.SkipConfirmation)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Directory already contains {existingFiles.Count} resource file(s).");
                if (!AnsiConsole.Confirm("Continue anyway?", false))
                {
                    AnsiConsole.MarkupLine("[grey]Initialization cancelled.[/]");
                    return 0;
                }
            }
        }

        // Create directory
        Directory.CreateDirectory(resourcePath);

        var factory = new ResourceBackendFactory();
        var backend = factory.GetBackend(settings.Format);
        var extension = settings.Format == "json" ? ".json" : ".resx";

        // Create default language file
        var defaultFileName = $"{settings.BaseName}{extension}";
        AnsiConsole.MarkupLine($"[green]Creating[/] {defaultFileName}...");

        var defaultFilePath = Path.Combine(resourcePath, defaultFileName);
        var defaultFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = settings.BaseName,
                Code = "",
                Name = GetCultureDisplayName(settings.DefaultLanguage),
                IsDefault = true,
                FilePath = defaultFilePath
            },
            Entries = new List<ResourceEntry>
            {
                new() { Key = "AppTitle", Value = "My Application", Comment = "Application title" },
                new() { Key = "WelcomeMessage", Value = "Welcome!", Comment = "Welcome message shown to users" }
            }
        };
        backend.Writer.Write(defaultFile);

        // Create additional language files
        if (!string.IsNullOrEmpty(settings.Languages))
        {
            foreach (var lang in settings.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = lang.Trim();
                var langFileName = $"{settings.BaseName}.{code}{extension}";
                AnsiConsole.MarkupLine($"[green]Creating[/] {langFileName}...");

                var langFilePath = Path.Combine(resourcePath, langFileName);
                var langFile = new ResourceFile
                {
                    Language = new LanguageInfo
                    {
                        BaseName = settings.BaseName,
                        Code = code,
                        Name = GetCultureDisplayName(code),
                        IsDefault = false,
                        FilePath = langFilePath
                    },
                    Entries = new List<ResourceEntry>
                    {
                        new() { Key = "AppTitle", Value = "", Comment = "Application title" },
                        new() { Key = "WelcomeMessage", Value = "", Comment = "Welcome message shown to users" }
                    }
                };
                backend.Writer.Write(langFile);
            }
        }

        // Create lrm.json config
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = settings.DefaultLanguage,
            ResourceFormat = settings.Format,
            Json = settings.Format == "json" ? new JsonFormatConfiguration
            {
                BaseName = settings.BaseName,
                UseNestedKeys = false,
                IncludeMeta = true,
                PreserveComments = true
            } : null
        };

        var configPath = Path.Combine(resourcePath, "lrm.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var configJson = JsonSerializer.Serialize(config, jsonOptions);
        File.WriteAllText(configPath, configJson);

        AnsiConsole.MarkupLine($"[green]Created[/] lrm.json configuration");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Initialization complete![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Next steps:[/]");
        AnsiConsole.MarkupLine($"  [grey]1.[/] cd {resourcePath}");
        AnsiConsole.MarkupLine($"  [grey]2.[/] lrm edit     [grey]# Interactive editor[/]");
        AnsiConsole.MarkupLine($"  [grey]3.[/] lrm add      [grey]# Add new keys[/]");
        AnsiConsole.MarkupLine($"  [grey]4.[/] lrm validate [grey]# Check for issues[/]");

        return 0;
    }

    private static string GetCultureDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "Default";
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(code).DisplayName;
        }
        catch
        {
            return code;
        }
    }
}
