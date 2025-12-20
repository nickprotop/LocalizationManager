// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Parses iOS .strings file format.
/// Format: /* Comment */ "key" = "value";
/// </summary>
public partial class StringsFileParser
{
    /// <summary>
    /// Represents a parsed entry from a .strings file.
    /// </summary>
    public record StringsEntry(string Key, string Value, string? Comment);

    /// <summary>
    /// Parses a .strings file content into a list of entries.
    /// </summary>
    public List<StringsEntry> Parse(string content)
    {
        var results = new List<StringsEntry>();
        string? pendingComment = null;

        // Normalize line endings
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = content.Split('\n');

        var inBlockComment = false;
        var blockCommentBuilder = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Handle block comment continuation
            if (inBlockComment)
            {
                var endIndex = line.IndexOf("*/", StringComparison.Ordinal);
                if (endIndex >= 0)
                {
                    blockCommentBuilder.Append(' ');
                    blockCommentBuilder.Append(line.Substring(0, endIndex).Trim());
                    pendingComment = blockCommentBuilder.ToString().Trim();
                    blockCommentBuilder.Clear();
                    inBlockComment = false;

                    // Check for content after comment end on same line
                    var remainder = line.Substring(endIndex + 2).Trim();
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        var entry = TryParseKeyValue(remainder);
                        if (entry != null)
                        {
                            results.Add(entry with { Comment = pendingComment });
                            pendingComment = null;
                        }
                    }
                }
                else
                {
                    blockCommentBuilder.Append(' ');
                    blockCommentBuilder.Append(line);
                }
                continue;
            }

            // Skip empty lines
            if (string.IsNullOrEmpty(line))
                continue;

            // Block comment start
            if (line.StartsWith("/*"))
            {
                var endIndex = line.IndexOf("*/", 2, StringComparison.Ordinal);
                if (endIndex >= 0)
                {
                    // Single-line block comment
                    pendingComment = line.Substring(2, endIndex - 2).Trim();

                    // Check for content after comment on same line
                    var remainder = line.Substring(endIndex + 2).Trim();
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        var entry = TryParseKeyValue(remainder);
                        if (entry != null)
                        {
                            results.Add(entry with { Comment = pendingComment });
                            pendingComment = null;
                        }
                    }
                }
                else
                {
                    // Multi-line block comment
                    inBlockComment = true;
                    blockCommentBuilder.Clear();
                    blockCommentBuilder.Append(line.Substring(2).Trim());
                }
                continue;
            }

            // Single-line comment
            if (line.StartsWith("//"))
            {
                pendingComment = line.Substring(2).Trim();
                continue;
            }

            // Key-value pair
            var parsedEntry = TryParseKeyValue(line);
            if (parsedEntry != null)
            {
                results.Add(parsedEntry with { Comment = pendingComment });
                pendingComment = null;
            }
        }

        return results;
    }

    /// <summary>
    /// Tries to parse a key-value pair from a line.
    /// Format: "key" = "value";
    /// </summary>
    private StringsEntry? TryParseKeyValue(string line)
    {
        var match = KeyValuePattern().Match(line);
        if (!match.Success)
            return null;

        var key = UnescapeString(match.Groups[1].Value);
        var value = UnescapeString(match.Groups[2].Value);

        return new StringsEntry(key, value, null);
    }

    /// <summary>
    /// Serializes entries back to .strings format.
    /// </summary>
    public string Serialize(IEnumerable<StringsEntry> entries)
    {
        var sb = new StringBuilder();

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Comment))
            {
                sb.AppendLine($"/* {entry.Comment} */");
            }
            sb.AppendLine($"\"{EscapeString(entry.Key)}\" = \"{EscapeString(entry.Value)}\";");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Unescapes a string from .strings format.
    /// </summary>
    public static string UnescapeString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var sb = new StringBuilder(s.Length);
        var i = 0;

        while (i < s.Length)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                switch (s[i + 1])
                {
                    case 'n':
                        sb.Append('\n');
                        i += 2;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i += 2;
                        break;
                    case 't':
                        sb.Append('\t');
                        i += 2;
                        break;
                    case '"':
                        sb.Append('"');
                        i += 2;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i += 2;
                        break;
                    case 'U' when i + 5 < s.Length:
                        // Unicode escape \Uxxxx
                        if (int.TryParse(s.AsSpan(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                        {
                            sb.Append((char)codePoint);
                            i += 6;
                        }
                        else
                        {
                            sb.Append(s[i]);
                            i++;
                        }
                        break;
                    default:
                        sb.Append(s[i]);
                        i++;
                        break;
                }
            }
            else
            {
                sb.Append(s[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes a string for .strings format.
    /// </summary>
    public static string EscapeString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // Pattern: "key" = "value"; with optional whitespace and semicolon
    [GeneratedRegex(@"^\s*""(.+?)""\s*=\s*""(.*)""\s*;?\s*$", RegexOptions.Singleline)]
    private static partial Regex KeyValuePattern();
}
