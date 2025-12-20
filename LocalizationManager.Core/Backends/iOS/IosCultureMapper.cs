// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Maps between iOS .lproj folder naming conventions and .NET culture codes.
/// iOS uses: en.lproj, es.lproj, zh-Hans.lproj, pt-BR.lproj, Base.lproj
/// .NET uses: "en", "es", "zh-Hans", "pt-BR", ""
/// </summary>
public static class IosCultureMapper
{
    /// <summary>
    /// Converts iOS lproj folder name to .NET culture code.
    /// The .lproj folder name IS the culture code (mostly).
    /// Special case: "Base.lproj" is the development language (default).
    /// </summary>
    /// <param name="folderName">The .lproj folder name (e.g., "en.lproj", "Base.lproj")</param>
    /// <param name="developmentLanguage">Optional development language code for Base.lproj resolution</param>
    /// <returns>The .NET culture code</returns>
    public static string LprojToCode(string folderName, string? developmentLanguage = null)
    {
        if (string.IsNullOrEmpty(folderName))
            throw new ArgumentException("Folder name cannot be null or empty", nameof(folderName));

        if (!folderName.EndsWith(".lproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid iOS resource folder: {folderName}", nameof(folderName));

        var code = folderName.Substring(0, folderName.Length - 6); // Remove ".lproj"

        // Base.lproj is the development language
        if (code.Equals("Base", StringComparison.OrdinalIgnoreCase))
        {
            return developmentLanguage ?? "";
        }

        return code;
    }

    /// <summary>
    /// Converts .NET culture code to iOS lproj folder name.
    /// </summary>
    /// <param name="cultureCode">The .NET culture code (e.g., "en", "zh-Hans")</param>
    /// <param name="useBase">If true, returns "Base.lproj" for empty culture code</param>
    /// <returns>The iOS .lproj folder name</returns>
    public static string CodeToLproj(string cultureCode, bool useBase = false)
    {
        if (string.IsNullOrEmpty(cultureCode))
            return useBase ? "Base.lproj" : "en.lproj";

        return $"{cultureCode}.lproj";
    }

    /// <summary>
    /// Checks if a folder name is a valid iOS .lproj folder.
    /// </summary>
    public static bool IsValidLprojFolder(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return false;

        return folderName.EndsWith(".lproj", StringComparison.OrdinalIgnoreCase) &&
               folderName.Length > 6; // Must have at least one character before ".lproj"
    }

    /// <summary>
    /// Checks if the folder is a Base.lproj folder (development language).
    /// </summary>
    public static bool IsBaseLproj(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return false;

        return folderName.Equals("Base.lproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the standard strings filename.
    /// </summary>
    public const string LocalizableStrings = "Localizable.strings";

    /// <summary>
    /// Gets the standard stringsdict filename for plurals.
    /// </summary>
    public const string LocalizableStringsdict = "Localizable.stringsdict";
}
