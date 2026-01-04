// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Config;

/// <summary>
/// Command to set an API key in the secure credential store.
/// </summary>
public class SetApiKeyCommand : Command<SetApiKeyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-p|--provider <PROVIDER>")]
        [Description("The translation provider (google, deepl, libretranslate, ollama, openai, claude, azureopenai)")]
        public string Provider { get; set; } = string.Empty;

        [CommandOption("-k|--key <KEY>")]
        [Description("The API key to store")]
        public string ApiKey { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate provider
            if (string.IsNullOrWhiteSpace(settings.Provider))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Provider name is required.");
                AnsiConsole.MarkupLine("Use: [cyan]lrm config set-api-key --provider <provider> --key <key>[/]");
                return 1;
            }

            if (!TranslationProviderFactory.IsProviderSupported(settings.Provider))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown provider '{settings.Provider}'.");
                AnsiConsole.MarkupLine($"Supported providers: [cyan]{string.Join(", ", TranslationProviderFactory.GetSupportedProviders())}[/]");
                return 1;
            }

            // Validate API key
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] API key is required.");
                AnsiConsole.MarkupLine("Use: [cyan]lrm config set-api-key --provider <provider> --key <key>[/]");
                return 1;
            }

            // Store the API key
            SecureCredentialManager.SetApiKey(settings.Provider, settings.ApiKey);

            AnsiConsole.MarkupLine($"[green]✓[/] API key for '[cyan]{settings.Provider}[/]' stored successfully.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Stored in: [dim]{AppDataPaths.GetCredentialsFilePath()}[/]");
            AnsiConsole.WriteLine();

            // Show usage info
            AnsiConsole.MarkupLine("[green]The API key is now ready to use.[/] No additional configuration needed.");
            AnsiConsole.WriteLine();

            // Show alternative with environment variables
            AnsiConsole.MarkupLine("[dim]For CI/CD, use environment variables instead:[/]");
            if (OperatingSystem.IsWindows())
            {
                AnsiConsole.MarkupLine($"  [dim]PowerShell:[/] $env:LRM_{settings.Provider.ToUpperInvariant()}_API_KEY=\"your-key\"");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [dim]export LRM_{settings.Provider.ToUpperInvariant()}_API_KEY=\"your-key\"[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
