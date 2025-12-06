// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// JSON implementation of resource validator.
/// Uses the format-agnostic ResourceValidator for validation logic.
/// </summary>
public class JsonResourceValidator : IResourceValidator
{
    private readonly ResourceValidator _inner = new();
    private readonly JsonResourceDiscovery _discovery;
    private readonly JsonResourceReader _reader;

    public JsonResourceValidator(JsonFormatConfiguration? config = null)
    {
        _discovery = new JsonResourceDiscovery(config);
        _reader = new JsonResourceReader(config);
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
        // Validate a single file by creating a list with just that file
        var result = _inner.Validate(new List<ResourceFile> { file });
        return Task.FromResult(result);
    }
}
