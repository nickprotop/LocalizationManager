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
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to add a new localization key to all language files.
/// </summary>
public class AddCommandSettings : BaseCommandSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("The resource key to add")]
    public required string Key { get; set; }

    [CommandOption("-l|--lang <LANGVALUE>")]
    [Description("Language value in format 'code:\"value\"' (e.g., --lang default:\"Save\" --lang el:\"Αποθήκευση\"). Use 'default' for default language. Can be used multiple times.")]
    public string[]? LanguageValues { get; set; }

    [CommandOption("-i|--interactive")]
    [Description("Interactive mode - prompts for all language values even if some are provided")]
    public bool Interactive { get; set; }

    [CommandOption("--comment <COMMENT>")]
    [Description("Optional comment for the resource entry")]
    public string? Comment { get; set; }

    [CommandOption("--no-backup")]
    [Description("Skip creating backups before modifying files")]
    public bool NoBackup { get; set; }
}

public class AddCommand : Command<AddCommandSettings>
{
    public override int Execute(CommandContext context, AddCommandSettings settings, CancellationToken cancellationToken = default)
    {
        var resourcePath = settings.GetResourcePath();

        AnsiConsole.MarkupLine($"[blue]Scanning:[/] {resourcePath}");
        AnsiConsole.WriteLine();

        try
        {
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
            var resourceFiles = new List<ResourceFile>();

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

            // Check if key already exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key == settings.Key))
            {
                AnsiConsole.MarkupLine($"[red]✗ Key '{settings.Key.EscapeMarkup()}' already exists![/]");
                return 1;
            }

            // Collect values for each language
            var values = new Dictionary<string, string?>();

            // Parse --lang arguments (unless in pure interactive mode)
            if (!settings.Interactive && settings.LanguageValues != null && settings.LanguageValues.Any())
            {
                foreach (var langValue in settings.LanguageValues)
                {
                    var parts = langValue.Split(':', 2);
                    if (parts.Length != 2)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Invalid format: '{langValue}'. Expected format: 'code:value' (e.g., en:Save)[/]");
                        return 1;
                    }

                    var code = parts[0].Trim();
                    var value = parts[1];

                    // Normalize "default" alias to empty string
                    if (code.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        code = "";
                    }

                    // Validate language code exists
                    var matchingLang = languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                    if (matchingLang == null)
                    {
                        var availableCodes = string.Join(", ", languages.Select(l => l.Code));
                        AnsiConsole.MarkupLine($"[red]✗ Unknown language code: '{code}'[/]");
                        AnsiConsole.MarkupLine($"[yellow]Available languages: {availableCodes}[/]");
                        return 1;
                    }

                    values[matchingLang.Code] = value;
                }
            }

            // Interactive mode or prompt for missing values
            if (settings.Interactive)
            {
                // Interactive mode: prompt for all languages
                AnsiConsole.MarkupLine("[dim]Interactive mode: Enter values for all languages[/]");
                AnsiConsole.WriteLine();

                foreach (var lang in languages)
                {
                    var prompt = $"[yellow]{lang.Name}:[/]";
                    var value = AnsiConsole.Ask<string>(prompt);
                    values[lang.Code] = value;
                }
            }
            else
            {
                // Non-interactive: prompt only for missing values
                foreach (var lang in languages)
                {
                    if (!values.ContainsKey(lang.Code))
                    {
                        var value = AnsiConsole.Ask<string>($"[yellow]Enter value for {lang.Name}:[/]");
                        values[lang.Code] = value;
                    }
                }
            }

            // Create backups
            if (!settings.NoBackup)
            {
                AnsiConsole.MarkupLine("[dim]Creating backups...[/]");
                var backupManager = new BackupVersionManager(10);
                var filePaths = resourceFiles.Select(rf => rf.Language.FilePath).ToList();

                try
                {
                    foreach (var filePath in filePaths)
                    {
                        backupManager.CreateBackupAsync(filePath, "add-key", resourcePath)
                            .GetAwaiter().GetResult();
                    }
                    AnsiConsole.MarkupLine("[dim green]✓ Backups created[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ Backup failed: {ex.Message}[/]");
                    if (!AnsiConsole.Confirm("Continue without backup?"))
                    {
                        return 1;
                    }
                }
            }

            // Add the key to all resource files
            foreach (var resourceFile in resourceFiles)
            {
                var value = values.ContainsKey(resourceFile.Language.Code)
                    ? values[resourceFile.Language.Code]
                    : string.Empty;

                resourceFile.Entries.Add(new ResourceEntry
                {
                    Key = settings.Key,
                    Value = value,
                    Comment = settings.Comment
                });

                // Save the file
                try
                {
                    parser.Write(resourceFile);
                    AnsiConsole.MarkupLine($"[green]✓ Added to {resourceFile.Language.Name}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to write {resourceFile.Language.Name}: {ex.Message}[/]");
                    return 1;
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green bold]✓ Successfully added key '{settings.Key.EscapeMarkup()}' to {resourceFiles.Count} file(s)[/]");

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
}
