// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// Reads and parses XLIFF files into ResourceFile objects.
/// Supports both XLIFF 1.2 and 2.0 formats.
/// </summary>
public class XliffResourceReader : IResourceReader
{
    private readonly XliffFormatConfiguration _config;
    private readonly XliffVersionDetector _detector = new();

    private static readonly XNamespace Ns12 = XliffVersionDetector.Xliff12Namespace;
    private static readonly XNamespace Ns20 = XliffVersionDetector.Xliff20Namespace;

    public XliffResourceReader(XliffFormatConfiguration? config = null)
    {
        _config = config ?? new XliffFormatConfiguration();
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("FilePath is required for file-based reading", nameof(language));

        return await Task.Run(() => Read(language), ct);
    }

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("FilePath is required for file-based reading", nameof(language));

        using var stream = new FileStream(language.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        return Read(reader, language);
    }

    /// <inheritdoc />
    public async Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
    {
        return await Task.Run(() => Read(reader, metadata), ct);
    }

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
    {
        var content = reader.ReadToEnd();

        // Use secure XML settings to prevent XXE attacks
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        XDocument doc;
        using (var xmlReader = XmlReader.Create(new StringReader(content), settings))
        {
            doc = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
        }

        // Detect version from the parsed document (more reliable than stream-based detection)
        var version = DetectVersionFromDocument(doc);

        var entries = version == "2.0"
            ? ParseXliff20(doc, metadata)
            : ParseXliff12(doc, metadata);

        return new ResourceFile
        {
            Language = metadata,
            Entries = entries
        };
    }

    /// <summary>
    /// Detects XLIFF version from a parsed XDocument.
    /// </summary>
    private string DetectVersionFromDocument(XDocument doc)
    {
        var root = doc.Root;
        if (root == null || root.Name.LocalName != "xliff")
            return "unknown";

        // Check namespace
        var ns = root.GetDefaultNamespace().NamespaceName;
        if (ns == XliffVersionDetector.Xliff20Namespace)
            return "2.0";
        if (ns == XliffVersionDetector.Xliff12Namespace)
            return "1.2";

        // Check version attribute
        var version = root.Attribute("version")?.Value;
        if (!string.IsNullOrEmpty(version))
        {
            if (version.StartsWith("2"))
                return "2.0";
            if (version.StartsWith("1"))
                return "1.2";
        }

        // Check for srcLang (XLIFF 2.0) vs source-language (XLIFF 1.2)
        if (root.Attribute("srcLang") != null)
            return "2.0";

        return "unknown";  // Let caller decide how to handle
    }

