// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// XLIFF implementation of the resource backend.
/// Coordinates discovery, reading, writing, and validation of XLIFF localization files.
/// Supports both XLIFF 1.2 and 2.0 formats with auto-detection.
/// </summary>
public class XliffResourceBackend : IResourceBackend
{
    private readonly XliffFormatConfiguration _config;

    /// <inheritdoc />
    public string Name => "xliff";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".xliff", ".xlf" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    /// <summary>
    /// Creates a new XLIFF backend with optional configuration.
    /// </summary>
    /// <param name="config">XLIFF format configuration. If null, uses defaults.</param>
    public XliffResourceBackend(XliffFormatConfiguration? config = null)
    {
        _config = config ?? new XliffFormatConfiguration();
        Discovery = new XliffResourceDiscovery(_config);
        Reader = new XliffResourceReader(_config);
        Writer = new XliffResourceWriter(_config);
        Validator = new XliffResourceValidator(_config);
    }

    /// <summary>
    /// Creates a new XLIFF backend with auto-detection of configuration from the specified path.
    /// </summary>
    /// <param name="path">Path to the directory containing XLIFF files.</param>
    public XliffResourceBackend(string path)
    {
        var discovery = new XliffResourceDiscovery();
        var discoveryResult = discovery.DiscoverConfiguration(path);

        _config = new XliffFormatConfiguration
        {
            Version = discoveryResult.Version,
            FileExtension = discoveryResult.FileExtension,
            Bilingual = discoveryResult.Bilingual
        };

        Discovery = new XliffResourceDiscovery(_config);
        Reader = new XliffResourceReader(_config);
        Writer = new XliffResourceWriter(_config);
        Validator = new XliffResourceValidator(_config);
    }

    /// <inheritdoc />
    public bool CanHandle(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // Check for XLIFF files
        var hasXliffFiles = Directory.GetFiles(path, "*.xliff", SearchOption.AllDirectories).Length > 0;
        var hasXlfFiles = Directory.GetFiles(path, "*.xlf", SearchOption.AllDirectories).Length > 0;

        return hasXliffFiles || hasXlfFiles;
    }
}
