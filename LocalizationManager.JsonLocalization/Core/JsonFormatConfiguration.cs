// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Core;

/// <summary>
/// Configuration settings for JSON localization format.
/// </summary>
public class JsonFormatConfiguration
{
    /// <summary>
    /// Use nested key structure (e.g., Errors.NotFound becomes {"Errors": {"NotFound": "..."}}).
    /// If false, keys are stored flat with dot notation ("Errors.NotFound": "...").
    /// Default: true
    /// </summary>
    public bool UseNestedKeys { get; set; } = true;

    /// <summary>
    /// Include _meta section in JSON files with language info and last modified date.
    /// Default: true
    /// </summary>
    public bool IncludeMeta { get; set; } = true;

    /// <summary>
    /// Preserve comments as _comment properties in JSON files.
    /// Default: true
    /// </summary>
    public bool PreserveComments { get; set; } = true;

    /// <summary>
    /// Base filename for resources (without extension or culture code).
    /// Example: "strings" produces "strings.json", "strings.fr.json", etc.
    /// Default: "strings"
    /// </summary>
    public string BaseName { get; set; } = "strings";

    /// <summary>
    /// Interpolation format for variable placeholders.
    /// Supported values: "dotnet" ({0}, {1}), "i18next" ({{name}}), "icu" ({name}).
    /// Default: "dotnet"
    /// </summary>
    public string InterpolationFormat { get; set; } = "dotnet";

    /// <summary>
    /// Plural key suffix format.
    /// Supported values: "cldr" (i18next-style: _zero, _one, _two, _few, _many, _other),
    ///                   "simple" (_singular, _plural).
    /// Default: "cldr"
    /// </summary>
    public string PluralFormat { get; set; } = "cldr";

    /// <summary>
    /// Enable full i18next compatibility mode.
    /// When true, uses i18next interpolation ({{var}}), CLDR plural suffixes,
    /// and i18next-style nesting ($t(key)).
    /// Overrides InterpolationFormat and PluralFormat settings.
    /// Default: false
    /// </summary>
    public bool I18nextCompatible { get; set; } = false;
}
