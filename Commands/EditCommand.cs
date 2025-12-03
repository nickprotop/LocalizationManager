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

using System.ComponentModel;
using LocalizationManager.Core;
using LocalizationManager.Core.Models;
using LocalizationManager.UI;
using Spectre.Console;
using Spectre.Console.Cli;
using Terminal.Gui;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to launch the interactive TUI editor.
/// </summary>
public class EditCommand : Command<EditCommand.Settings>
{
    public class Settings : BaseCommandSettings
    {
        [CommandOption("--source-path <PATH>")]
        [Description("Path to source code directory for code scanning. Defaults to parent directory of resource path.")]
        public string? SourcePath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();
        var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";

        // Determine source path for code scanning (same logic as ScanCommand)
        // Convert to absolute path first to handle relative paths correctly
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

        try
        {
            // Discover languages
            var languages = settings.DiscoverLanguages();
            var backendName = settings.GetBackendName();

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine($"[red]✗ No {backendName.ToUpper()} files found![/]");
                return 1;
            }

            // Parse resource files
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
                    AnsiConsole.MarkupLine($"[red]✗ Error parsing {lang.Name}: {ex.Message}[/]");
                    return 1;
                }
            }

            // Launch TUI - pass backend for writing
            var backend = settings.GetBackend();
            Application.Init();
            Application.Run(new ResourceEditorWindow(resourceFiles, backend, defaultCode, sourcePath, settings.LoadedConfiguration));
            Application.Shutdown();

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
