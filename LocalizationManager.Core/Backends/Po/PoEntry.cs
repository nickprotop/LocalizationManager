// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// Internal representation of a PO file entry during parsing.
/// Contains all the raw PO-specific metadata that may not map directly to ResourceEntry.
/// </summary>
internal class PoEntry
{
    /// <summary>
    /// Message context (msgctxt) - used to disambiguate identical source strings.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Source string (msgid) - the original untranslated text.
    /// </summary>
    public string MsgId { get; set; } = string.Empty;

    /// <summary>
    /// Plural source string (msgid_plural) - for plural entries.
    /// </summary>
    public string? MsgIdPlural { get; set; }

    /// <summary>
    /// Translation (msgstr) - for non-plural entries.
    /// </summary>
    public string? MsgStr { get; set; }

    /// <summary>
    /// Plural translations (msgstr[n]) - for plural entries.
    /// Key is the index (0, 1, 2, ...).
    /// </summary>
    public Dictionary<int, string>? MsgStrPlural { get; set; }

    /// <summary>
    /// Translator comment (# comment).
    /// </summary>
    public string? TranslatorComment { get; set; }

    /// <summary>
    /// Extracted comment (#. comment) - from source code.
    /// </summary>
    public string? ExtractedComment { get; set; }

    /// <summary>
    /// Reference locations (#: file:line).
    /// </summary>
    public List<string>? References { get; set; }

    /// <summary>
    /// Flags (#, flag1, flag2) - e.g., fuzzy, c-format.
    /// </summary>
    public HashSet<string>? Flags { get; set; }

    /// <summary>
    /// Previous msgid for fuzzy entries (#| msgid "...").
    /// </summary>
    public string? PreviousMsgId { get; set; }

    /// <summary>
    /// Previous msgctxt for fuzzy entries (#| msgctxt "...").
    /// </summary>
    public string? PreviousMsgCtxt { get; set; }

    /// <summary>
    /// Line number in the source file (for error reporting).
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Whether this entry is marked as fuzzy (needs review).
    /// </summary>
    public bool IsFuzzy => Flags?.Contains("fuzzy") == true;

    /// <summary>
    /// Whether this is a plural entry.
    /// </summary>
    public bool IsPlural => MsgIdPlural != null || MsgStrPlural?.Count > 0;

    /// <summary>
    /// Whether this is the header entry (empty msgid).
    /// </summary>
    public bool IsHeader => string.IsNullOrEmpty(MsgId);

    /// <summary>
    /// Whether this is an obsolete entry (marked with #~).
    /// </summary>
    public bool IsObsolete { get; set; }

    /// <summary>
    /// Gets the combined comment (translator + extracted).
    /// </summary>
    public string? GetCombinedComment()
    {
        if (string.IsNullOrEmpty(TranslatorComment) && string.IsNullOrEmpty(ExtractedComment))
            return null;

        if (string.IsNullOrEmpty(ExtractedComment))
            return TranslatorComment;

        if (string.IsNullOrEmpty(TranslatorComment))
            return ExtractedComment;

        return $"{TranslatorComment}\n{ExtractedComment}";
    }

    /// <summary>
    /// Generates the key for this entry based on the key strategy.
    /// </summary>
    /// <param name="strategy">Key generation strategy: auto, msgid, context, hash.</param>
    /// <returns>The generated key.</returns>
    public string GenerateKey(string strategy = "auto")
    {
        return strategy.ToLowerInvariant() switch
        {
            "msgid" => MsgId,
            "context" when !string.IsNullOrEmpty(Context) => Context,
            "context" => MsgId, // Fallback if no context
            "hash" => GenerateHash(),
            "auto" or _ => GenerateAutoKey()
        };
    }

    /// <summary>
    /// Generates a key using the hybrid (auto) strategy.
    /// Uses context if available, otherwise uses msgid.
    /// </summary>
    private string GenerateAutoKey()
    {
        if (!string.IsNullOrEmpty(Context))
            return $"{Context}|{MsgId}";

        // For very long msgids, use a hash
        if (MsgId.Length > 500)
            return GenerateHash();

        return MsgId;
    }

