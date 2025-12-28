// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LrmCloud.Shared.DTOs.Ota;

/// <summary>
/// OTA bundle response containing translations for a project.
/// </summary>
public class OtaBundleDto
{
    /// <summary>
    /// Version timestamp of this bundle (ISO 8601 format).
    /// Used for delta updates via ETag and ?since= parameter.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// Project identifier (@username/project or org/project).
    /// </summary>
    public string Project { get; set; } = "";

    /// <summary>
    /// Default/source language code.
    /// </summary>
    public string DefaultLanguage { get; set; } = "";

    /// <summary>
    /// List of available language codes.
    /// </summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Keys deleted since the 'since' timestamp (for delta updates).
    /// </summary>
    public List<string> Deleted { get; set; } = new();

    /// <summary>
    /// Translations organized by language code, then by key.
    /// For regular values: { "en": { "Hello": "Hello!", "Goodbye": "Goodbye!" } }
    /// For plural values: { "en": { "Items": { "one": "{0} item", "other": "{0} items" } } }
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> Translations { get; set; } = new();
}

/// <summary>
/// OTA version response for efficient polling.
/// </summary>
public class OtaVersionDto
{
    /// <summary>
    /// Version timestamp of the current bundle (ISO 8601 format).
    /// </summary>
    public string Version { get; set; } = "";
}
