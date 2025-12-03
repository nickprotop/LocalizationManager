// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;

namespace LocalizationManager.JsonLocalization.Core;

/// <summary>
/// Resolves CLDR plural forms based on count and culture.
/// Implements simplified CLDR plural rules for common languages.
/// </summary>
public static class PluralResolver
{
    /// <summary>
    /// CLDR plural form categories.
    /// </summary>
    public static class Categories
    {
        public const string Zero = "zero";
        public const string One = "one";
        public const string Two = "two";
        public const string Few = "few";
        public const string Many = "many";
        public const string Other = "other";
    }

    /// <summary>
    /// Gets the CLDR plural form for a count in the specified culture.
    /// </summary>
    /// <param name="count">The count value.</param>
    /// <param name="culture">The culture to use for plural rules. If null, uses current culture.</param>
    /// <returns>The plural form category: "zero", "one", "two", "few", "many", or "other".</returns>
    public static string GetPluralForm(int count, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var languageCode = GetBaseLanguageCode(culture);

        return languageCode switch
        {
            // Languages with zero form
            "ar" => GetArabicPlural(count),
            "lv" => GetLatvianPlural(count),

            // Languages with one/other only (Germanic, Romance, etc.)
            "en" or "de" or "es" or "it" or "pt" or "nl" or "da" or "no" or "sv" or "fi" or "el" or "he" or "hu" or "tr" =>
                count == 1 ? Categories.One : Categories.Other,

            // French: 0 and 1 are singular
            "fr" => count <= 1 ? Categories.One : Categories.Other,

            // Slavic languages (Russian, Ukrainian, Polish, Czech, Slovak)
            "ru" or "uk" or "be" => GetSlavicPlural(count),
            "pl" => GetPolishPlural(count),
            "cs" or "sk" => GetCzechPlural(count),

            // Celtic languages
            "ga" => GetIrishPlural(count),
            "cy" => GetWelshPlural(count),

            // Asian languages (typically no grammatical number)
            "zh" or "ja" or "ko" or "vi" or "th" or "id" or "ms" => Categories.Other,

            // Default fallback
            _ => count == 1 ? Categories.One : Categories.Other
        };
    }

    /// <summary>
    /// Gets the base language code (without region) from a culture.
    /// </summary>
    private static string GetBaseLanguageCode(CultureInfo culture)
    {
        var code = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return code;
    }

    /// <summary>
    /// Arabic plural rules (6 forms).
    /// </summary>
    private static string GetArabicPlural(int count)
    {
        if (count == 0) return Categories.Zero;
        if (count == 1) return Categories.One;
        if (count == 2) return Categories.Two;

        var mod100 = count % 100;
        if (mod100 >= 3 && mod100 <= 10) return Categories.Few;
        if (mod100 >= 11 && mod100 <= 99) return Categories.Many;

        return Categories.Other;
    }

    /// <summary>
    /// Latvian plural rules.
    /// </summary>
    private static string GetLatvianPlural(int count)
    {
        if (count == 0) return Categories.Zero;

        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11) return Categories.One;
        return Categories.Other;
    }

    /// <summary>
    /// Russian/Ukrainian/Belarusian plural rules.
    /// </summary>
    private static string GetSlavicPlural(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11) return Categories.One;
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return Categories.Few;
        if (mod10 == 0 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 11 && mod100 <= 14)) return Categories.Many;

        return Categories.Other;
    }

    /// <summary>
    /// Polish plural rules.
    /// </summary>
    private static string GetPolishPlural(int count)
    {
        if (count == 1) return Categories.One;

        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return Categories.Few;
        if (mod10 == 0 || mod10 == 1 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 12 && mod100 <= 14)) return Categories.Many;

        return Categories.Other;
    }

    /// <summary>
    /// Czech/Slovak plural rules.
    /// </summary>
    private static string GetCzechPlural(int count)
    {
        if (count == 1) return Categories.One;
        if (count >= 2 && count <= 4) return Categories.Few;
        return Categories.Other;
    }

    /// <summary>
    /// Irish plural rules.
    /// </summary>
    private static string GetIrishPlural(int count)
    {
        if (count == 1) return Categories.One;
        if (count == 2) return Categories.Two;
        if (count >= 3 && count <= 6) return Categories.Few;
        if (count >= 7 && count <= 10) return Categories.Many;
        return Categories.Other;
    }

    /// <summary>
    /// Welsh plural rules.
    /// </summary>
    private static string GetWelshPlural(int count)
    {
        return count switch
        {
            0 => Categories.Zero,
            1 => Categories.One,
            2 => Categories.Two,
            3 => Categories.Few,
            6 => Categories.Many,
            _ => Categories.Other
        };
    }
}