    /// <summary>
    /// Generates a hash-based key for very long msgids.
    /// </summary>
    private string GenerateHash()
    {
        var input = string.IsNullOrEmpty(Context) ? MsgId : $"{Context}|{MsgId}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).Substring(0, 16);
    }
}

/// <summary>
/// Represents the parsed header of a PO file.
/// </summary>
internal class PoHeader
{
    /// <summary>
    /// Project-Id-Version
    /// </summary>
    public string? ProjectIdVersion { get; set; }

    /// <summary>
    /// Report-Msgid-Bugs-To
    /// </summary>
    public string? ReportMsgidBugsTo { get; set; }

    /// <summary>
    /// POT-Creation-Date
    /// </summary>
    public string? PotCreationDate { get; set; }

    /// <summary>
    /// PO-Revision-Date
    /// </summary>
    public string? PoRevisionDate { get; set; }

    /// <summary>
    /// Last-Translator
    /// </summary>
    public string? LastTranslator { get; set; }

    /// <summary>
    /// Language-Team
    /// </summary>
    public string? LanguageTeam { get; set; }

    /// <summary>
    /// Language code (Language header).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// MIME-Version
    /// </summary>
    public string? MimeVersion { get; set; }

    /// <summary>
    /// Content-Type (includes charset).
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Content-Transfer-Encoding
    /// </summary>
    public string? ContentTransferEncoding { get; set; }

    /// <summary>
    /// Plural-Forms expression.
    /// </summary>
    public string? PluralForms { get; set; }

    /// <summary>
    /// Extracted charset from Content-Type.
    /// </summary>
    public string Charset { get; set; } = "UTF-8";

    /// <summary>
    /// Extracted nplurals from Plural-Forms.
    /// </summary>
    public int NPlurals { get; set; } = 2;

    /// <summary>
    /// X-Generator header.
    /// </summary>
    public string? Generator { get; set; }

    /// <summary>
    /// Parse header values from msgstr content.
    /// </summary>
    public static PoHeader Parse(string headerContent)
    {
        var header = new PoHeader();
        var lines = headerContent.Split('\n');

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim().TrimEnd('\\', 'n');

            switch (key)
            {
                case "Project-Id-Version":
                    header.ProjectIdVersion = value;
                    break;
                case "Report-Msgid-Bugs-To":
                    header.ReportMsgidBugsTo = value;
                    break;
                case "POT-Creation-Date":
                    header.PotCreationDate = value;
                    break;
                case "PO-Revision-Date":
                    header.PoRevisionDate = value;
                    break;
                case "Last-Translator":
                    header.LastTranslator = value;
                    break;
                case "Language-Team":
                    header.LanguageTeam = value;
                    break;
                case "Language":
                    header.Language = value;
                    break;
                case "MIME-Version":
                    header.MimeVersion = value;
                    break;
                case "Content-Type":
                    header.ContentType = value;
                    header.Charset = ExtractCharset(value);
                    break;
                case "Content-Transfer-Encoding":
                    header.ContentTransferEncoding = value;
                    break;
                case "Plural-Forms":
                    header.PluralForms = value;
                    header.NPlurals = ExtractNPlurals(value);
                    break;
                case "X-Generator":
                    header.Generator = value;
                    break;
            }
        }

        return header;
    }

    private static string ExtractCharset(string contentType)
    {
        // Content-Type: text/plain; charset=UTF-8
        var match = System.Text.RegularExpressions.Regex.Match(
            contentType, @"charset=([^\s;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "UTF-8";
    }

    private static int ExtractNPlurals(string pluralForms)
    {
        // Plural-Forms: nplurals=2; plural=(n != 1);
        var match = System.Text.RegularExpressions.Regex.Match(
            pluralForms, @"nplurals=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 2;
    }
}
