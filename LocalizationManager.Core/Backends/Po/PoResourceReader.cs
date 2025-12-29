// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Text.RegularExpressions;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// Reads and parses GNU gettext PO files into ResourceFile objects.
/// Supports plurals, context (msgctxt), fuzzy flags, and all comment types.
/// </summary>
public class PoResourceReader : IResourceReader
{
    private readonly PoFormatConfiguration _config;

    // Regex patterns for parsing PO entries
    private static readonly Regex MsgIdPattern = new(@"^msgid\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex MsgIdPluralPattern = new(@"^msgid_plural\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex MsgStrPattern = new(@"^msgstr\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex MsgStrPluralPattern = new(@"^msgstr\[(\d+)\]\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex MsgCtxtPattern = new(@"^msgctxt\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex StringContinuationPattern = new(@"^""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex PreviousMsgIdPattern = new(@"^#\|\s+msgid\s+""(.*)""$", RegexOptions.Compiled);
    private static readonly Regex PreviousMsgCtxtPattern = new(@"^#\|\s+msgctxt\s+""(.*)""$", RegexOptions.Compiled);

    public PoResourceReader(PoFormatConfiguration? config = null)
    {
        _config = config ?? new PoFormatConfiguration();
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("FilePath is required for file-based reading", nameof(language));

        // First, detect encoding from header
        var encoding = await DetectEncodingAsync(language.FilePath, ct);

        using var stream = new FileStream(language.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, encoding);
        return await ReadAsyncCore(reader, language, ct);
    }

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        return ReadAsync(language).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
    {
        return await ReadAsyncCore(reader, metadata, ct);
    }

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
    {
        return ReadAsync(reader, metadata).GetAwaiter().GetResult();
    }

    private async Task<ResourceFile> ReadAsyncCore(TextReader reader, LanguageInfo metadata, CancellationToken ct)
    {
        var entries = new List<ResourceEntry>();
        PoHeader? header = null;
        var poEntries = await ParseEntriesAsync(reader, ct);

        foreach (var poEntry in poEntries)
        {
            ct.ThrowIfCancellationRequested();

            // Skip obsolete entries
            if (poEntry.IsObsolete)
                continue;

            // Parse header entry
            if (poEntry.IsHeader)
            {
                header = PoHeader.Parse(poEntry.MsgStr ?? "");
                continue;
            }

            // Convert to ResourceEntry
            var resourceEntry = ConvertToResourceEntry(poEntry, metadata.Code);
            entries.Add(resourceEntry);
        }

        return new ResourceFile
        {
            Language = metadata,
            Entries = entries
        };
    }

    private async Task<List<PoEntry>> ParseEntriesAsync(TextReader reader, CancellationToken ct)
    {
        var entries = new List<PoEntry>();
        var currentEntry = new PoEntry();
        var lineNumber = 0;
        var currentField = PoField.None;
        var isObsolete = false;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            // Handle obsolete entries (#~)
            if (line.StartsWith("#~"))
            {
                isObsolete = true;
                line = line.Substring(2).TrimStart();
            }

            // Empty line marks end of entry
            if (string.IsNullOrWhiteSpace(line))
            {
                if (HasContent(currentEntry))
                {
                    currentEntry.IsObsolete = isObsolete;
                    entries.Add(currentEntry);
                    currentEntry = new PoEntry();
                    isObsolete = false;
                }
                currentField = PoField.None;
                continue;
            }

            // Comments
            if (line.StartsWith("#"))
            {
                ParseComment(line, currentEntry);
                continue;
            }

            // msgctxt
            var match = MsgCtxtPattern.Match(line);
            if (match.Success)
            {
                currentEntry.LineNumber = lineNumber;
                currentEntry.Context = UnescapeString(match.Groups[1].Value);
                currentField = PoField.MsgCtxt;
                continue;
            }

            // msgid
            match = MsgIdPattern.Match(line);
            if (match.Success)
            {
                if (currentEntry.LineNumber == 0)
                    currentEntry.LineNumber = lineNumber;
                currentEntry.MsgId = UnescapeString(match.Groups[1].Value);
                currentField = PoField.MsgId;
                continue;
            }

            // msgid_plural
            match = MsgIdPluralPattern.Match(line);
            if (match.Success)
            {
                currentEntry.MsgIdPlural = UnescapeString(match.Groups[1].Value);
                currentField = PoField.MsgIdPlural;
                continue;
            }

            // msgstr (non-plural)
            match = MsgStrPattern.Match(line);
            if (match.Success)
            {
                currentEntry.MsgStr = UnescapeString(match.Groups[1].Value);
                currentField = PoField.MsgStr;
                continue;
            }

            // msgstr[n] (plural)
            match = MsgStrPluralPattern.Match(line);
            if (match.Success)
            {
                var index = int.Parse(match.Groups[1].Value);
                currentEntry.MsgStrPlural ??= new Dictionary<int, string>();
                currentEntry.MsgStrPlural[index] = UnescapeString(match.Groups[2].Value);
                currentField = PoField.MsgStrPlural;
                continue;
            }

            // String continuation
            match = StringContinuationPattern.Match(line);
            if (match.Success)
            {
                var value = UnescapeString(match.Groups[1].Value);
                AppendToCurrent(currentEntry, currentField, value);
                continue;
            }
        }

        // Don't forget last entry
        if (HasContent(currentEntry))
        {
            currentEntry.IsObsolete = isObsolete;
            entries.Add(currentEntry);
        }

        return entries;
    }

    private void ParseComment(string line, PoEntry entry)
    {
        if (line.StartsWith("#:"))
        {
            // Reference comment
            entry.References ??= new List<string>();
            entry.References.Add(line.Substring(2).Trim());
        }
        else if (line.StartsWith("#."))
        {
            // Extracted comment
            var comment = line.Substring(2).Trim();
            entry.ExtractedComment = string.IsNullOrEmpty(entry.ExtractedComment)
                ? comment
                : entry.ExtractedComment + "\n" + comment;
        }
        else if (line.StartsWith("#,"))
        {
            // Flags
            entry.Flags ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var flags = line.Substring(2).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var flag in flags)
                entry.Flags.Add(flag);
        }
        else if (line.StartsWith("#|"))
        {
            // Previous msgid/msgctxt (for fuzzy)
            var match = PreviousMsgIdPattern.Match(line);
            if (match.Success)
            {
                entry.PreviousMsgId = UnescapeString(match.Groups[1].Value);
            }
            else
            {
                match = PreviousMsgCtxtPattern.Match(line);
                if (match.Success)
                    entry.PreviousMsgCtxt = UnescapeString(match.Groups[1].Value);
            }
        }
        else if (line.StartsWith("# ") || line == "#")
        {
            // Translator comment
            var comment = line.Length > 2 ? line.Substring(2) : "";
            entry.TranslatorComment = string.IsNullOrEmpty(entry.TranslatorComment)
                ? comment
                : entry.TranslatorComment + "\n" + comment;
        }
    }

    private void AppendToCurrent(PoEntry entry, PoField field, string value)
    {
        switch (field)
        {
            case PoField.MsgCtxt:
                entry.Context += value;
                break;
            case PoField.MsgId:
                entry.MsgId += value;
                break;
            case PoField.MsgIdPlural:
                entry.MsgIdPlural += value;
                break;
            case PoField.MsgStr:
                entry.MsgStr += value;
                break;
            case PoField.MsgStrPlural:
                // For plural, append to the last added index
                if (entry.MsgStrPlural != null && entry.MsgStrPlural.Count > 0)
                {
                    var lastIndex = entry.MsgStrPlural.Keys.Max();
                    entry.MsgStrPlural[lastIndex] += value;
                }
                break;
        }
    }

    private ResourceEntry ConvertToResourceEntry(PoEntry poEntry, string languageCode)
    {
        var key = poEntry.GenerateKey(_config.KeyStrategy);

        if (poEntry.IsPlural && poEntry.MsgStrPlural != null)
        {
            // Convert plural indices to CLDR categories
            var pluralForms = new Dictionary<string, string>();
            foreach (var (index, value) in poEntry.MsgStrPlural)
            {
                var category = PoPluralMapper.IndexToCategory(languageCode, index);
                pluralForms[category] = value;
            }

            return new ResourceEntry
            {
                Key = key,
                Value = pluralForms.GetValueOrDefault("other") ?? pluralForms.Values.FirstOrDefault(),
                Comment = poEntry.GetCombinedComment(),
                IsPlural = true,
                PluralForms = pluralForms,
                // Store msgid_plural for translation (source plural text)
                SourcePluralText = poEntry.MsgIdPlural
            };
        }

        return new ResourceEntry
        {
            Key = key,
            Value = poEntry.MsgStr,
            Comment = poEntry.GetCombinedComment()
        };
    }

    private static bool HasContent(PoEntry entry)
    {
        return !string.IsNullOrEmpty(entry.MsgId) ||
               !string.IsNullOrEmpty(entry.MsgStr) ||
               entry.MsgStrPlural?.Count > 0;
    }

    /// <summary>
    /// Unescapes a PO string value (handles \n, \t, \\, \", octal, hex).
    /// </summary>
    public static string UnescapeString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                switch (next)
                {
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case 'x' when i + 3 < value.Length:
                        // Hex escape \xNN
                        if (TryParseHex(value.Substring(i + 2, 2), out var hexValue))
                        {
                            sb.Append((char)hexValue);
                            i += 3;
                        }
                        else
                        {
                            sb.Append(value[i]);
                        }
                        break;
                    case >= '0' and <= '7':
                        // Octal escape \ooo
                        var octalLength = 1;
                        while (octalLength < 3 && i + 1 + octalLength < value.Length &&
                               value[i + 1 + octalLength] >= '0' && value[i + 1 + octalLength] <= '7')
                        {
                            octalLength++;
                        }
                        if (TryParseOctal(value.Substring(i + 1, octalLength), out var octalValue))
                        {
                            sb.Append((char)octalValue);
                            i += octalLength;
                        }
                        else
                        {
                            sb.Append(value[i]);
                        }
                        break;
                    default:
                        sb.Append(value[i]);
                        break;
                }
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }

