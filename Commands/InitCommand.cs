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
    [Description("Resource format: resx, json, android, or ios (default: json)")]
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
                .AddChoices("json", "resx", "android", "ios"));

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
        var validFormats = new[] { "json", "resx", "android", "ios" };
        if (!validFormats.Contains(settings.Format.ToLowerInvariant()))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid format '{settings.Format}'. Must be one of: {string.Join(", ", validFormats)}.");
            return 1;
        }

        // Check if directory exists with resources
        if (Directory.Exists(resourcePath))
        {
            var existingFiles = Directory.GetFiles(resourcePath, "*.json", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase))
                .Concat(Directory.GetFiles(resourcePath, "*.resx", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(resourcePath, "strings.xml", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(resourcePath, "*.strings", SearchOption.AllDirectories))
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

        // Handle Android and iOS separately due to different folder structures
        if (settings.Format == "android")
        {
            return CreateAndroidResources(settings, resourcePath);
        }
        else if (settings.Format == "ios")
        {
            return CreateIosResources(settings, resourcePath);
        }

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

    private int CreateAndroidResources(InitCommandSettings settings, string resourcePath)
    {
        // Android: resourcePath IS the res folder (containing values/ folders)
        // Default language uses "values" folder
        var defaultDir = Path.Combine(resourcePath, "values");
        Directory.CreateDirectory(defaultDir);
        var defaultFilePath = Path.Combine(defaultDir, "strings.xml");

        AnsiConsole.MarkupLine($"[green]Creating[/] values/strings.xml...");

        var xmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
    <!-- Application title -->
    <string name=""app_name"">My Application</string>
    <!-- Welcome message shown to users -->
    <string name=""welcome_message"">Welcome!</string>
</resources>";
        File.WriteAllText(defaultFilePath, xmlContent);

        // Create additional language files
        if (!string.IsNullOrEmpty(settings.Languages))
        {
            foreach (var lang in settings.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = lang.Trim();
                var folder = $"values-{code}";
                var langDir = Path.Combine(resourcePath, folder);
                Directory.CreateDirectory(langDir);
                var langFilePath = Path.Combine(langDir, "strings.xml");

                AnsiConsole.MarkupLine($"[green]Creating[/] {folder}/strings.xml...");

                var langXmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
    <!-- Application title -->
    <string name=""app_name""></string>
    <!-- Welcome message shown to users -->
    <string name=""welcome_message""></string>
</resources>";
                File.WriteAllText(langFilePath, langXmlContent);
            }
        }

        // Create lrm.json config
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = settings.DefaultLanguage,
            ResourceFormat = "android"
        };

        var configPath = Path.Combine(resourcePath, "lrm.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var configJson = JsonSerializer.Serialize(config, jsonOptions);
        File.WriteAllText(configPath, configJson);

        PrintSuccessMessage(resourcePath);
        return 0;
    }

    private int CreateIosResources(InitCommandSettings settings, string resourcePath)
    {
        // iOS uses xx.lproj/Localizable.strings structure

        // Default language uses Base.lproj or en.lproj
        var defaultFolder = settings.DefaultLanguage.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "en.lproj"
            : $"{settings.DefaultLanguage}.lproj";
        var defaultDir = Path.Combine(resourcePath, defaultFolder);
        Directory.CreateDirectory(defaultDir);
        var defaultFilePath = Path.Combine(defaultDir, "Localizable.strings");

        AnsiConsole.MarkupLine($"[green]Creating[/] {defaultFolder}/Localizable.strings...");

        var stringsContent = @"/* Application title */
""app_name"" = ""My Application"";

/* Welcome message shown to users */
""welcome_message"" = ""Welcome!"";
";
        File.WriteAllText(defaultFilePath, stringsContent);

        // Create additional language files
        if (!string.IsNullOrEmpty(settings.Languages))
        {
            foreach (var lang in settings.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = lang.Trim();
                var folder = $"{code}.lproj";
                var langDir = Path.Combine(resourcePath, folder);
                Directory.CreateDirectory(langDir);
                var langFilePath = Path.Combine(langDir, "Localizable.strings");

                AnsiConsole.MarkupLine($"[green]Creating[/] {folder}/Localizable.strings...");

                var langStringsContent = @"/* Application title */
""app_name"" = """";

/* Welcome message shown to users */
""welcome_message"" = """";
";
                File.WriteAllText(langFilePath, langStringsContent);
            }
        }

        // Create lrm.json config
        var config = new ConfigurationModel
        {
            DefaultLanguageCode = settings.DefaultLanguage,
            ResourceFormat = "ios"
        };

        var configPath = Path.Combine(resourcePath, "lrm.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var configJson = JsonSerializer.Serialize(config, jsonOptions);
        File.WriteAllText(configPath, configJson);

        PrintSuccessMessage(resourcePath);
        return 0;
    }

    private void PrintSuccessMessage(string resourcePath)
    {
        AnsiConsole.MarkupLine($"[green]Created[/] lrm.json configuration");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Initialization complete![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Next steps:[/]");
        AnsiConsole.MarkupLine($"  [grey]1.[/] cd {resourcePath}");
        AnsiConsole.MarkupLine($"  [grey]2.[/] lrm edit     [grey]# Interactive editor[/]");
        AnsiConsole.MarkupLine($"  [grey]3.[/] lrm add      [grey]# Add new keys[/]");
        AnsiConsole.MarkupLine($"  [grey]4.[/] lrm validate [grey]# Check for issues[/]");
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
