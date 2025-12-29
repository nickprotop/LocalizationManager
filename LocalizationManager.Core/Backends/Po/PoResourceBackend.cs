// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Core.Backends.Po;

/// <summary>
/// PO (GNU gettext) implementation of the resource backend.
/// Coordinates discovery, reading, writing, and validation of PO/POT localization files.
/// Supports both GNU folder structure (locale/{lang}/LC_MESSAGES/) and flat structure.
/// </summary>
public class PoResourceBackend : IResourceBackend
{
    private readonly PoFormatConfiguration _config;
    private readonly string? _defaultLanguageCode;

    /// <inheritdoc />
    public string Name => "po";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".po", ".pot" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    /// <summary>
    /// Creates a new PO backend with optional configuration.
    /// </summary>
    /// <param name="config">PO format configuration. If null, uses defaults.</param>
    /// <param name="defaultLanguageCode">Default/source language code. Used when no POT file exists.</param>
    public PoResourceBackend(PoFormatConfiguration? config = null, string? defaultLanguageCode = null)
    {
        _config = config ?? new PoFormatConfiguration();
        _defaultLanguageCode = defaultLanguageCode;
        Discovery = new PoResourceDiscovery(_config, _defaultLanguageCode);
        Reader = new PoResourceReader(_config);
        Writer = new PoResourceWriter(_config);
        Validator = new PoResourceValidator(_config);
    }

    /// <summary>
    /// Creates a new PO backend with auto-detection of configuration from the specified path.
    /// </summary>
    /// <param name="path">Path to the directory containing PO files.</param>
    /// <param name="defaultLanguageCode">Default/source language code. Used when no POT file exists.</param>
    public PoResourceBackend(string path, string? defaultLanguageCode = null)
    {
        var discovery = new PoResourceDiscovery();
        var discoveryResult = discovery.DiscoverConfiguration(path);

        _config = new PoFormatConfiguration
        {
            Domain = discoveryResult.Domain,
            FolderStructure = discoveryResult.FolderStructure,
            KeyStrategy = "auto"
        };

        _defaultLanguageCode = defaultLanguageCode;
        Discovery = new PoResourceDiscovery(_config, _defaultLanguageCode);
        Reader = new PoResourceReader(_config);
        Writer = new PoResourceWriter(_config);
        Validator = new PoResourceValidator(_config);
    }

    /// <inheritdoc />
    public bool CanHandle(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // Check for PO/POT files
        var hasPoFiles = Directory.GetFiles(path, "*.po", SearchOption.AllDirectories).Length > 0;
        var hasPotFiles = Directory.GetFiles(path, "*.pot", SearchOption.AllDirectories).Length > 0;

        if (hasPoFiles || hasPotFiles)
            return true;

        // Check for GNU gettext structure (locale/*/LC_MESSAGES/)
        var localeDir = Path.Combine(path, "locale");
        if (Directory.Exists(localeDir))
        {
            var lcMessagesDirs = Directory.GetDirectories(localeDir, "LC_MESSAGES", SearchOption.AllDirectories);
            if (lcMessagesDirs.Any())
                return true;
        }

        // Check for common PO folder names
        var commonDirs = new[] { "po", "locales", "i18n" };
        foreach (var dir in commonDirs)
        {
            var poDir = Path.Combine(path, dir);
            if (Directory.Exists(poDir) &&
                Directory.GetFiles(poDir, "*.po").Length > 0)
                return true;
        }

        return false;
    }
}
