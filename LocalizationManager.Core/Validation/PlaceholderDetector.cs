// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Validation;

/// <summary>
/// Detects and extracts placeholders from localization strings.
/// Supports .NET format strings, printf-style, ICU MessageFormat, and template literals.
/// </summary>
public static class PlaceholderDetector
{
    // .NET format strings: {0}, {1}, {name}, {0:N2}, {count:D3}
    // Uses negative lookbehind to exclude template literals ${...}
    private static readonly Regex DotNetFormatRegex = new(
        @"(?<!\$)\{(?<index>\d+)(?::(?<format>[^}]+))?\}|(?<!\$)\{(?<name>[a-zA-Z_]\w*)(?::(?<format>[^}]+))?\}",
        RegexOptions.Compiled);

    // Printf-style: %s, %d, %f, %1$s, %2$d
    private static readonly Regex PrintfStyleRegex = new(
        @"%(?:(?<position>\d+)\$)?(?<flags>[-+0 #]*)(?<width>\d+)?(?:\.(?<precision>\d+))?(?<type>[sdifFeEgGxXocpn%])",
        RegexOptions.Compiled);

    // ICU MessageFormat: {count, plural, one {# item} other {# items}}
    // Only matches ICU-specific syntax (requires comma after name)
    private static readonly Regex IcuMessageFormatRegex = new(
        @"\{(?<name>\w+),\s*(?<type>plural|select|selectordinal)(?:,\s*(?<format>[^}]+))?\}",
        RegexOptions.Compiled);

    // Template literals: ${var}, ${user.name}
    private static readonly Regex TemplateLiteralRegex = new(
        @"\$\{(?<name>[a-zA-Z_][\w.]*)\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Detects all placeholders in a string.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>List of detected placeholders.</returns>
    public static List<Placeholder> DetectPlaceholders(string? text)
    {
        return DetectPlaceholders(text, PlaceholderType.All);
    }

    /// <summary>
    /// Detects placeholders of specific types in a string.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="enabledTypes">The placeholder types to detect.</param>
    /// <returns>List of detected placeholders.</returns>
    public static List<Placeholder> DetectPlaceholders(string? text, PlaceholderType enabledTypes)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<Placeholder>();
        }

        var placeholders = new List<Placeholder>();

        // Detect .NET format strings
        if (enabledTypes.HasFlag(PlaceholderType.DotNetFormat))
        {
            foreach (Match match in DotNetFormatRegex.Matches(text))
            {
                var index = match.Groups["index"].Success ? match.Groups["index"].Value : null;
                var name = match.Groups["name"].Success ? match.Groups["name"].Value : null;
                var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                placeholders.Add(new Placeholder
                {
                    Type = PlaceholderType.DotNetFormat,
                    Original = match.Value,
                    Index = index,
                    Name = name,
                    Format = format,
                    Position = match.Index
                });
            }
        }

        // Detect printf-style placeholders
        if (enabledTypes.HasFlag(PlaceholderType.PrintfStyle))
        {
            foreach (Match match in PrintfStyleRegex.Matches(text))
            {
                // Skip escaped %%
                if (match.Groups["type"].Value == "%")
                {
                    continue;
                }

                placeholders.Add(new Placeholder
                {
                    Type = PlaceholderType.PrintfStyle,
                    Original = match.Value,
                    Index = match.Groups["position"].Success ? match.Groups["position"].Value : null,
                    Format = match.Groups["type"].Value,
                    Position = match.Index
                });
            }
        }

        // Detect ICU MessageFormat placeholders
        if (enabledTypes.HasFlag(PlaceholderType.IcuMessageFormat))
        {
            foreach (Match match in IcuMessageFormatRegex.Matches(text))
            {
                placeholders.Add(new Placeholder
                {
                    Type = PlaceholderType.IcuMessageFormat,
                    Original = match.Value,
                    Name = match.Groups["name"].Value,
                    Format = match.Groups["type"].Success ? match.Groups["type"].Value : null,
                    Position = match.Index
                });
            }
        }

        // Detect template literals
        if (enabledTypes.HasFlag(PlaceholderType.TemplateLiteral))
        {
            foreach (Match match in TemplateLiteralRegex.Matches(text))
            {
                placeholders.Add(new Placeholder
                {
                    Type = PlaceholderType.TemplateLiteral,
                    Original = match.Value,
                    Name = match.Groups["name"].Value,
                    Position = match.Index
                });
            }
        }

        return placeholders.OrderBy(p => p.Position).ToList();
    }

    /// <summary>
    /// Gets a normalized identifier for a placeholder for comparison purposes.
    /// </summary>
    /// <param name="placeholder">The placeholder to normalize.</param>
    /// <returns>A normalized string identifier.</returns>
    public static string GetNormalizedIdentifier(Placeholder placeholder)
    {
        return placeholder.Type switch
        {
            PlaceholderType.DotNetFormat => placeholder.Index ?? placeholder.Name ?? "",
            PlaceholderType.PrintfStyle => placeholder.Index ?? placeholder.Format ?? "",
            PlaceholderType.IcuMessageFormat => placeholder.Name ?? "",
            PlaceholderType.TemplateLiteral => placeholder.Name ?? "",
            _ => placeholder.Original
        };
    }
}

/// <summary>
/// Represents a detected placeholder in a string.
/// </summary>
public class Placeholder
{
    /// <summary>
    /// The type of placeholder.
    /// </summary>
    public PlaceholderType Type { get; set; }

    /// <summary>
    /// The original placeholder text as it appears in the string.
    /// </summary>
    public required string Original { get; set; }

    /// <summary>
    /// The index (for positional placeholders like {0} or %1$s).
    /// </summary>
    public string? Index { get; set; }

    /// <summary>
    /// The name (for named placeholders like {name} or ${user}).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The format specifier (like N2 in {0:N2} or d in %d).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The position in the original string.
    /// </summary>
    public int Position { get; set; }

    public override string ToString() => Original;
}

/// <summary>
/// Types of placeholders that can be detected.
/// </summary>
[Flags]
public enum PlaceholderType
{
    /// <summary>
    /// No placeholder types.
    /// </summary>
    None = 0,

    /// <summary>
    /// .NET format strings: {0}, {name}, {0:N2}
    /// </summary>
    DotNetFormat = 1,

    /// <summary>
    /// Printf-style: %s, %d, %1$s
    /// </summary>
    PrintfStyle = 2,

    /// <summary>
    /// ICU MessageFormat: {count, plural, one {# item} other {# items}}
    /// </summary>
    IcuMessageFormat = 4,

    /// <summary>
    /// Template literals: ${var}, ${user.name}
    /// </summary>
    TemplateLiteral = 8,

    /// <summary>
    /// All placeholder types.
    /// </summary>
    All = DotNetFormat | PrintfStyle | IcuMessageFormat | TemplateLiteral
}
