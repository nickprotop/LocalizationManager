// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Android implementation of resource validator.
/// Uses the format-agnostic ResourceValidator for validation logic.
/// </summary>
public class AndroidResourceValidator : IResourceValidator
{
    private readonly ResourceValidator _inner = new();
    private readonly AndroidResourceDiscovery _discovery;
    private readonly AndroidResourceReader _reader;

    /// <summary>
    /// Creates a new Android resource validator.
    /// </summary>
    /// <param name="resourceFileName">The resource file name (default: "strings.xml")</param>
    public AndroidResourceValidator(string resourceFileName = "strings.xml")
    {
        _discovery = new AndroidResourceDiscovery(resourceFileName);
        _reader = new AndroidResourceReader();
    }

    /// <inheritdoc />
    public ValidationResult Validate(string searchPath)
    {
        var languages = _discovery.DiscoverLanguages(searchPath);
        var files = languages.Select(l => _reader.Read(l)).ToList();
        return _inner.Validate(files);
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(Validate(searchPath));

    /// <inheritdoc />
    public Task<ValidationResult> ValidateFileAsync(ResourceFile file, CancellationToken ct = default)
    {
        var result = _inner.Validate(new List<ResourceFile> { file });
        return Task.FromResult(result);
    }
}
