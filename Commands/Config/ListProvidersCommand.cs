// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands.Config;

/// <summary>
/// Command to list all translation providers and their configuration status.
/// </summary>
public class ListProvidersCommand : Command<ListProvidersCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings, System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            // Load configuration
            var (config, _) = Core.Configuration.ConfigurationManager.LoadConfiguration(null, Directory.GetCurrentDirectory());

            // Create table
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[cyan]Provider[/]");
            table.AddColumn("[cyan]Status[/]");
            table.AddColumn("[cyan]Source[/]");

            // Check each provider
            foreach (var provider in TranslationProviderFactory.GetSupportedProviders())
            {
                var hasKey = ApiKeyResolver.HasApiKey(provider, config);
                var source = ApiKeyResolver.GetApiKeySource(provider, config);

                var status = hasKey ? "[green]✓ Configured[/]" : "[red]✗ Not configured[/]";
                var sourceText = source ?? "[dim]N/A[/]";

                table.AddRow(provider, status, sourceText);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Show configuration instructions
            var anyConfigured = false;
            foreach (var provider in TranslationProviderFactory.GetSupportedProviders())
            {
                if (ApiKeyResolver.HasApiKey(provider, config))
                {
                    anyConfigured = true;
                    break;
                }
            }

            if (!anyConfigured)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No providers are configured.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("To configure a provider, use:");
                AnsiConsole.MarkupLine("  [cyan]lrm config set-api-key --provider <provider> --key <key>[/]");
                AnsiConsole.MarkupLine("  [cyan]lrm config get-api-key --provider <provider>[/]");
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
