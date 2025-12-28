// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json.Serialization;

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// OTA bundle response from LRM Cloud.
/// </summary>
public class OtaBundle
{
    /// <summary>
    /// Version timestamp (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>
    /// Project identifier.
    /// </summary>
    [JsonPropertyName("project")]
    public string Project { get; set; } = "";

    /// <summary>
    /// Default/source language code.
    /// </summary>
    [JsonPropertyName("defaultLanguage")]
    public string DefaultLanguage { get; set; } = "";

    /// <summary>
    /// List of available language codes.
    /// </summary>
    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Keys deleted since the last sync (for delta updates).
    /// </summary>
    [JsonPropertyName("deleted")]
    public List<string> Deleted { get; set; } = new();

    /// <summary>
    /// Translations organized by language code, then by key.
    /// Values can be strings or dictionaries (for plural forms).
    /// </summary>
    [JsonPropertyName("translations")]
    public Dictionary<string, Dictionary<string, object>> Translations { get; set; } = new();
}

/// <summary>
/// OTA version response for efficient polling.
/// </summary>
public class OtaVersion
{
    /// <summary>
    /// Version timestamp (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}
