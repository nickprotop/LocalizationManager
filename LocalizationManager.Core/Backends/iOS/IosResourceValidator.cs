// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// iOS implementation of resource validator.
/// Uses the format-agnostic ResourceValidator for validation logic.
/// </summary>
public class IosResourceValidator : IResourceValidator
{
    private readonly ResourceValidator _inner = new();
    private readonly IosResourceDiscovery _discovery;
    private readonly IosResourceReader _reader;

    /// <summary>
    /// Creates a new iOS resource validator.
    /// </summary>
    /// <param name="stringsFileName">The strings file name (default: "Localizable.strings")</param>
    /// <param name="developmentLanguage">The development language for Base.lproj resolution</param>
    public IosResourceValidator(
        string stringsFileName = "Localizable.strings",
        string? developmentLanguage = null)
    {
        _discovery = new IosResourceDiscovery(stringsFileName, developmentLanguage);
        _reader = new IosResourceReader();
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
