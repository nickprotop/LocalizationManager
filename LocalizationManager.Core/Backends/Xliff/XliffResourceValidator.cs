// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// XLIFF implementation of resource validator.
/// Validates XLIFF files for missing translations, XML structure issues,
/// and other XLIFF-specific rules.
/// </summary>
public class XliffResourceValidator : IResourceValidator
{
    private readonly ResourceValidator _inner = new();
    private readonly XliffResourceDiscovery _discovery;
    private readonly XliffResourceReader _reader;

    public XliffResourceValidator(XliffFormatConfiguration? config = null)
    {
        _discovery = new XliffResourceDiscovery(config);
        _reader = new XliffResourceReader(config);
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
