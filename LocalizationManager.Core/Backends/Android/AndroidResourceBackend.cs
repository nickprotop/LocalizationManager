// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Android implementation of the resource backend.
/// Coordinates discovery, reading, writing, and validation of Android strings.xml files.
/// Supports strings, plurals, and string-arrays.
/// </summary>
public class AndroidResourceBackend : IResourceBackend
{
    /// <inheritdoc />
    public string Name => "android";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".xml" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    /// <summary>
    /// Creates a new Android backend with optional resource file name configuration.
    /// </summary>
    /// <param name="resourceFileName">The resource file name (default: "strings.xml")</param>
    /// <param name="defaultLanguageCode">The default language code from configuration (e.g., "en").
    /// Used when there's no bare "values" folder to identify the source language.</param>
    public AndroidResourceBackend(string resourceFileName = "strings.xml", string? defaultLanguageCode = null)
    {
        Discovery = new AndroidResourceDiscovery(resourceFileName, defaultLanguageCode);
        Reader = new AndroidResourceReader();
        Writer = new AndroidResourceWriter(resourceFileName);
        Validator = new AndroidResourceValidator(resourceFileName);
    }
}
