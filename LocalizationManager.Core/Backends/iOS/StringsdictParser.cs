// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Xml.Linq;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Parses iOS .stringsdict plist files for plurals.
/// </summary>
public class StringsdictParser
{
    /// <summary>
    /// Represents a parsed plural entry from a .stringsdict file.
    /// </summary>
    public record StringsdictEntry(
        string Key,
        string FormatKey,
        string ValueType,
        Dictionary<string, string> PluralForms);

    /// <summary>
    /// CLDR plural categories supported by iOS.
    /// </summary>
    public static readonly string[] PluralCategories = { "zero", "one", "two", "few", "many", "other" };

    /// <summary>
    /// Parses a .stringsdict file content into a list of plural entries.
    /// </summary>
    public List<StringsdictEntry> Parse(string content)
    {
        var results = new List<StringsdictEntry>();

        try
        {
            var doc = XDocument.Parse(content);
            var plist = doc.Element("plist");
            var rootDict = plist?.Element("dict");

            if (rootDict == null)
                return results;

            // Parse each key-dict pair in the root
            var elements = rootDict.Elements().ToList();
            for (int i = 0; i < elements.Count - 1; i += 2)
            {
                if (elements[i].Name != "key" || elements[i + 1].Name != "dict")
                    continue;

                var entryKey = elements[i].Value;
                var entryDict = elements[i + 1];

                var entry = ParsePluralEntry(entryKey, entryDict);
                if (entry != null)
                    results.Add(entry);
            }
        }
        catch (Exception)
        {
            // Invalid XML, return empty list
        }

        return results;
    }

    /// <summary>
    /// Parses a single plural entry from a dict element.
    /// </summary>
    private StringsdictEntry? ParsePluralEntry(string key, XElement entryDict)
    {
        string? formatKey = null;
        string? valueType = null;
        Dictionary<string, string>? pluralForms = null;
        string? variableName = null;

        var elements = entryDict.Elements().ToList();
        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name != "key")
                continue;

            var propKey = elements[i].Value;
            var propValue = elements[i + 1];

            switch (propKey)
            {
                case "NSStringLocalizedFormatKey":
                    formatKey = propValue.Value;
                    // Extract variable name from format like "%#@count@"
                    if (formatKey.Contains("@"))
                    {
                        var start = formatKey.IndexOf('@') + 1;
                        var end = formatKey.LastIndexOf('@');
                        if (end > start)
                        {
                            variableName = formatKey.Substring(start, end - start);
                        }
                    }
                    break;

                default:
                    // This might be the plural variable definition
                    if (propValue.Name == "dict")
                    {
                        var parsed = ParsePluralVariable(propValue);
                        if (parsed != null)
                        {
                            valueType = parsed.Value.ValueType;
                            pluralForms = parsed.Value.Forms;
                        }
                    }
                    break;
            }
        }

        if (formatKey != null && pluralForms != null && pluralForms.Count > 0)
        {
            return new StringsdictEntry(key, formatKey, valueType ?? "d", pluralForms);
        }

        return null;
    }

    /// <summary>
    /// Parses a plural variable dict.
    /// </summary>
    private (string ValueType, Dictionary<string, string> Forms)? ParsePluralVariable(XElement dict)
    {
        string valueType = "d";
        var forms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var elements = dict.Elements().ToList();
        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name != "key")
                continue;

            var propKey = elements[i].Value;
            var propValue = elements[i + 1];

            switch (propKey)
            {
                case "NSStringFormatSpecTypeKey":
                    // Should be "NSStringPluralRuleType"
                    break;
                case "NSStringFormatValueTypeKey":
                    valueType = propValue.Value;
                    break;
                case "zero":
                case "one":
                case "two":
                case "few":
                case "many":
                case "other":
                    forms[propKey] = propValue.Value;
                    break;
            }
        }

        return forms.Count > 0 ? (valueType, forms) : null;
    }

    /// <summary>
    /// Serializes plural entries back to .stringsdict plist format.
    /// </summary>
    public string Serialize(IEnumerable<StringsdictEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\">");
        sb.AppendLine("<dict>");

        foreach (var entry in entries)
        {
            var variableName = ExtractVariableName(entry.FormatKey) ?? "count";

            sb.AppendLine($"\t<key>{EscapeXml(entry.Key)}</key>");
            sb.AppendLine("\t<dict>");
            sb.AppendLine("\t\t<key>NSStringLocalizedFormatKey</key>");
            sb.AppendLine($"\t\t<string>{EscapeXml(entry.FormatKey)}</string>");
            sb.AppendLine($"\t\t<key>{variableName}</key>");
            sb.AppendLine("\t\t<dict>");
            sb.AppendLine("\t\t\t<key>NSStringFormatSpecTypeKey</key>");
            sb.AppendLine("\t\t\t<string>NSStringPluralRuleType</string>");
            sb.AppendLine("\t\t\t<key>NSStringFormatValueTypeKey</key>");
            sb.AppendLine($"\t\t\t<string>{entry.ValueType}</string>");

            // Write plural forms in order
            foreach (var category in PluralCategories)
            {
                if (entry.PluralForms.TryGetValue(category, out var value))
                {
                    sb.AppendLine($"\t\t\t<key>{category}</key>");
                    sb.AppendLine($"\t\t\t<string>{EscapeXml(value)}</string>");
                }
            }

            sb.AppendLine("\t\t</dict>");
            sb.AppendLine("\t</dict>");
        }

        sb.AppendLine("</dict>");
        sb.AppendLine("</plist>");

        return sb.ToString();
    }

    /// <summary>
    /// Creates a format key for a given variable name.
    /// </summary>
    public static string CreateFormatKey(string variableName = "count")
    {
        return $"%#@{variableName}@";
    }

    /// <summary>
    /// Extracts variable name from format key like "%#@count@".
    /// </summary>
    private string? ExtractVariableName(string formatKey)
    {
        if (string.IsNullOrEmpty(formatKey))
            return null;

        var start = formatKey.IndexOf('@');
        var end = formatKey.LastIndexOf('@');

        if (start >= 0 && end > start + 1)
        {
            return formatKey.Substring(start + 1, end - start - 1);
        }

        return null;
    }

    /// <summary>
    /// Escapes a string for XML content.
    /// </summary>
    private static string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// Gets the printf-style format specifier from a value type.
    /// </summary>
    public static string GetFormatSpecifier(string valueType) => valueType switch
    {
        "d" or "i" or "u" => "%d",
        "f" or "F" => "%f",
        "s" => "%s",
        "@" => "%@",
        _ => "%d"
    };
}
