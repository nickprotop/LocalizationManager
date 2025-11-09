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

using LocalizationManager.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("lrm");
    config.SetApplicationVersion("0.6.0");

    // Propagate exceptions so we can handle them intelligently
    config.PropagateExceptions();

    // Validate presence of commands (Spectre.Console.Cli default behavior is to show help on errors)
    config.ValidateExamples();

    // Add helpful description
    config.AddExample(new[] { "validate", "--help" });
    config.AddExample(new[] { "view", "SaveButton", "--format", "json" });
    config.AddExample(new[] { "add", "NewKey", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" });

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate resource files for missing keys, duplicates, and empty values")
        .WithExample(new[] { "validate" })
        .WithExample(new[] { "validate", "--path", "../Resources" })
        .WithExample(new[] { "validate", "--format", "json" })
        .WithExample(new[] { "validate", "--format", "simple" });

    config.AddCommand<StatsCommand>("stats")
        .WithDescription("Display statistics about resource files and translation coverage")
        .WithExample(new[] { "stats" })
        .WithExample(new[] { "stats", "--path", "./Resources" })
        .WithExample(new[] { "stats", "--format", "json" })
        .WithExample(new[] { "stats", "--format", "simple" });

    config.AddCommand<ViewCommand>("view")
        .WithDescription("View details of a specific localization key")
        .WithExample(new[] { "view", "SaveButton" })
        .WithExample(new[] { "view", "SaveButton", "--show-comments" })
        .WithExample(new[] { "view", "SaveButton", "--format", "json" })
        .WithExample(new[] { "view", "SaveButton", "--format", "simple" });

    config.AddCommand<AddCommand>("add")
        .WithDescription("Add a new localization key to all language files")
        .WithExample(new[] { "add", "NewKey", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" })
        .WithExample(new[] { "add", "SaveButton", "-i" })
        .WithExample(new[] { "add", "SaveButton", "--lang", "default:\"Save\"", "--comment", "Button label for save action" })
        .WithExample(new[] { "add", "NewKey", "-l", "default:\"English value\"", "--no-backup" });

    config.AddCommand<UpdateCommand>("update")
        .WithDescription("Update values for an existing localization key")
        .WithExample(new[] { "update", "SaveButton", "--lang", "default:\"Save Changes\"", "--lang", "el:\"Αποθήκευση Αλλαγών\"" })
        .WithExample(new[] { "update", "SaveButton", "-i" })
        .WithExample(new[] { "update", "SaveButton", "--comment", "Updated comment" })
        .WithExample(new[] { "update", "SaveButton", "-l", "default:\"New value\"", "-y", "--no-backup" });

    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete a localization key from all language files")
        .WithExample(new[] { "delete", "OldKey" })
        .WithExample(new[] { "delete", "OldKey", "-y" })
        .WithExample(new[] { "delete", "OldKey", "-y", "--no-backup" });

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Export resource files to various formats (CSV, JSON, or simple text)")
        .WithExample(new[] { "export" })
        .WithExample(new[] { "export", "-o", "translations.csv" })
        .WithExample(new[] { "export", "--format", "json", "-o", "translations.json" })
        .WithExample(new[] { "export", "--format", "simple", "--include-status" });

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import translations from CSV format")
        .WithExample(new[] { "import", "translations.csv" })
        .WithExample(new[] { "import", "translations.csv", "--overwrite" })
        .WithExample(new[] { "import", "translations.csv", "--no-backup" });

    config.AddCommand<EditCommand>("edit")
        .WithDescription("Launch interactive TUI editor for resource files")
        .WithExample(new[] { "edit" })
        .WithExample(new[] { "edit", "--path", "../Resources" })
        .WithExample(new[] { "edit", "-p", "./Resources" });

    config.AddCommand<AddLanguageCommand>("add-language")
        .WithDescription("Create a new language resource file")
        .WithExample(new[] { "add-language", "--culture", "fr" })
        .WithExample(new[] { "add-language", "-c", "fr-CA", "--copy-from", "fr" })
        .WithExample(new[] { "add-language", "-c", "de", "--empty" });

    config.AddCommand<RemoveLanguageCommand>("remove-language")
        .WithDescription("Delete a language resource file")
        .WithExample(new[] { "remove-language", "--culture", "fr" })
        .WithExample(new[] { "remove-language", "-c", "fr", "-y" });

    config.AddCommand<ListLanguagesCommand>("list-languages")
        .WithDescription("List all available language files")
        .WithExample(new[] { "list-languages" })
        .WithExample(new[] { "list-languages", "--format", "json" });
});

int result;

try
{
    result = app.Run(args);
}
catch (CommandParseException ex)
{
    // Parse error - could be unknown command or argument error
    AnsiConsole.MarkupLine($"[red]{ex.Message.Replace("[", "[[").Replace("]", "]]")}[/]");
    AnsiConsole.WriteLine();

    // Check if this is a command-not-found error vs argument error
    // If the error mentions "Unknown command", show main help
    // Otherwise, show command-specific help if we can detect the command name
    if (ex.Message.Contains("Unknown command") || ex.Message.Contains("No such command"))
    {
        AnsiConsole.MarkupLine("[dim]Run 'lrm --help' to see available commands.[/]");
    }
    else
    {
        // Try to extract command name from args to show command-specific help
        var commandName = args.FirstOrDefault(a => !a.StartsWith("-"));
        if (!string.IsNullOrEmpty(commandName))
        {
            AnsiConsole.MarkupLine($"[dim]Run 'lrm {commandName} --help' for command-specific usage information.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Run 'lrm --help' for usage information.[/]");
        }
    }

    Console.WriteLine();
    return 1;
}
catch (Exception ex)
{
    // Other exceptions
    AnsiConsole.MarkupLine($"[red]Error: {ex.Message.Replace("[", "[[").Replace("]", "]]")}[/]");
    AnsiConsole.WriteLine();
    Console.WriteLine();
    return 1;
}

// Show help hint on error (common CLI practice - like git, docker, etc.)
if (result != 0)
{
    AnsiConsole.WriteLine();

    // Try to detect which command was run for contextual help
    var commandName = args.FirstOrDefault(a => !a.StartsWith("-"));
    if (!string.IsNullOrEmpty(commandName))
    {
        AnsiConsole.MarkupLine($"[dim]Run 'lrm {commandName} --help' for more information.[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[dim]Run 'lrm --help' for usage information.[/]");
    }
}

// Add newline after output (better UX - prompt not right after output)
Console.WriteLine();

return result;
