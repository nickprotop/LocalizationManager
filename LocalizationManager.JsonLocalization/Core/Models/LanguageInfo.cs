// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Core.Models;

/// <summary>
/// Represents metadata about a language resource.
/// </summary>
public class LanguageInfo
{
    /// <summary>
    /// Base name of the resource file (e.g., "strings").
    /// </summary>
    public required string BaseName { get; set; }

    /// <summary>
    /// Culture code (e.g., "en", "el", "fr"). Empty for default/invariant culture.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name of the language (e.g., "English (en)", "Greek (el)").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Indicates if this is the default language (no culture suffix).
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Full file path or resource name for the localization file.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Gets a display-friendly language code.
    /// Returns "default" for the default language (empty code), otherwise returns the code.
    /// </summary>
    public string GetDisplayCode() => string.IsNullOrEmpty(Code) ? "default" : Code;
}
