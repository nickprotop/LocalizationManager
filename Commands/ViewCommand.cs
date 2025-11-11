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
using LocalizationManager.Core.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace LocalizationManager.Commands;

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
            var resourceFiles = new List<Core.Models.ResourceFile>();

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

            // Auto-detect and convert wildcard patterns to regex
            if (!settings.UseRegex && IsWildcardPattern(settings.Key))
            {
                settings.Key = ConvertWildcardToRegex(settings.Key);
                settings.UseRegex = true;
                usedWildcards = true;
            }

            if (settings.UseRegex)
            {
                try
                {
                    var regex = new Regex(settings.Key, RegexOptions.None, TimeSpan.FromSeconds(1));
                    matchedKeys = defaultFile.Entries
                        .Where(e => regex.IsMatch(e.Key))
                        .Select(e => e.Key)
                        .ToList();
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
            }
            else
            {
                // Exact match (backward compatible)
                var existingEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == settings.Key);
                if (existingEntry == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Key '{Markup.Escape(settings.Key)}' not found![/]");
                    return 1;
                }
                matchedKeys = new List<string> { settings.Key };
            }

            // Apply sorting if requested
            if (settings.Sort)
            {
                matchedKeys = matchedKeys.OrderBy(k => k).ToList();
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

            // Check if we have any matches
            if (matchedKeys.Count == 0)
            {
                var patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "pattern" : "key");
                var displayPattern = usedWildcards ? originalPattern : settings.Key;
                // Escape pattern to prevent Spectre.Console markup interpretation
                AnsiConsole.MarkupLine($"[red]✗ No keys match {patternType} '{Markup.Escape(displayPattern)}'[/]");
                return 1;
            }

            // Display based on format
            switch (format)
            {
                case OutputFormat.Json:
                    DisplayJson(matchedKeys, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
                    break;
                case OutputFormat.Simple:
                    DisplaySimple(matchedKeys, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
                    break;
                case OutputFormat.Table:
                default:
                    DisplayTable(matchedKeys, resourceFiles, settings.ShowComments, settings, usedWildcards, originalPattern);
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

    private void DisplayTable(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        DisplayConfigNotice(settings);

        if (keys.Count == 1)
        {
            // Single key - use backward compatible format
            DisplaySingleKeyTable(keys[0], resourceFiles, showComments);
        }
        else
        {
            // Multiple keys - new grouped format
            DisplayMultipleKeysTable(keys, resourceFiles, showComments, settings, usedWildcards, originalPattern);
        }
    }

    private void DisplaySingleKeyTable(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        AnsiConsole.MarkupLine($"[yellow]Key:[/] [bold]{Markup.Escape(key)}[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Language");
        table.AddColumn("Value");
        if (showComments)
        {
            table.AddColumn("Comment");
        }

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
            if (entry != null)
            {
                var langName = rf.Language.IsDefault
                    ? $"{rf.Language.Name} [yellow](default)[/]"
                    : rf.Language.Name;

                var value = entry.IsEmpty
                    ? "[dim](empty)[/]"
                    : entry.Value;

                if (showComments)
                {
                    var comment = string.IsNullOrWhiteSpace(entry.Comment)
                        ? "[dim](no comment)[/]"
                        : entry.Comment;
                    table.AddRow(langName, value ?? "", comment);
                }
                else
                {
                    table.AddRow(langName, value ?? "");
                }
            }
            else
            {
                var langName = rf.Language.IsDefault
                    ? $"{rf.Language.Name} [yellow](default)[/]"
                    : rf.Language.Name;

                if (showComments)
                {
                    table.AddRow(langName, "[red](missing)[/]", "[dim](no comment)[/]");
                }
                else
                {
                    table.AddRow(langName, "[red](missing)[/]");
                }
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Show statistics
        var total = resourceFiles.Count;
        var present = resourceFiles.Count(rf => rf.Entries.Any(e => e.Key == key));
        var empty = resourceFiles.Count(rf => rf.Entries.Any(e => e.Key == key && e.IsEmpty));

        AnsiConsole.MarkupLine($"[dim]Present in {present}/{total} language(s), {empty} empty value(s)[/]");
    }

    private void DisplayMultipleKeysTable(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        string patternDisplay;
        if (usedWildcards)
        {
            // Escape pattern to prevent Spectre.Console markup interpretation
            patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](wildcard)[/]";
        }
        else if (settings.UseRegex)
        {
            // Escape pattern to prevent Spectre.Console markup interpretation
            patternDisplay = $"Pattern: {Markup.Escape(originalPattern)} [dim](regex)[/]";
        }
        else
        {
            patternDisplay = $"Keys: {keys.Count}";
        }

        AnsiConsole.MarkupLine($"[yellow]{patternDisplay}[/]");
        AnsiConsole.MarkupLine($"[dim]Matched {keys.Count} key(s)[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Language");
        table.AddColumn("Value");
        if (showComments)
        {
            table.AddColumn("Comment");
        }

        foreach (var key in keys)
        {
            bool firstRowForKey = true;
            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                var langName = rf.Language.IsDefault
                    ? $"{rf.Language.Name} [yellow](default)[/]"
                    : rf.Language.Name;

                var keyDisplay = firstRowForKey ? key : "";
                var value = entry?.IsEmpty == true ? "[dim](empty)[/]" : entry?.Value ?? "[red](missing)[/]";

                if (showComments)
                {
                    var comment = entry == null || string.IsNullOrWhiteSpace(entry.Comment)
                        ? "[dim](no comment)[/]"
                        : entry.Comment;
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
        AnsiConsole.MarkupLine($"[dim]Showing {keys.Count} key(s) across {resourceFiles.Count} language(s)[/]");
    }

    private void DisplayJson(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        if (keys.Count == 1)
        {
            // Single key - backward compatible format
            DisplaySingleKeyJson(keys[0], resourceFiles, showComments);
        }
        else
        {
            // Multiple keys - array format
            DisplayMultipleKeysJson(keys, resourceFiles, showComments, settings, usedWildcards, originalPattern);
        }
    }

    private void DisplaySingleKeyJson(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        var translations = new Dictionary<string, object?>();

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
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

        var output = new
        {
            key = key,
            translations = translations
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplayMultipleKeysJson(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        var keyObjects = new List<object>();

        foreach (var key in keys)
        {
            var translations = new Dictionary<string, object?>();
            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
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
                key = key,
                translations = translations
            });
        }

        var output = new
        {
            pattern = settings.UseRegex || usedWildcards ? originalPattern : (string?)null,
            patternType = usedWildcards ? "wildcard" : (settings.UseRegex ? "regex" : (string?)null),
            matchCount = keys.Count,
            keys = keyObjects
        };

        Console.WriteLine(OutputFormatter.FormatJson(output));
    }

    private void DisplaySimple(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
    {
        if (keys.Count == 1)
        {
            // Single key - backward compatible format
            DisplaySingleKeySimple(keys[0], resourceFiles, showComments);
        }
        else
        {
            // Multiple keys
            DisplayMultipleKeysSimple(keys, resourceFiles, showComments, settings, usedWildcards, originalPattern);
        }
    }

    private void DisplaySingleKeySimple(string key, List<Core.Models.ResourceFile> resourceFiles, bool showComments)
    {
        Console.WriteLine($"Key: {key}");
        Console.WriteLine();

        foreach (var rf in resourceFiles)
        {
            var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
            var langLabel = rf.Language.IsDefault ? $"{rf.Language.Name} (default)" : rf.Language.Name;
            var value = entry?.Value ?? "(missing)";

            Console.WriteLine($"{langLabel}: {value}");

            if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
            {
                Console.WriteLine($"  Comment: {entry.Comment}");
            }
        }
    }

    private void DisplayMultipleKeysSimple(List<string> keys, List<Core.Models.ResourceFile> resourceFiles, bool showComments, Settings settings, bool usedWildcards, string originalPattern)
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
            patternDisplay = $"Keys: {keys.Count}";
        }

        Console.WriteLine(patternDisplay);
        Console.WriteLine($"Matched {keys.Count} key(s)");
        Console.WriteLine();

        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            Console.WriteLine($"--- {key} ---");

            foreach (var rf in resourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key == key);
                var langLabel = rf.Language.IsDefault
                    ? $"{rf.Language.Name} (default)"
                    : rf.Language.Name;
                var value = entry?.Value ?? "(missing)";

                Console.WriteLine($"{langLabel}: {value}");

                if (showComments && entry != null && !string.IsNullOrWhiteSpace(entry.Comment))
                {
                    Console.WriteLine($"  Comment: {entry.Comment}");
                }
            }

            // Add blank line between keys except after last one
            if (i < keys.Count - 1)
            {
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Detects if a pattern contains wildcard characters (* or ?) that should be converted to regex.
    /// Handles backslash escaping for literal wildcard characters.
    /// </summary>
    internal static bool IsWildcardPattern(string pattern)
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
    internal static string ConvertWildcardToRegex(string wildcardPattern)
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
}