    private static bool TryParseHex(string hex, out int value)
    {
        return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    private static bool TryParseOctal(string octal, out int value)
    {
        value = 0;
        foreach (var c in octal)
        {
            if (c < '0' || c > '7')
                return false;
            value = value * 8 + (c - '0');
        }
        return true;
    }

    /// <summary>
    /// Detects the encoding of a PO file by reading the header.
    /// </summary>
    private async Task<Encoding> DetectEncodingAsync(string filePath, CancellationToken ct)
    {
        // First read with default encoding to get the header
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Read first few lines to find Content-Type header
            var lineCount = 0;
            var headerContent = new StringBuilder();
            var inHeader = false;

            while (lineCount < 50) // Header is typically in first 50 lines
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                lineCount++;

                if (line.StartsWith("msgid \"\""))
                {
                    inHeader = true;
                }
                else if (inHeader)
                {
                    if (line.StartsWith("msgstr"))
                    {
                        headerContent.Append(line.Substring(line.IndexOf('"') + 1).TrimEnd('"'));
                    }
                    else if (line.StartsWith("\""))
                    {
                        headerContent.Append(line.Trim('"'));
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        break;
                    }
                }
            }

            var header = PoHeader.Parse(UnescapeString(headerContent.ToString()));

            return header.Charset.ToUpperInvariant() switch
            {
                "UTF-8" or "UTF8" => Encoding.UTF8,
                "UTF-16" or "UTF16" => Encoding.Unicode,
                "ISO-8859-1" or "LATIN1" or "LATIN-1" => Encoding.Latin1,
                "WINDOWS-1252" or "CP1252" => Encoding.GetEncoding(1252),
                _ => Encoding.UTF8 // Default fallback
            };
        }
        catch
        {
            // If we can't detect, use UTF-8
            return Encoding.UTF8;
        }
    }

    private enum PoField
    {
        None,
        MsgCtxt,
        MsgId,
        MsgIdPlural,
        MsgStr,
        MsgStrPlural
    }
}
