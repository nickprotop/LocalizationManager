// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// Maps PO plural indices to CLDR plural categories and vice versa.
/// Each language has different plural rules, and PO files use numeric indices
/// while CLDR uses named categories (zero, one, two, few, many, other).
/// </summary>
public static class PoPluralMapper
{
    /// <summary>
    /// CLDR plural category names in standard order.
    /// </summary>
    public static readonly string[] CldrCategories = { "zero", "one", "two", "few", "many", "other" };

    /// <summary>
    /// Maps language codes to their CLDR plural categories in index order.
    /// The index in the array corresponds to msgstr[n] in PO files.
    /// </summary>
    private static readonly Dictionary<string, string[]> LanguagePluralCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Languages with 1 form (no plural distinction)
        ["ja"] = new[] { "other" },
        ["ko"] = new[] { "other" },
        ["zh"] = new[] { "other" },
        ["vi"] = new[] { "other" },
        ["th"] = new[] { "other" },
        ["id"] = new[] { "other" },
        ["ms"] = new[] { "other" },
        ["lo"] = new[] { "other" },
        ["ka"] = new[] { "other" },
        ["my"] = new[] { "other" },
        ["km"] = new[] { "other" },
        ["tr"] = new[] { "other" },

        // Languages with 2 forms (one, other) - most common
        ["en"] = new[] { "one", "other" },
        ["de"] = new[] { "one", "other" },
        ["es"] = new[] { "one", "other" },
        ["it"] = new[] { "one", "other" },
        ["pt"] = new[] { "one", "other" },
        ["nl"] = new[] { "one", "other" },
        ["sv"] = new[] { "one", "other" },
        ["da"] = new[] { "one", "other" },
        ["no"] = new[] { "one", "other" },
        ["fi"] = new[] { "one", "other" },
        ["el"] = new[] { "one", "other" },
        ["hu"] = new[] { "one", "other" },
        ["bg"] = new[] { "one", "other" },
        ["he"] = new[] { "one", "other" },
        ["ca"] = new[] { "one", "other" },
        ["eu"] = new[] { "one", "other" },
        ["gl"] = new[] { "one", "other" },
        ["af"] = new[] { "one", "other" },
        ["sq"] = new[] { "one", "other" },
        ["et"] = new[] { "one", "other" },
        ["fo"] = new[] { "one", "other" },
        ["fy"] = new[] { "one", "other" },
        ["is"] = new[] { "one", "other" },
        ["lb"] = new[] { "one", "other" },
        ["mn"] = new[] { "one", "other" },
        ["ne"] = new[] { "one", "other" },
        ["nn"] = new[] { "one", "other" },
        ["or"] = new[] { "one", "other" },
        ["pa"] = new[] { "one", "other" },
        ["ps"] = new[] { "one", "other" },
        ["rm"] = new[] { "one", "other" },
        ["so"] = new[] { "one", "other" },
        ["sw"] = new[] { "one", "other" },
        ["ta"] = new[] { "one", "other" },
        ["te"] = new[] { "one", "other" },
        ["tk"] = new[] { "one", "other" },
        ["ur"] = new[] { "one", "other" },
        ["zu"] = new[] { "one", "other" },

        // French: 0 and 1 are singular (different from English!)
        ["fr"] = new[] { "one", "other" },

        // Languages with 2 forms (one, other) but one includes 0
        ["pt-br"] = new[] { "one", "other" },

        // Languages with 3 forms (one, few, other) - Slavic with special rules
        ["ru"] = new[] { "one", "few", "many" },
        ["uk"] = new[] { "one", "few", "many" },
        ["be"] = new[] { "one", "few", "many" },
        ["hr"] = new[] { "one", "few", "other" },
        ["sr"] = new[] { "one", "few", "other" },
        ["bs"] = new[] { "one", "few", "other" },

        // Polish: more complex (one, few, many)
        ["pl"] = new[] { "one", "few", "many" },

        // Czech/Slovak: (one, few, other)
        ["cs"] = new[] { "one", "few", "other" },
        ["sk"] = new[] { "one", "few", "other" },

