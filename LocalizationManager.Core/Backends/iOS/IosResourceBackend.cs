// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// iOS implementation of the resource backend.
/// Coordinates discovery, reading, writing, and validation of iOS .strings and .stringsdict files.
/// Supports simple strings and plurals (via .stringsdict).
/// </summary>
public class IosResourceBackend : IResourceBackend
{
    private readonly string _stringsFileName;
    private readonly string _stringsdictFileName;

    /// <inheritdoc />
    public string Name => "ios";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".strings", ".stringsdict" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    /// <summary>
    /// Creates a new iOS backend with optional configuration.
    /// </summary>
    /// <param name="stringsFileName">The strings file name (default: "Localizable.strings")</param>
    /// <param name="developmentLanguage">The development language for Base.lproj resolution</param>
    public IosResourceBackend(
        string stringsFileName = "Localizable.strings",
        string? developmentLanguage = null)
    {
        _stringsFileName = stringsFileName;
        _stringsdictFileName = stringsFileName.Replace(".strings", ".stringsdict");
        Discovery = new IosResourceDiscovery(stringsFileName, developmentLanguage);
        Reader = new IosResourceReader();
        Writer = new IosResourceWriter(stringsFileName);
        Validator = new IosResourceValidator(stringsFileName, developmentLanguage);
    }

    /// <inheritdoc />
    public bool CanHandle(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // Check for *.lproj folders with .strings or .stringsdict files
        return Directory.GetDirectories(path, "*.lproj")
            .Any(d => File.Exists(Path.Combine(d, _stringsFileName)) ||
                      File.Exists(Path.Combine(d, _stringsdictFileName)));
    }
}
