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
/// Command to check where an API key is configured from.
/// </summary>
public class GetApiKeyCommand : Command<GetApiKeyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--provider <PROVIDER>")]
        [Description("The translation provider (google, deepl, libretranslate, ollama, openai, claude, azureopenai)")]
        public string Provider { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate provider
            if (string.IsNullOrWhiteSpace(settings.Provider))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Provider name is required.");
                AnsiConsole.MarkupLine("Use: [cyan]lrm config get-api-key --provider <provider>[/]");
                return 1;
            }

            if (!TranslationProviderFactory.IsProviderSupported(settings.Provider))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown provider '{settings.Provider}'.");
                AnsiConsole.MarkupLine($"Supported providers: [cyan]{string.Join(", ", TranslationProviderFactory.GetSupportedProviders())}[/]");
                return 1;
            }

            // Load configuration
            var (config, _) = Core.Configuration.ConfigurationManager.LoadConfiguration(null, Directory.GetCurrentDirectory());

            // Check where the API key is configured
            var source = ApiKeyResolver.GetApiKeySource(settings.Provider, config);

            if (source == null)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] API key for '[cyan]{settings.Provider}[/]' is [red]not configured[/].");
                AnsiConsole.WriteLine();
                ShowConfigurationHelp(settings.Provider);
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] API key for '[cyan]{settings.Provider}[/]' is configured.");
            AnsiConsole.MarkupLine($"Source: [cyan]{source}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static void ShowConfigurationHelp(string provider)
    {
        AnsiConsole.MarkupLine("[yellow]To configure the API key, use one of these methods:[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]1. Environment variable (recommended for CI/CD):[/]");
        if (OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine($"   PowerShell: $env:LRM_{provider.ToUpperInvariant()}_API_KEY=\"your-key\"");
            AnsiConsole.MarkupLine($"   CMD: set LRM_{provider.ToUpperInvariant()}_API_KEY=your-key");
        }
        else
        {
            AnsiConsole.MarkupLine($"   export LRM_{provider.ToUpperInvariant()}_API_KEY=\"your-key\"");
        }
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]2. Configuration file (convenient for local development):[/]");
        AnsiConsole.MarkupLine("   Add to lrm.json:");
        AnsiConsole.MarkupLine("   {");
        AnsiConsole.MarkupLine("     \"Translation\": {");
        AnsiConsole.MarkupLine("       \"ApiKeys\": {");
        AnsiConsole.MarkupLine($"         \"{provider.Substring(0, 1).ToUpper() + provider.Substring(1)}\": \"your-api-key-here\"");
        AnsiConsole.MarkupLine("       }");
        AnsiConsole.MarkupLine("     }");
        AnsiConsole.MarkupLine("   }");
        AnsiConsole.MarkupLine("   [yellow]⚠ WARNING: Do not commit API keys to git![/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]3. Secure credential store (optional, advanced):[/]");
        AnsiConsole.MarkupLine($"   lrm config set-api-key --provider {provider} --key \"your-key\"");
    }
}