        // Latvian: (zero, one, other)
        ["lv"] = new[] { "zero", "one", "other" },

        // Lithuanian: (one, few, other)
        ["lt"] = new[] { "one", "few", "other" },

        // Romanian: (one, few, other)
        ["ro"] = new[] { "one", "few", "other" },

        // Irish: (one, two, few, many, other)
        ["ga"] = new[] { "one", "two", "few", "many", "other" },

        // Welsh: (zero, one, two, few, many, other)
        ["cy"] = new[] { "zero", "one", "two", "few", "many", "other" },

        // Arabic: (zero, one, two, few, many, other) - 6 forms
        ["ar"] = new[] { "zero", "one", "two", "few", "many", "other" },

        // Slovenian: (one, two, few, other)
        ["sl"] = new[] { "one", "two", "few", "other" },

        // Maltese: (one, few, many, other)
        ["mt"] = new[] { "one", "few", "many", "other" },
    };

    /// <summary>
    /// Gets the expected number of plural forms for a language.
    /// </summary>
    /// <param name="languageCode">Language code (e.g., "en", "ru", "ar").</param>
    /// <returns>Number of plural forms.</returns>
    public static int GetPluralCount(string languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        if (LanguagePluralCategories.TryGetValue(normalizedCode, out var categories))
            return categories.Length;

        // Default: assume 2 forms like English
        return 2;
    }

    /// <summary>
    /// Converts a PO plural index to a CLDR category name.
    /// </summary>
    /// <param name="languageCode">Language code (e.g., "en", "ru").</param>
    /// <param name="index">PO plural index (0, 1, 2, ...).</param>
    /// <returns>CLDR category name (zero, one, two, few, many, other).</returns>
    public static string IndexToCategory(string languageCode, int index)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        if (LanguagePluralCategories.TryGetValue(normalizedCode, out var categories))
        {
            if (index >= 0 && index < categories.Length)
                return categories[index];
        }

        // Fallback for unknown languages
        return index switch
        {
            0 => "one",
            _ => "other"
        };
    }

    /// <summary>
    /// Converts a CLDR category name to a PO plural index.
    /// </summary>
    /// <param name="languageCode">Language code (e.g., "en", "ru").</param>
    /// <param name="category">CLDR category name (zero, one, two, few, many, other).</param>
    /// <returns>PO plural index, or -1 if not found.</returns>
    public static int CategoryToIndex(string languageCode, string category)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        if (LanguagePluralCategories.TryGetValue(normalizedCode, out var categories))
        {
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i].Equals(category, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        // Fallback for unknown languages
        return category.ToLowerInvariant() switch
        {
            "one" => 0,
            "other" => 1,
            _ => -1
        };
    }

    /// <summary>
    /// Gets all CLDR categories for a language in index order.
    /// </summary>
    /// <param name="languageCode">Language code.</param>
    /// <returns>Array of CLDR category names.</returns>
    public static string[] GetCategoriesForLanguage(string languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        if (LanguagePluralCategories.TryGetValue(normalizedCode, out var categories))
            return categories;

        // Default: assume like English
        return new[] { "one", "other" };
    }

    /// <summary>
    /// Normalizes a language code for lookup (lowercase, handle variants).
    /// </summary>
    private static string NormalizeLanguageCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "en";

        // Handle full locale codes like "en-US", "pt-BR"
        var normalized = code.ToLowerInvariant();

        // Check for exact match first (e.g., "pt-br")
        if (LanguagePluralCategories.ContainsKey(normalized))
            return normalized;

        // Try base language code
        var hyphenIndex = normalized.IndexOf('-');
        if (hyphenIndex > 0)
        {
            var baseCode = normalized.Substring(0, hyphenIndex);
            if (LanguagePluralCategories.ContainsKey(baseCode))
                return baseCode;
        }

        var underscoreIndex = normalized.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var baseCode = normalized.Substring(0, underscoreIndex);
            if (LanguagePluralCategories.ContainsKey(baseCode))
                return baseCode;
        }

        return normalized;
    }
}
