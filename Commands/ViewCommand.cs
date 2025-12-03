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
using LocalizationManager.Core.Enums;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace LocalizationManager.Commands;

/// <summary>
/// Represents a key occurrence for tracking duplicates
/// </summary>
public class KeyOccurrence
{
    public string Key { get; set; } = string.Empty;
    public int Occurrence { get; set; } = 1;
    public int TotalOccurrences { get; set; } = 1;
    public string DisplayKey => TotalOccurrences > 1 ? $"{Key} [{Occurrence}]" : Key;
}

/// <summary>
/// Command to view details of a specific localization key.
/// </summary>
public class ViewCommand : Command<ViewCommand.Settings>
{
    public class Settings : BaseFormattableCommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("The key or pattern to view")]
        public required string Key { get; set; }

        [CommandOption("--regex")]
        [Description("Treat the key as a regular expression pattern")]
        public bool UseRegex { get; set; }

        [CommandOption("--show-comments")]
        [Description("Show comments for the key(s)")]
        public bool ShowComments { get; set; }

        [CommandOption("--limit <COUNT>")]
        [Description("Maximum number of keys to display (default: 100, 0 for no limit)")]
        [DefaultValue(100)]
        public int Limit { get; set; } = 100;

        [CommandOption("--no-limit")]
        [Description("Show all matches without limit")]
        public bool NoLimit { get; set; }

        [CommandOption("--sort")]
        [Description("Sort matched keys alphabetically")]
        public bool Sort { get; set; }

        [CommandOption("--cultures <CODES>")]
        [Description("Include only specific cultures (comma-separated, e.g., en,fr,el,default)")]
        public string? Cultures { get; set; }

        [CommandOption("--exclude <CODES>")]
        [Description("Exclude specific cultures (comma-separated, e.g., el,fr,default)")]
        public string? ExcludeCultures { get; set; }

        [CommandOption("--keys-only")]
        [Description("Output only key names without translations")]
        public bool KeysOnly { get; set; }

        [CommandOption("--no-translations")]
        [Description("Alias for --keys-only")]
        public bool NoTranslations { get; set; }

        [CommandOption("--search-in|--scope <SCOPE>")]
        [Description("Where to search: keys (default), values, both, comments, or all")]
        [DefaultValue(SearchScope.Keys)]
        public SearchScope SearchIn { get; set; } = SearchScope.Keys;

        [CommandOption("--case-sensitive")]
        [Description("Make search case-sensitive (default is case-insensitive)")]
        [DefaultValue(false)]
        public bool CaseSensitive { get; set; } = false;

        [CommandOption("--count")]
        [Description("Show only the count of matching keys")]
        [DefaultValue(false)]
        public bool Count { get; set; } = false;

        [CommandOption("--status <STATUS>")]
        [Description("Filter by translation status: empty, missing, untranslated, complete, or partial")]
        public TranslationStatus? Status { get; set; }

        [CommandOption("--not <PATTERNS>")]
        [Description("Exclude keys matching these patterns (can be used multiple times, supports wildcards)")]
        public string[]? NotPatterns { get; set; }
    }

    /// <summary>
    /// Defines where to search for the pattern
    /// </summary>
    public enum SearchScope
    {
        [Description("Search only in key names")]
        Keys,

        [Description("Search only in translation values")]
        Values,

        [Description("Search in both keys and values")]
        Both,

        [Description("Search only in comments")]
        Comments,

        [Description("Search in keys, values, and comments")]
        All
    }

    /// <summary>
    /// Defines translation status for filtering
    /// </summary>
    public enum TranslationStatus
    {
        [Description("Keys with empty or whitespace-only values in any language")]
        Empty,

        [Description("Keys missing in any language file")]
        Missing,

        [Description("Keys that are untranslated (empty, missing, or identical to default)")]
        Untranslated,

        [Description("Keys with translations in all languages")]
        Complete,

        [Description("Keys with some but not all translations")]
        Partial
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();

        try
        {
            var format = settings.GetOutputFormat();
            if (format == OutputFormat.Table)
            {
                AnsiConsole.MarkupLine($"[blue]Scanning:[/] {Markup.Escape(resourcePath)}");
                AnsiConsole.WriteLine();
            }

            // Discover languages using the backend (auto-detected or specified)
            var languages = settings.DiscoverLanguages();
            var backendName = settings.GetBackendName();

            if (!languages.Any())
            {
                AnsiConsole.MarkupLine($"[red]✗ No {backendName.ToUpper()} resource files found![/]");
                return 1;
            }

            // Parse resource files using the backend
            var resourceFiles = new List<Core.Models.ResourceFile>();

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

            // Check if key exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                AnsiConsole.MarkupLine("[red]✗ No default language file found![/]");
                return 1;
            }

            // Find matching keys
            List<string> matchedKeys;
            bool usedWildcards = false;
            string originalPattern = settings.Key;
            int? requestedOccurrence = null;

            // Parse occurrence syntax like "Key [2]" before other processing
            var (parsedKey, occurrence) = ParseKeyWithOccurrence(settings.Key);
            if (occurrence.HasValue)
            {
                settings.Key = parsedKey;
                originalPattern = parsedKey;
                requestedOccurrence = occurrence;
            }

            // Auto-detect and convert wildcard patterns to regex
            if (!settings.UseRegex && IsWildcardPattern(settings.Key))
            {
                settings.Key = ConvertWildcardToRegex(settings.Key);
                settings.UseRegex = true;
                usedWildcards = true;
            }

            // Find matching keys based on search scope
            try
            {
                matchedKeys = FindMatchingKeys(defaultFile, resourceFiles, settings.Key, settings.SearchIn, settings.UseRegex, settings.CaseSensitive);

                // For exact match with keys-only scope, show error if not found
                if (!settings.UseRegex && settings.SearchIn == SearchScope.Keys && matchedKeys.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Key '{Markup.Escape(settings.Key)}' not found![/]");
                    return 1;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                AnsiConsole.MarkupLine("[red]✗ Regex pattern timed out (too complex)[/]");
                return 1;
            }
            catch (RegexParseException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid regex pattern: {ex.Message}[/]");
                return 1;
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Invalid regex pattern: {ex.Message}[/]");
                return 1;
            }

            // Apply sorting if requested
            if (settings.Sort)
            {
                matchedKeys = matchedKeys.OrderBy(k => k).ToList();
            }

            // Keep original resource files for status filtering (needs all languages)
            var allResourceFiles = resourceFiles;

            // Apply culture filtering for display
            List<string> invalidCodes;
            if (!string.IsNullOrEmpty(settings.Cultures) || !string.IsNullOrEmpty(settings.ExcludeCultures))
            {
                var originalCount = resourceFiles.Count;
                resourceFiles = FilterResourceFiles(resourceFiles, settings, out invalidCodes);

                // Warn about invalid culture codes
                if (invalidCodes.Any())
                {
                    if (format == OutputFormat.Table)
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠ Unknown culture code(s): {string.Join(", ", invalidCodes)}[/]");
                        AnsiConsole.WriteLine();
                    }
                }

                // Inform when auto keys-only mode activates
                if (resourceFiles.Count == 0 && !settings.KeysOnly && !settings.NoTranslations)
                {
                    if (format == OutputFormat.Table)
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠ All cultures filtered out - showing keys only[/]");
                        AnsiConsole.WriteLine();
                    }
                }
            }

            // Apply status filtering (use original resource files for status checks)
            if (settings.Status.HasValue)
            {
                matchedKeys = FilterByStatus(matchedKeys, defaultFile, allResourceFiles, settings.Status.Value);
            }

            // Apply exclusion patterns
            if (settings.NotPatterns != null && settings.NotPatterns.Length > 0)
            {
                matchedKeys = ApplyExclusions(matchedKeys, settings.NotPatterns, settings.CaseSensitive);
            }

            // Apply limit
            var effectiveLimit = settings.NoLimit || settings.Limit == 0 ? int.MaxValue : settings.Limit;
            var totalMatches = matchedKeys.Count;

            if (matchedKeys.Count > effectiveLimit)
            {
                if (format == OutputFormat.Table)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ Found {totalMatches} matches, showing first {effectiveLimit}. Use --limit to adjust.[/]");
                    AnsiConsole.WriteLine();
                }
                matchedKeys = matchedKeys.Take(effectiveLimit).ToList();
            }

            // Handle --count flag (exit early)
            if (settings.Count)
            {
                DisplayCount(matchedKeys.Count, settings, usedWildcards, originalPattern, format);
                return 0;
            }

            // Detect extra keys in filtered languages (not in default)
            var extraKeys = DetectExtraKeysInFilteredFiles(defaultFile, resourceFiles);
            if (extraKeys.Any())
            {
                if (format == OutputFormat.Table || format == OutputFormat.Simple)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Warning: Some filtered languages contain keys not in default:[/]");
                    foreach (var kvp in extraKeys)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]• {kvp.Key}: {kvp.Value.Count} extra key(s)[/]");
                    }
                    AnsiConsole.MarkupLine("[dim]Run 'lrm validate' to detect all inconsistencies[/]");
                    AnsiConsole.WriteLine();
                }
            }

            // Check if we have any matches
            if (matchedKeys.Count == 0)
            {
                var patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "pattern" : "key");
                var displayPattern = usedWildcards ? originalPattern : settings.Key;
                // Escape pattern to prevent Spectre.Console markup interpretation
                AnsiConsole.MarkupLine($"[red]✗ No keys match {patternType} '{Markup.Escape(displayPattern)}'[/]");
                return 1;
            }

            // Convert matched keys to occurrences (handles both case variants and true duplicates)
            var keyOccurrences = ConvertToOccurrences(matchedKeys, defaultFile, settings.CaseSensitive);

            // Show warning if duplicates were found (case variants or true duplicates)
            var uniqueKeys = keyOccurrences.Select(o => o.Key).Distinct().Count();
            var totalOccurrences = keyOccurrences.Count;

            if (format == OutputFormat.Table && !settings.Count)
            {
                // Check for case variants (multiple unique keys from same pattern)
                if (uniqueKeys > 1 && !settings.UseRegex && !usedWildcards)
                {
                    var caseVariants = keyOccurrences.Select(o => o.Key).Distinct().ToList();
                    AnsiConsole.MarkupLine($"[yellow]⚠ Found {uniqueKeys} case variant(s) matching '{Markup.Escape(originalPattern)}':[/]");
                    foreach (var variant in caseVariants)
                    {
                        var count = keyOccurrences.Count(o => o.Key == variant);
                        if (count > 1)
                            AnsiConsole.MarkupLine($"  [dim]• {variant} ({count} occurrences)[/]");
                        else
                            AnsiConsole.MarkupLine($"  [dim]• {variant}[/]");
                    }
                    AnsiConsole.MarkupLine("[dim]Use --case-sensitive to match exact casing.[/]");
                    AnsiConsole.WriteLine();
                }
                // Check for true duplicates (same key appearing multiple times)
                else if (totalOccurrences > uniqueKeys)
                {
                    var duplicateKeys = keyOccurrences
                        .GroupBy(o => o.Key)
                        .Where(g => g.Count() > 1)
                        .Select(g => new { Key = g.Key, Count = g.Count() })
                        .ToList();

                    AnsiConsole.MarkupLine($"[yellow]⚠ Found duplicate key(s):[/]");
                    foreach (var dup in duplicateKeys)
                    {
                        AnsiConsole.MarkupLine($"  [dim]• {dup.Key} ({dup.Count} occurrences)[/]");
                    }
                    AnsiConsole.MarkupLine("[dim]Use \"Key [N]\" to view a specific occurrence.[/]");
                    AnsiConsole.WriteLine();
                }
            }

            // If a specific occurrence was requested, filter to just that one
            if (requestedOccurrence.HasValue)
            {
                var filteredOccurrences = keyOccurrences
                    .Where(ko => ko.Key.Equals(originalPattern, settings.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)
                                 && ko.Occurrence == requestedOccurrence.Value)
                    .ToList();

                if (filteredOccurrences.Count == 0)
                {
                    // Check if key exists but occurrence is out of range
                    var maxOccurrence = keyOccurrences
                        .Where(ko => ko.Key.Equals(originalPattern, settings.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                        .Select(ko => ko.TotalOccurrences)
                        .FirstOrDefault();

                    if (maxOccurrence > 0)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Key '{Markup.Escape(originalPattern)}' has only {maxOccurrence} occurrence(s), but [{requestedOccurrence}] was requested.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Key '{Markup.Escape(originalPattern)}' not found![/]");
                    }
                    return 1;
                }

                keyOccurrences = filteredOccurrences;
            }

            // Display based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(keyOccurrences, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(keyOccurrences, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(keyOccurrences, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
                    break;
            }

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

    private void DisplayConfigNotice(Settings settings)
    {
        if (!string.IsNullOrEmpty(settings.LoadedConfigurationPath))
        {
            AnsiConsole.MarkupLine($"[dim]Using configuration from: {settings.LoadedConfigurationPath}[/]");
            AnsiConsole.WriteLine();
        }
    }

    private void DisplayCount(int count, Settings settings, bool usedWildcards, string originalPattern, OutputFormat format)
    {
        DisplayConfigNotice(settings);

        string patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "regex" : "exact");

        if (format == OutputFormat.Json)
        {
            var output = new
            {
                pattern = originalPattern,
                patternType = patternType,
                matchCount = count
            };
            Console.WriteLine(OutputFormatter.FormatJson(output));
        }
        else if (format == OutputFormat.Simple)
        {
            Console.WriteLine($"Pattern: {originalPattern} ({patternType})");
            Console.WriteLine($"Found {count} matching key(s)");
        }
        else // Table format
        {
            AnsiConsole.MarkupLine($"[yellow]Pattern: {Markup.Escape(originalPattern)} [dim]({patternType})[/][/]");
            AnsiConsole.MarkupLine($"[green]Found {count} matching key(s)[/]");
        }
    }

    private void DisplayTable(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        DisplayConfigNotice(settings);

        // Always use array format for consistency
        DisplayMultipleKeysTable(occurrences, resourceFiles, showComments, settings, usedWildcards, originalPattern);
    }

    private void DisplayMultipleKeysTable(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        var uniqueKeys = occurrences.Select(o => o.Key).Distinct().Count();
        var totalOccurrences = occurrences.Count;

        // Check if keys-only mode
        if (IsKeysOnlyMode(settings, resourceFiles))
        {
            string patternDisplay;
            if (usedWildcards)
            {
                patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](wildcard)[/]";
            }
            else if (settings.UseRegex)
            {
                patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](regex)[/]";
            }
            else
            {
                patternDisplay = $"Keys: {uniqueKeys}";
            }

            AnsiConsole.MarkupLine($"[yellow]{patternDisplay}[/]");
            if (totalOccurrences != uniqueKeys)
            {
                AnsiConsole.MarkupLine($"[dim]Matched {totalOccurrences} occurrence(s) of {uniqueKeys} key(s)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Matched {uniqueKeys} key(s)[/]");
            }
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("Key");

            foreach (var occ in occurrences)
            {
                table.AddRow(occ.DisplayKey.EscapeMarkup());
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Showing {totalOccurrences} occurrence(s)[/]");
        }
        else
        {
            // Normal mode with translations
            string patternDisplay;
            if (usedWildcards)
            {
                patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](wildcard)[/]";
            }
            else if (settings.UseRegex)
            {
                patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](regex)[/]";
            }
            else
            {
                patternDisplay = $"Keys: {uniqueKeys}";
            }

            AnsiConsole.MarkupLine($"[yellow]{patternDisplay}[/]");
            if (totalOccurrences != uniqueKeys)
            {
                AnsiConsole.MarkupLine($"[dim]Matched {totalOccurrences} occurrence(s) of {uniqueKeys} key(s)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Matched {uniqueKeys} key(s)[/]");
            }
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Language");
            table.AddColumn("Value");
            if (showComments)
            {
                table.AddColumn("Comment");
            }

            foreach (var occ in occurrences)
            {
                bool firstRowForKey = true;
                foreach (var rf in resourceFiles)
                {
                    // Use GetNthOccurrence for true duplicates, or find by key for non-duplicates
                    ResourceEntry? entry;
                    if (occ.TotalOccurrences > 1)
                    {
                        entry = GetNthOccurrence(rf, occ.Key, occ.Occurrence);
                    }
                    else
                    {
                        entry = rf.Entries.FirstOrDefault(e => e.Key == occ.Key);
                    }

                    var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
                    var langName = rf.Language.IsDefault
                        ? $"{rf.Language.Name} [yellow]({defaultCode})[/]"
                        : rf.Language.Name;

                    var keyDisplay = firstRowForKey ? occ.DisplayKey.EscapeMarkup() : "";
                    var value = entry?.IsEmpty == true ? "[dim](empty)[/]" : (entry?.Value?.EscapeMarkup() ?? "[red](missing)[/]");

                    if (showComments)
                    {
                        var comment = entry == null || string.IsNullOrWhiteSpace(entry.Comment)
                            ? "[dim](no comment)[/]"
                            : entry.Comment.EscapeMarkup();
                        table.AddRow(keyDisplay, langName, value, comment);
                    }
                    else
                    {
                        table.AddRow(keyDisplay, langName, value);
                    }

                    firstRowForKey = false;
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Showing {totalOccurrences} occurrence(s) across {resourceFiles.Count} language(s)[/]");
        }
    }

    private void DisplayJson(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        // Always use array format for consistency
        DisplayMultipleKeysJson(occurrences, resourceFiles, showComments, settings, usedWildcards, originalPattern);
    }

    private void DisplayMultipleKeysJson(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        var uniqueKeys = occurrences.Select(o => o.Key).Distinct().Count();
        var totalOccurrences = occurrences.Count;

        // Check if keys-only mode
        if (IsKeysOnlyMode(settings, resourceFiles))
        {
            // Use same structure but with empty translations for consistency
            var keyObjects = occurrences.Select(occ => new
            {
                key = occ.Key,
                occurrence = occ.Occurrence,
                totalOccurrences = occ.TotalOccurrences,
                displayKey = occ.DisplayKey,
                translations = new Dictionary<string, object?>()
            }).ToList();

            var output = new
            {
                pattern = settings.UseRegex || usedWildcards ? originalPattern : (string?)null,
                patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "regex" : (string?)null),
                uniqueKeys = uniqueKeys,
                totalOccurrences = totalOccurrences,
                keys = keyObjects
            };

            Console.WriteLine(OutputFormatter.FormatJson(output));
        }
        else
        {
            // Normal mode with translations
            var keyObjects = new List<object>();

            foreach (var occ in occurrences)
            {
                var translations = new Dictionary<string, object?>();
                foreach (var rf in resourceFiles)
                {
                    // Use GetNthOccurrence for true duplicates, or find by key for non-duplicates
                    ResourceEntry? entry;
                    if (occ.TotalOccurrences > 1)
                    {
                        entry = GetNthOccurrence(rf, occ.Key, occ.Occurrence);
                    }
                    else
                    {
                        entry = rf.Entries.FirstOrDefault(e => e.Key == occ.Key);
                    }

                    var langCode = rf.Language.GetDisplayCode();

                    if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
                    {
                        translations[langCode] = new
                        {
                            value = entry.Value,
                            comment = entry.Comment
                        };
                    }
                    else
                    {
                        translations[langCode] = entry?.Value;
                    }
                }

                keyObjects.Add(new
                {
                    key = occ.Key,
                    occurrence = occ.Occurrence,
                    totalOccurrences = occ.TotalOccurrences,
                    displayKey = occ.DisplayKey,
                    translations = translations
                });
            }

            var output = new
            {
                pattern = settings.UseRegex || usedWildcards ? originalPattern : (string?)null,
                patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "regex" : (string?)null),
                uniqueKeys = uniqueKeys,
                totalOccurrences = totalOccurrences,
                keys = keyObjects
            };

            Console.WriteLine(OutputFormatter.FormatJson(output));
        }
    }

    private void DisplaySimple(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        DisplayConfigNotice(settings);

        // Always use array format for consistency
        DisplayMultipleKeysSimple(occurrences, resourceFiles, showComments, settings, usedWildcards, originalPattern);
    }

    private void DisplayMultipleKeysSimple(List<KeyOccurrence> occurrences, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        var uniqueKeys = occurrences.Select(o => o.Key).Distinct().Count();
        var totalOccurrences = occurrences.Count;

        // Check if keys-only mode
        if (IsKeysOnlyMode(settings, resourceFiles))
        {
            string patternDisplay;
            if (usedWildcards)
            {
                patternDisplay = $"Pattern: {originalPattern} (wildcard)";
            }
            else if (settings.UseRegex)
            {
                patternDisplay = $"Pattern: {originalPattern} (regex)";
            }
            else
            {
                patternDisplay = $"Keys: {uniqueKeys}";
            }

            Console.WriteLine(patternDisplay);
            if (totalOccurrences != uniqueKeys)
            {
                Console.WriteLine($"Matched {totalOccurrences} occurrence(s) of {uniqueKeys} key(s)");
            }
            else
            {
                Console.WriteLine($"Matched {uniqueKeys} key(s)");
            }
            Console.WriteLine();

            foreach (var occ in occurrences)
            {
                Console.WriteLine(occ.DisplayKey);
            }
        }
        else
        {
            // Normal mode with translations
            string patternDisplay;
            if (usedWildcards)
            {
                patternDisplay = $"Pattern: {originalPattern} (wildcard)";
            }
            else if (settings.UseRegex)
            {
                patternDisplay = $"Pattern: {originalPattern} (regex)";
            }
            else
            {
                patternDisplay = $"Keys: {uniqueKeys}";
            }

            Console.WriteLine(patternDisplay);
            if (totalOccurrences != uniqueKeys)
            {
                Console.WriteLine($"Matched {totalOccurrences} occurrence(s) of {uniqueKeys} key(s)");
            }
            else
            {
                Console.WriteLine($"Matched {uniqueKeys} key(s)");
            }
            Console.WriteLine();

            for (int i = 0; i < occurrences.Count; i++)
            {
                var occ = occurrences[i];
                Console.WriteLine($"--- {occ.DisplayKey} ---");

                foreach (var rf in resourceFiles)
                {
                    // Use GetNthOccurrence for true duplicates, or find by key for non-duplicates
                    ResourceEntry? entry;
                    if (occ.TotalOccurrences > 1)
                    {
                        entry = GetNthOccurrence(rf, occ.Key, occ.Occurrence);
                    }
                    else
                    {
                        entry = rf.Entries.FirstOrDefault(e => e.Key == occ.Key);
                    }

                    var defaultCode = settings.LoadedConfiguration?.DefaultLanguageCode ?? "default";
                    var langLabel = rf.Language.IsDefault
                        ? $"{rf.Language.Name} ({defaultCode})"
                        : rf.Language.Name;
                    var value = entry?.Value ?? "(missing)";

                    Console.WriteLine($"{langLabel}: {value}");

                    if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
                    {
                        Console.WriteLine($"  Comment: {entry.Comment}");
                    }
                }

                // Add blank line between keys except after last one
                if (i < occurrences.Count - 1)
                {
                    Console.WriteLine();
                }
            }
        }
    }

    /// <summary>
    /// Detects if a pattern contains wildcard characters (* or ?) that should be converted to regex.
    /// Handles backslash escaping for literal wildcard characters.
    /// </summary>
    public static bool IsWildcardPattern(string pattern)
    {
        // Simply check if pattern contains unescaped wildcards
        // The --regex flag takes precedence, so we don't need "smart" detection
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];

            // Check if this is an escaped character
            if (c == '\\' && i + 1 < pattern.Length)
            {
                i++; // Skip next character
                continue;
            }

            // Check for unescaped wildcards
            if (c == '*' || c == '?')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a wildcard pattern to a regex pattern.
    /// Supports:
    /// - * for zero or more characters
    /// - ? for exactly one character
    /// - \* and \? for literal asterisk and question mark
    /// </summary>
    public static string ConvertWildcardToRegex(string wildcardPattern)
    {
        var result = new System.Text.StringBuilder();

        for (int i = 0; i < wildcardPattern.Length; i++)
        {
            char c = wildcardPattern[i];

            if (c == '\\' && i + 1 < wildcardPattern.Length)
            {
                char next = wildcardPattern[i + 1];
                if (next == '*' || next == '?')
                {
                    // Escaped wildcard - treat as literal
                    result.Append('\\').Append(next);
                    i++; // Skip next character
                }
                else
                {
                    // Other escaped character - escape for regex
                    result.Append(Regex.Escape(c.ToString()));
                }
            }
            else if (c == '*')
            {
                // Wildcard: match any characters
                result.Append(".*");
            }
            else if (c == '?')
            {
                // Wildcard: match single character
                result.Append('.');
            }
            else
            {
                // Regular character - escape for regex
                result.Append(Regex.Escape(c.ToString()));
            }
        }

        // Anchor to match entire string
        return "^" + result.ToString() + "$";
    }

    /// <summary>
    /// Parse and normalize comma-separated culture codes
    /// </summary>
    public static List<string> ParseCultureCodes(string? cultureString)
    {
        if (string.IsNullOrWhiteSpace(cultureString))
        {
            return new List<string>();
        }

        return cultureString
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Determine if keys-only mode should be used
    /// </summary>
    public static bool IsKeysOnlyMode(Settings settings, List<Core.Models.ResourceFile> resourceFiles)
    {
        // Explicit flags
        if (settings.KeysOnly || settings.NoTranslations)
        {
            return true;
        }

        // Implicit: no languages remain after filtering
        if (resourceFiles.Count == 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Filter resource files based on culture include/exclude settings
    /// </summary>
    public static List<Core.Models.ResourceFile> FilterResourceFiles(
        List<Core.Models.ResourceFile> files,
        Settings settings,
        out List<string> invalidCodes)
    {
        invalidCodes = new List<string>();
        var result = files;

        // Parse culture codes
        var includeCodes = ParseCultureCodes(settings.Cultures);
        var excludeCodes = ParseCultureCodes(settings.ExcludeCultures);

        // Validate culture codes and collect invalid ones
        var allLanguageCodes = files.Select(f => f.Language.Code.ToLowerInvariant()).ToList();
        if (files.Any(f => f.Language.IsDefault))
        {
            allLanguageCodes.Add("default");
        }

        foreach (var code in includeCodes.Concat(excludeCodes).Distinct())
        {
            if (code != "default" && !allLanguageCodes.Contains(code))
            {
                invalidCodes.Add(code);
            }
        }

        // Apply include filter (whitelist)
        if (includeCodes.Any())
        {
            result = result.Where(rf =>
                (includeCodes.Contains("default") && rf.Language.IsDefault) ||
                includeCodes.Contains(rf.Language.Code.ToLowerInvariant())
            ).ToList();
        }

        // Apply exclude filter (blacklist)
        if (excludeCodes.Any())
        {
            result = result.Where(rf =>
                !(excludeCodes.Contains("default") && rf.Language.IsDefault) &&
                !excludeCodes.Contains(rf.Language.Code.ToLowerInvariant())
            ).ToList();
        }

        return result;
    }

    /// <summary>
    /// Detect keys that exist in filtered resource files but not in the default file.
    /// This helps identify structural inconsistencies where translation files have extra keys.
    /// </summary>
    public static Dictionary<string, List<string>> DetectExtraKeysInFilteredFiles(
        Core.Models.ResourceFile defaultFile,
        List<Core.Models.ResourceFile> filteredResourceFiles)
    {
        var result = new Dictionary<string, List<string>>();

        // Get all keys from default file for fast lookup
        var defaultKeys = new HashSet<string>(defaultFile.Entries.Select(e => e.Key));

        // Check each filtered resource file (excluding default)
        foreach (var resourceFile in filteredResourceFiles.Where(rf => !rf.Language.IsDefault))
        {
            var extraKeys = resourceFile.Entries
                .Select(e => e.Key)
                .Where(key => !defaultKeys.Contains(key))
                .ToList();

            if (extraKeys.Any())
            {
                result[resourceFile.Language.Name] = extraKeys;
            }
        }

        return result;
    }

    /// <summary>
    /// Find keys matching the pattern based on search scope
    /// </summary>
    public static List<string> FindMatchingKeys(
        ResourceFile defaultFile,
        List<ResourceFile> resourceFiles,
        string pattern,
        SearchScope scope,
        bool isRegex,
        bool caseSensitive = false)
    {
        var matchedKeys = new HashSet<string>();

        if (isRegex)
        {
            var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1));
            return FindMatchingKeysWithRegex(defaultFile, resourceFiles, regex, scope);
        }

        // Exact match
        return FindMatchingKeysExact(defaultFile, resourceFiles, pattern, scope, caseSensitive);
    }

    /// <summary>
    /// Find keys matching exact pattern based on search scope
    /// </summary>
    private static List<string> FindMatchingKeysExact(
        ResourceFile defaultFile,
        List<ResourceFile> resourceFiles,
        string pattern,
        SearchScope scope,
        bool caseSensitive = false)
    {
        var matchedKeys = new HashSet<string>();
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Search in keys - find ALL matching entries (case variants and duplicates)
        if (scope == SearchScope.Keys || scope == SearchScope.Both || scope == SearchScope.All)
        {
            var entries = defaultFile.Entries.Where(e => e.Key.Equals(pattern, comparison));
            foreach (var entry in entries)
            {
                matchedKeys.Add(entry.Key);
            }
        }

        // Search in values
        if (scope == SearchScope.Values || scope == SearchScope.Both || scope == SearchScope.All)
        {
            // Get all keys from default to ensure we return valid keys
            var allKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();

            foreach (var resourceFile in resourceFiles)
            {
                foreach (var entry in resourceFile.Entries)
                {
                    // Only include keys that exist in default file
                    if (allKeys.Contains(entry.Key) && entry.Value != null && entry.Value.Equals(pattern, comparison))
                    {
                        matchedKeys.Add(entry.Key);
                    }
                }
            }
        }

        // Search in comments
        if (scope == SearchScope.Comments || scope == SearchScope.All)
        {
            // Get all keys from default to ensure we return valid keys
            var allKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();

            foreach (var resourceFile in resourceFiles)
            {
                foreach (var entry in resourceFile.Entries)
                {
                    // Only include keys that exist in default file
                    if (allKeys.Contains(entry.Key) && entry.Comment != null && entry.Comment.Equals(pattern, comparison))
                    {
                        matchedKeys.Add(entry.Key);
                    }
                }
            }
        }

        return matchedKeys.ToList();
    }

    /// <summary>
    /// Find keys matching regex pattern based on search scope
    /// </summary>
    private static List<string> FindMatchingKeysWithRegex(
        ResourceFile defaultFile,
        List<ResourceFile> resourceFiles,
        Regex regex,
        SearchScope scope)
    {
        var matchedKeys = new HashSet<string>();

        // Search in keys
        if (scope == SearchScope.Keys || scope == SearchScope.Both || scope == SearchScope.All)
        {
            foreach (var entry in defaultFile.Entries)
            {
                if (regex.IsMatch(entry.Key))
                {
                    matchedKeys.Add(entry.Key);
                }
            }
        }

        // Search in values
        if (scope == SearchScope.Values || scope == SearchScope.Both || scope == SearchScope.All)
        {
            // Get all keys from default to ensure we return valid keys
            var allKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();

            foreach (var resourceFile in resourceFiles)
            {
                foreach (var entry in resourceFile.Entries)
                {
                    // Only include keys that exist in default file
                    if (allKeys.Contains(entry.Key) && entry.Value != null && regex.IsMatch(entry.Value))
                    {
                        matchedKeys.Add(entry.Key);
                    }
                }
            }
        }

        // Search in comments
        if (scope == SearchScope.Comments || scope == SearchScope.All)
        {
            // Get all keys from default to ensure we return valid keys
            var allKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();

            foreach (var resourceFile in resourceFiles)
            {
                foreach (var entry in resourceFile.Entries)
                {
                    // Only include keys that exist in default file
                    if (allKeys.Contains(entry.Key) && entry.Comment != null && regex.IsMatch(entry.Comment))
                    {
                        matchedKeys.Add(entry.Key);
                    }
                }
            }
        }

        return matchedKeys.ToList();
    }

    /// <summary>
    /// Filter keys by translation status
    /// </summary>
    public static List<string> FilterByStatus(
        List<string> keys,
        ResourceFile defaultFile,
        List<ResourceFile> resourceFiles,
        TranslationStatus status)
    {
        var result = new List<string>();

        foreach (var key in keys)
        {
            bool includeKey = false;

            switch (status)
            {
                case TranslationStatus.Empty:
                    // Check if any NON-DEFAULT language has empty/whitespace value
                    includeKey = resourceFiles.Where(rf => !rf.Language.IsDefault).Any(rf =>
                    {
                        var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                        return entry == null || string.IsNullOrWhiteSpace(entry.Value);
                    });
                    break;

                case TranslationStatus.Missing:
                    // Check if key is missing in any NON-DEFAULT language file
                    includeKey = resourceFiles.Where(rf => !rf.Language.IsDefault).Any(rf =>
                        !rf.Entries.Any(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));
                    break;

                case TranslationStatus.Untranslated:
                    // Check if any language is missing, empty, or identical to default
                    var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                    var defaultValue = defaultEntry?.Value ?? "";

                    includeKey = resourceFiles.Where(rf => !rf.Language.IsDefault).Any(rf =>
                    {
                        var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                        // Missing, empty, or same as default
                        return entry == null ||
                               string.IsNullOrWhiteSpace(entry.Value) ||
                               entry.Value == defaultValue;
                    });
                    break;

                case TranslationStatus.Complete:
                    // Check if all languages have non-empty translations
                    // Special case: if no languages to check, can't be complete
                    if (resourceFiles.Count == 0)
                    {
                        includeKey = false;
                    }
                    else
                    {
                        includeKey = resourceFiles.All(rf =>
                        {
                            var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                            return entry != null && !string.IsNullOrWhiteSpace(entry.Value);
                        });
                    }
                    break;

                case TranslationStatus.Partial:
                    // Has some translations but not all (or some are empty)
                    // First verify default entry exists and is non-empty
                    var defaultPartialEntry = defaultFile.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (defaultPartialEntry == null || string.IsNullOrWhiteSpace(defaultPartialEntry.Value))
                    {
                        // Default is broken, don't report as partial (should be caught by validate)
                        includeKey = false;
                        break;
                    }

                    var hasAnyTranslation = false;
                    var hasAnyMissing = false;

                    foreach (var rf in resourceFiles.Where(rf => !rf.Language.IsDefault))
                    {
                        var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
                        {
                            hasAnyTranslation = true;
                        }
                        else
                        {
                            hasAnyMissing = true;
                        }
                    }

                    includeKey = hasAnyTranslation && hasAnyMissing;
                    break;
            }

            if (includeKey)
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// Apply exclusion patterns to filter out matching keys
    /// </summary>
    public static List<string> ApplyExclusions(
        List<string> keys,
        string[] notPatternsArray,
        bool caseSensitive = false)
    {
        if (notPatternsArray == null || notPatternsArray.Length == 0)
        {
            return keys;
        }

        // Flatten array and split by commas (supports both multiple --not flags and comma-separated)
        var patterns = notPatternsArray
            .SelectMany(p => p.Split(','))
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (!patterns.Any())
        {
            return keys;
        }

        // Pre-compile regex patterns once before iterating through keys
        var compiledPatterns = new List<(string pattern, Regex? regex, bool isWildcard)>();
        var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        foreach (var pattern in patterns)
        {
            if (IsWildcardPattern(pattern))
            {
                try
                {
                    var regexPattern = ConvertWildcardToRegex(pattern);
                    var regex = new Regex(regexPattern, regexOptions, TimeSpan.FromSeconds(1));
                    compiledPatterns.Add((pattern, regex, true));
                }
                catch (RegexParseException)
                {
                    // Skip invalid regex patterns
                    continue;
                }
            }
            else
            {
                // Exact match pattern
                compiledPatterns.Add((pattern, null, false));
            }
        }

        // Filter keys using pre-compiled patterns
        var result = new List<string>();
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var key in keys)
        {
            bool shouldExclude = false;

            foreach (var (pattern, regex, isWildcard) in compiledPatterns)
            {
                if (isWildcard && regex != null)
                {
                    try
                    {
                        if (regex.IsMatch(key))
                        {
                            shouldExclude = true;
                            break;
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        // Skip this pattern if it times out (catastrophic backtracking)
                        continue;
                    }
                }
                else
                {
                    // Exact match
                    if (key.Equals(pattern, comparison))
                    {
                        shouldExclude = true;
                        break;
                    }
                }
            }

            if (!shouldExclude)
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a key that may include occurrence syntax like "Key [2]"
    /// </summary>
    public static (string key, int? occurrence) ParseKeyWithOccurrence(string input)
    {
        if (string.IsNullOrEmpty(input))
            return (input, null);

        // Match pattern like "KeyName [2]" at the end
        var match = Regex.Match(input, @"^(.+?)\s*\[(\d+)\]$");
        if (match.Success)
        {
            var key = match.Groups[1].Value.Trim();
            var occurrence = int.Parse(match.Groups[2].Value);
            return (key, occurrence);
        }

        return (input, null);
    }

    /// <summary>
    /// Convert matched keys to KeyOccurrences, handling both case variants and true duplicates
    /// </summary>
    public static List<KeyOccurrence> ConvertToOccurrences(
        List<string> matchedKeys,
        ResourceFile defaultFile,
        bool caseSensitive)
    {
        var occurrences = new List<KeyOccurrence>();

        // Group keys that match case-insensitively (to find case variants)
        var keyGroups = caseSensitive
            ? matchedKeys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.ToList())
            : matchedKeys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                         .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var matchedKey in matchedKeys)
        {
            // Count true duplicates (exact same key appearing multiple times in file)
            var exactDuplicateCount = defaultFile.Entries.Count(e => e.Key == matchedKey);

            if (exactDuplicateCount > 1)
            {
                // Add each occurrence with [N] suffix
                for (int i = 1; i <= exactDuplicateCount; i++)
                {
                    occurrences.Add(new KeyOccurrence
                    {
                        Key = matchedKey,
                        Occurrence = i,
                        TotalOccurrences = exactDuplicateCount
                    });
                }
            }
            else
            {
                // Single occurrence
                occurrences.Add(new KeyOccurrence
                {
                    Key = matchedKey,
                    Occurrence = 1,
                    TotalOccurrences = 1
                });
            }
        }

        return occurrences;
    }

    /// <summary>
    /// Get the Nth occurrence of a key from a resource file (1-based index)
    /// </summary>
    public static ResourceEntry? GetNthOccurrence(ResourceFile resourceFile, string key, int occurrence)
    {
        var entries = resourceFile.Entries
            .Where(e => e.Key == key)
            .ToList();

        if (occurrence < 1 || occurrence > entries.Count)
            return null;

        return entries[occurrence - 1];
    }

    /// <summary>
    /// Get all entries for a key (case-insensitive), handling case variants
    /// </summary>
    public static List<ResourceEntry> GetAllCaseVariants(ResourceFile resourceFile, string key)
    {
        return resourceFile.Entries
            .Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
