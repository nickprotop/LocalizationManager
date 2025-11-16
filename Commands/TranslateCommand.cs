// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalizationManager.Core;
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Translation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to translate resource keys using translation providers.
/// </summary>
public class TranslateCommand : AsyncCommand<TranslateCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "[KEY]")]
        [Description("The resource key pattern to translate (supports wildcards)")]
        public string? KeyPattern { get; set; }

        [CommandOption("--provider <PROVIDER>")]
        [Description("Translation provider (google, deepl, libretranslate, ollama, openai, claude, azureopenai). Default from config.")]
        public string? Provider { get; set; }

        [CommandOption("--source-language <LANG>")]
        [Description("Source language code (e.g., 'en', 'fr', or 'default'). Defaults to default language file (auto-detect).")]
        public string? SourceLanguage { get; set; }

        [CommandOption("--target-languages <LANGS>")]
        [Description("Target language codes, comma-separated (e.g., 'fr,de,es')")]
        public string? TargetLanguages { get; set; }

        [CommandOption("--only-missing")]
        [Description("Only translate keys with missing or empty values")]
        public bool OnlyMissing { get; set; }

        [CommandOption("--overwrite")]
        [Description("Allow overwriting existing translations (use with KEY pattern)")]
        public bool Overwrite { get; set; }

        [CommandOption("--dry-run")]
        [Description("Preview translations without saving")]
        public bool DryRun { get; set; }

        [CommandOption("--no-cache")]
        [Description("Disable translation cache")]
        public bool NoCache { get; set; }

        [CommandOption("--batch-size <SIZE>")]
        [Description("Number of translations to process in a batch (default: 10)")]
        public int? BatchSize { get; set; }

        [CommandOption("--no-backup")]
        [Description("Skip creating backups before translating")]
        public bool NoBackup { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            // Load configuration
            settings.LoadConfiguration();

            // Validate inputs
            if (!ValidateSettings(settings))
            {
                return 1;
            }

            // Discover resource files
            var resourcePath = settings.GetResourcePath();
            var discovery = new ResourceDiscovery();
            var languages = discovery.DiscoverLanguages(resourcePath);

            if (languages.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No resource files found.");
                return 1;
            }

            var defaultLanguage = languages.FirstOrDefault(f => f.IsDefault);
            if (defaultLanguage == null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No default resource file found.");
                return 1;
            }

            // Parse default resource file
            var parser = new ResourceFileParser();
            var defaultFile = parser.Parse(defaultLanguage);
            var entries = defaultFile.Entries;

            // Filter keys by pattern
            var keysToTranslate = FilterKeys(entries, settings.KeyPattern);

            if (keysToTranslate.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No keys match the pattern.[/]");
                return 0;
            }

            // Level 1 Safety Check: Require explicit intent
            if (!settings.OnlyMissing && string.IsNullOrWhiteSpace(settings.KeyPattern))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Translation requires explicit intent to prevent accidental overwrites.");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("Choose one option:");
                AnsiConsole.MarkupLine("  • Use [cyan]--only-missing[/] to translate only missing/empty keys (safe)");
                AnsiConsole.MarkupLine("  • Specify a [cyan]KEY[/] pattern to translate specific keys");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("Examples:");
                AnsiConsole.MarkupLine("  [dim]lrm translate --only-missing --target-language es[/]");
                AnsiConsole.MarkupLine("  [dim]lrm translate Welcome* --target-language es[/]");
                return 1;
            }

            // Determine target languages
            var targetLanguages = DetermineTargetLanguages(settings, languages);

            if (targetLanguages.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No target languages specified.");
                AnsiConsole.MarkupLine("Use: [cyan]--target-languages fr,de,es[/]");
                return 1;
            }

            // Determine source language
            var sourceLanguage = DetermineSourceLanguage(settings, defaultLanguage);

            // Level 2 Safety Check: When KEY pattern is provided, check for existing translations
            if (!string.IsNullOrWhiteSpace(settings.KeyPattern) && !settings.OnlyMissing && !settings.Overwrite)
            {
                var safetyParser = new ResourceFileParser();
                var existingCount = 0;

                foreach (var targetLang in targetLanguages)
                {
                    var targetLanguageInfo = languages.FirstOrDefault(f => f.Code == targetLang);
                    if (targetLanguageInfo != null)
                    {
                        var targetFile = safetyParser.Parse(targetLanguageInfo);
                        var targetDict = targetFile.Entries.ToDictionary(e => e.Key, e => e);

                        foreach (var key in keysToTranslate)
                        {
                            if (targetDict.TryGetValue(key.Key, out var existing) && !string.IsNullOrWhiteSpace(existing.Value))
                            {
                                existingCount++;
                            }
                        }
                    }
                }

                if (existingCount > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] {existingCount} key(s) already have translations that will be overwritten.");
                    AnsiConsole.MarkupLine("");

                    if (!AnsiConsole.Confirm("Do you want to continue and overwrite existing translations?", false))
                    {
                        AnsiConsole.MarkupLine("");
                        AnsiConsole.MarkupLine("[yellow]Translation cancelled.[/]");
                        AnsiConsole.MarkupLine("Tip: Use [cyan]--overwrite[/] flag to skip this prompt, or [cyan]--only-missing[/] to translate only missing keys.");
                        return 1;
                    }
                }
            }

            // Get provider
            var providerName = settings.Provider ?? settings.LoadedConfiguration?.Translation?.DefaultProvider ?? "google";
            var provider = CreateProvider(providerName, settings.LoadedConfiguration);

            if (!provider.IsConfigured())
            {
                ShowProviderNotConfiguredError(providerName);
                return 1;
            }

            // Create translation cache
            TranslationCache? cache = settings.NoCache ? null : new TranslationCache();

            try
            {
                // Translate
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Translating...", async ctx =>
                    {
                        await TranslateKeysAsync(
                            settings,
                            languages,
                            keysToTranslate,
                            targetLanguages,
                            sourceLanguage,
                            provider,
                            cache,
                            cancellationToken);
                    });

                if (!settings.DryRun)
                {
                    AnsiConsole.MarkupLine("[green]✓ Translation completed successfully.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Dry run completed. No changes were saved.[/]");
                }

                return 0;
            }
            finally
            {
                cache?.Dispose();
            }
        }
        catch (TranslationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Translation Error:[/] {ex.Message}");
            if (ex.IsRetryable)
            {
                AnsiConsole.MarkupLine("[yellow]This error may be temporary. Please try again.[/]");
            }
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private bool ValidateSettings(Settings settings)
    {
        // Provider validation happens in CreateProvider
        return true;
    }

    private List<ResourceEntry> FilterKeys(List<ResourceEntry> entries, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return entries;
        }

        // Simple wildcard matching
        var regex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return entries.Where(e => regex.IsMatch(e.Key)).ToList();
    }

    private List<string> DetermineTargetLanguages(Settings settings, List<LanguageInfo> resourceFiles)
    {
        if (!string.IsNullOrWhiteSpace(settings.TargetLanguages))
        {
            return settings.TargetLanguages
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        // If not specified, use all non-default languages
        return resourceFiles
            .Where(f => !f.IsDefault)
            .Select(f => f.Code)
            .ToList();
    }

    private string? DetermineSourceLanguage(Settings settings, LanguageInfo defaultLanguage)
    {
        // Priority 1: Explicit --source-language argument
        if (!string.IsNullOrWhiteSpace(settings.SourceLanguage))
        {
            // User can specify "default" to explicitly use default language
            if (settings.SourceLanguage.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                return null; // null means use default (auto-detect from default file)
            }
            return settings.SourceLanguage;
        }

        // Priority 2: DefaultLanguageCode from configuration
        if (!string.IsNullOrWhiteSpace(settings.LoadedConfiguration?.DefaultLanguageCode))
        {
            return settings.LoadedConfiguration.DefaultLanguageCode;
        }

        // Priority 3: Fallback to auto-detect
        // We return null which means auto-detect, and the provider will detect from the source text
        return null;
    }

    private ITranslationProvider CreateProvider(string providerName, ConfigurationModel? config)
    {
        return TranslationProviderFactory.Create(providerName, config);
    }

    private async Task TranslateKeysAsync(
        Settings settings,
        List<LanguageInfo> languages,
        List<ResourceEntry> keysToTranslate,
        List<string> targetLanguages,
        string? sourceLanguage,
        ITranslationProvider provider,
        TranslationCache? cache,
        CancellationToken cancellationToken)
    {
        var batchSize = settings.BatchSize ?? settings.LoadedConfiguration?.Translation?.BatchSize ?? 10;
        var totalTranslations = keysToTranslate.Count * targetLanguages.Count;
        var completed = 0;

        // Create a table to show results
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[cyan]Key[/]");
        table.AddColumn("[cyan]Language[/]");
        table.AddColumn("[cyan]Translation[/]");
        table.AddColumn("[cyan]Status[/]");

        var parser = new ResourceFileParser();

        foreach (var targetLang in targetLanguages)
        {
            // Find the target language resource file
            var targetLanguageInfo = languages.FirstOrDefault(f => f.Code == targetLang);
            if (targetLanguageInfo == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] No resource file for language '{targetLang}'. Skipping.");
                continue;
            }

            // Load target resource file
            var targetFile = parser.Parse(targetLanguageInfo);
            var targetDict = targetFile.Entries.ToDictionary(e => e.Key, e => e);

            foreach (var key in keysToTranslate)
            {
                // Check if already translated
                if (settings.OnlyMissing &&
                    targetDict.TryGetValue(key.Key, out var existing) &&
                    !string.IsNullOrWhiteSpace(existing.Value))
                {
                    completed++;
                    continue;
                }

                try
                {
                    // Try cache first
                    var request = new TranslationRequest
                    {
                        SourceText = key.Value ?? string.Empty,
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLang,
                        TargetLanguageName = targetLanguageInfo.Name // Use the display name from LanguageInfo
                    };

                    TranslationResponse response;

                    if (cache != null && cache.TryGet(request, provider.Name, out var cachedResponse))
                    {
                        response = cachedResponse!;
                    }
                    else
                    {
                        // Translate via API
                        response = await provider.TranslateAsync(request, cancellationToken);

                        // Store in cache
                        cache?.Store(request, response);
                    }

                    // Update or add entry
                    if (targetDict.ContainsKey(key.Key))
                    {
                        targetDict[key.Key].Value = response.TranslatedText;
                    }
                    else
                    {
                        targetFile.Entries.Add(new ResourceEntry
                        {
                            Key = key.Key,
                            Value = response.TranslatedText,
                            Comment = key.Comment
                        });
                    }

                    // Add to results table
                    var status = response.FromCache ? "[dim](cached)[/]" : "[green]✓[/]";
                    table.AddRow(
                        key.Key.Length > 30 ? key.Key.Substring(0, 27) + "..." : key.Key,
                        targetLang,
                        response.TranslatedText.Length > 40 ? response.TranslatedText.Substring(0, 37) + "..." : response.TranslatedText,
                        status);
                }
                catch (TranslationException ex)
                {
                    table.AddRow(
                        key.Key.Length > 30 ? key.Key.Substring(0, 27) + "..." : key.Key,
                        targetLang,
                        "[dim]N/A[/]",
                        $"[red]Error: {ex.ErrorCode}[/]");
                }

                completed++;
            }

            // Create backup before saving
            if (!settings.DryRun && !settings.NoBackup)
            {
                var backupManager = new BackupVersionManager(10);
                var basePath = System.IO.Path.GetDirectoryName(targetLanguageInfo.FilePath) ?? Environment.CurrentDirectory;
                await backupManager.CreateBackupAsync(targetLanguageInfo.FilePath, "translate", basePath);
            }

            // Save the updated resource file
            if (!settings.DryRun)
            {
                parser.Write(targetFile);
            }
        }

        // Show results
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Translated [cyan]{completed}[/] of [cyan]{totalTranslations}[/] items.");
    }

    private void ShowProviderNotConfiguredError(string provider)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Provider '[cyan]{provider}[/]' is not configured.");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]To configure the provider:[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]1. Environment variable (recommended):[/]");
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

        AnsiConsole.MarkupLine("[cyan]2. Secure credential store:[/]");
        AnsiConsole.MarkupLine($"   lrm config set-api-key --provider {provider} --key \"your-key\"");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]3. Configuration file:[/]");
        AnsiConsole.MarkupLine("   Add to lrm.json:");
        AnsiConsole.MarkupLine("   {");
        AnsiConsole.MarkupLine("     \"Translation\": {");
        AnsiConsole.MarkupLine("       \"ApiKeys\": {");
        AnsiConsole.MarkupLine($"         \"{provider.Substring(0, 1).ToUpper() + provider.Substring(1)}\": \"your-api-key\"");
        AnsiConsole.MarkupLine("       }");
        AnsiConsole.MarkupLine("     }");
        AnsiConsole.MarkupLine("   }");
    }
}
