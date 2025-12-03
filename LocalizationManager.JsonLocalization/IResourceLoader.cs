// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Interface for loading JSON localization resources from various sources.
/// </summary>
public interface IResourceLoader
{
    /// <summary>
    /// Gets a stream for the specified resource.
    /// </summary>
    /// <param name="baseName">Base name of the resource (e.g., "strings").</param>
    /// <param name="culture">Culture code (e.g., "fr", "de"). Empty for default culture.</param>
    /// <returns>A stream containing the JSON content, or null if not found.</returns>
    Stream? GetResourceStream(string baseName, string culture);

    /// <summary>
    /// Gets all available culture codes for the specified resource base name.
    /// </summary>
    /// <param name="baseName">Base name of the resource.</param>
    /// <returns>Collection of available culture codes (empty string for default culture).</returns>
    IEnumerable<string> GetAvailableCultures(string baseName);
}