    /// <summary>
    /// Parses XLIFF 1.2 format.
    /// </summary>
    private List<ResourceEntry> ParseXliff12(XDocument doc, LanguageInfo metadata)
    {
        var entries = new List<ResourceEntry>();
        var root = doc.Root;

        if (root == null)
            return entries;

        // Handle both namespaced and non-namespaced elements
        var ns = root.GetDefaultNamespace();
        if (ns == XNamespace.None)
            ns = Ns12;

        var fileElements = root.Elements(ns + "file");
        foreach (var fileElement in fileElements)
        {
            var body = fileElement.Element(ns + "body");
            if (body == null) continue;

            // Parse trans-units
            var transUnits = body.Descendants(ns + "trans-unit");
            foreach (var unit in transUnits)
            {
                var entry = ParseTransUnit12(unit, ns, metadata);
                if (entry != null)
                    entries.Add(entry);
            }

            // Parse groups (for plural handling)
            var groups = body.Elements(ns + "group");
            foreach (var group in groups)
            {
                var restype = group.Attribute("restype")?.Value;
                if (restype == "x-gettext-plurals")
                {
                    var pluralEntry = ParsePluralGroup12(group, ns, metadata);
                    if (pluralEntry != null)
                        entries.Add(pluralEntry);
                }
                else
                {
                    // Regular group - parse nested trans-units
                    var nestedUnits = group.Descendants(ns + "trans-unit");
                    foreach (var unit in nestedUnits)
                    {
                        var entry = ParseTransUnit12(unit, ns, metadata);
                        if (entry != null)
                            entries.Add(entry);
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Parses a trans-unit element in XLIFF 1.2.
    /// </summary>
    private ResourceEntry? ParseTransUnit12(XElement unit, XNamespace ns, LanguageInfo metadata)
    {
        var id = unit.Attribute("id")?.Value;
        if (string.IsNullOrEmpty(id))
            return null;

        var source = unit.Element(ns + "source")?.Value;
        var target = unit.Element(ns + "target")?.Value;

        // Get notes (comments)
        var notes = unit.Elements(ns + "note")
            .Select(n => n.Value)
            .Where(n => !string.IsNullOrEmpty(n));
        var comment = string.Join("\n", notes);

        // Get state for translation status
        var state = unit.Element(ns + "target")?.Attribute("state")?.Value;

        // Always prefer target value (which contains the localized text)
        // Fall back to source if target is not present
        var value = target ?? source ?? "";

        return new ResourceEntry
        {
            Key = id,
            Value = value,
            Comment = string.IsNullOrEmpty(comment) ? null : comment
        };
    }

    /// <summary>
    /// Parses a plural group in XLIFF 1.2.
    /// </summary>
    private ResourceEntry? ParsePluralGroup12(XElement group, XNamespace ns, LanguageInfo metadata)
    {
        var id = group.Attribute("id")?.Value
            ?? group.Attribute("resname")?.Value;

        if (string.IsNullOrEmpty(id))
            return null;

        var pluralForms = new Dictionary<string, string>();
        var transUnits = group.Elements(ns + "trans-unit").ToList();

        foreach (var unit in transUnits)
        {
            var pluralId = unit.Attribute("id")?.Value;
            var source = unit.Element(ns + "source")?.Value;
            var target = unit.Element(ns + "target")?.Value;

            // Always prefer target value (which contains the localized text)
            var pluralValue = target ?? source;

            if (!string.IsNullOrEmpty(pluralId) && !string.IsNullOrEmpty(pluralValue))
            {
                // Extract plural category from id (e.g., "key[one]", "key[other]")
                var category = ExtractPluralCategory(pluralId);
                if (!string.IsNullOrEmpty(category))
                    pluralForms[category] = pluralValue;
            }
        }

        if (pluralForms.Count == 0)
            return null;

        // Get notes from group
        var notes = group.Elements(ns + "note")
            .Select(n => n.Value)
            .Where(n => !string.IsNullOrEmpty(n));
        var comment = string.Join("\n", notes);

        return new ResourceEntry
        {
            Key = id,
            Value = pluralForms.GetValueOrDefault("other")
                ?? pluralForms.GetValueOrDefault("one")
                ?? pluralForms.Values.FirstOrDefault()
                ?? "",
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            IsPlural = true,
            PluralForms = pluralForms
        };
    }

    /// <summary>
    /// Parses XLIFF 2.0 format.
    /// </summary>
    private List<ResourceEntry> ParseXliff20(XDocument doc, LanguageInfo metadata)
    {
        var entries = new List<ResourceEntry>();
        var root = doc.Root;

        if (root == null)
            return entries;

        var ns = root.GetDefaultNamespace();
        if (ns == XNamespace.None)
            ns = Ns20;

        var fileElements = root.Elements(ns + "file");
        foreach (var fileElement in fileElements)
        {
            // Parse units
            var units = fileElement.Descendants(ns + "unit");
            foreach (var unit in units)
            {
                var entry = ParseUnit20(unit, ns, metadata);
                if (entry != null)
                    entries.Add(entry);
            }

            // Parse groups
            var groups = fileElement.Elements(ns + "group");
            foreach (var group in groups)
            {
                // Check if this is a plural group (has multiple units with same base key)
                var groupUnits = group.Elements(ns + "unit").ToList();
                if (groupUnits.Count > 1 && IsPluralGroup(groupUnits))
                {
                    var pluralEntry = ParsePluralGroup20(group, ns, metadata);
                    if (pluralEntry != null)
                        entries.Add(pluralEntry);
                }
                else
                {
                    // Regular group - parse nested units
                    var nestedUnits = group.Descendants(ns + "unit");
                    foreach (var unit in nestedUnits)
                    {
                        var entry = ParseUnit20(unit, ns, metadata);
                        if (entry != null)
                            entries.Add(entry);
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Parses a unit element in XLIFF 2.0.
    /// </summary>
    private ResourceEntry? ParseUnit20(XElement unit, XNamespace ns, LanguageInfo metadata)
    {
        var id = unit.Attribute("id")?.Value;
        if (string.IsNullOrEmpty(id))
            return null;

        // XLIFF 2.0 uses segments inside units
        var segments = unit.Elements(ns + "segment").ToList();
        if (segments.Count == 0)
        {
            // Fallback: some tools put source/target directly in unit
            var directSource = unit.Element(ns + "source")?.Value;
            var directTarget = unit.Element(ns + "target")?.Value;

            if (directSource != null || directTarget != null)
            {
                // Always prefer target value (which contains the localized text)
                var directValue = directTarget ?? directSource ?? "";
                return new ResourceEntry
                {
                    Key = id,
                    Value = directValue
                };
            }
            return null;
        }

        // Concatenate all segments
        var values = new List<string>();
        foreach (var segment in segments)
        {
            var target = segment.Element(ns + "target")?.Value;
            var source = segment.Element(ns + "source")?.Value;
            // Always prefer target value (which contains the localized text)
            values.Add(target ?? source ?? "");
        }
        var value = string.Join("", values);

        // Get notes
        var notesElement = unit.Element(ns + "notes");
        var notes = notesElement?.Elements(ns + "note")
            .Select(n => n.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            ?? Enumerable.Empty<string>();
        var comment = string.Join("\n", notes);

        return new ResourceEntry
        {
            Key = id,
            Value = value,
            Comment = string.IsNullOrEmpty(comment) ? null : comment
        };
    }

    /// <summary>
    /// Extracts the plural category from an ID like "key[one]" or "key_plural_one".
    /// </summary>
    private static string? ExtractPluralCategory(string id)
    {
        return ExtractPluralCategoryWithBase(id).category;
    }

    /// <summary>
    /// Extracts both the base name and plural category from an ID like "key[one]" or "key_plural_one".
    /// Returns (baseName, category) or (id, null) if not a plural suffix key.
    /// </summary>
    private static (string baseName, string? category) ExtractPluralCategoryWithBase(string id)
    {
        // Pattern 1: key[category]
        var bracketStart = id.LastIndexOf('[');
        var bracketEnd = id.LastIndexOf(']');
        // Validate: brackets must be in correct order and have content between them
        if (bracketStart >= 0 && bracketEnd > bracketStart + 1)
        {
            var baseName = id.Substring(0, bracketStart);
            var category = id.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            return (baseName, category);
        }

        // Pattern 2: key_plural_category
        var categories = new[] { "zero", "one", "two", "few", "many", "other" };
        foreach (var cat in categories)
        {
            if (id.EndsWith($"_{cat}", StringComparison.OrdinalIgnoreCase))
            {
                return (id.Substring(0, id.Length - cat.Length - 1), cat);
            }
            if (id.EndsWith($"_plural_{cat}", StringComparison.OrdinalIgnoreCase))
            {
                return (id.Substring(0, id.Length - cat.Length - 8), cat);
            }
        }

        return (id, null);
    }

    /// <summary>
    /// Checks if a group's units represent a plural entry.
    /// Returns true if all units share the same base key and there are multiple units.
    /// </summary>
    private bool IsPluralGroup(List<XElement> units)
    {
        if (units.Count <= 1)
            return false;

        // Extract base keys (remove [category] suffix)
        var baseKeys = units
            .Select(u => u.Attribute("id")?.Value)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => ExtractPluralCategoryWithBase(id!).baseName)
            .Distinct()
            .ToList();

        // If all units share the same base key and there are multiple units, it's a plural group
        return baseKeys.Count == 1 && units.Count > 1;
    }

    /// <summary>
    /// Parses a plural group in XLIFF 2.0.
    /// </summary>
    private ResourceEntry? ParsePluralGroup20(XElement group, XNamespace ns, LanguageInfo metadata)
    {
        var id = group.Attribute("id")?.Value ?? group.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(id))
            return null;

        var pluralForms = new Dictionary<string, string>();
        var units = group.Elements(ns + "unit").ToList();

        foreach (var unit in units)
        {
            var unitId = unit.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(unitId))
                continue;

            // Extract category from ID like "key[one]" or "key[other]"
            var (baseName, category) = ExtractPluralCategoryWithBase(unitId);
            if (string.IsNullOrEmpty(category))
                continue;

            // Get target value from segment
            var segment = unit.Element(ns + "segment");
            var target = segment?.Element(ns + "target")?.Value;
            var source = segment?.Element(ns + "source")?.Value;

            pluralForms[category] = target ?? source ?? "";
        }

        if (pluralForms.Count == 0)
            return null;

        // Get notes from group
        var notesElement = group.Element(ns + "notes");
        var notes = notesElement?.Elements(ns + "note")
            .Select(n => n.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            ?? Enumerable.Empty<string>();
        var comment = string.Join("\n", notes);

        return new ResourceEntry
        {
            Key = id,
            Value = pluralForms.GetValueOrDefault("other")
                ?? pluralForms.GetValueOrDefault("one")
                ?? pluralForms.Values.FirstOrDefault()
                ?? "",
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            IsPlural = true,
            PluralForms = pluralForms
        };
    }
}
