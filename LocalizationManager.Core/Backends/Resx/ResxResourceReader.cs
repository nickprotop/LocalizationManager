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
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX implementation of resource reader.
/// Wraps the existing ResourceFileParser class.
/// </summary>
public class ResxResourceReader : IResourceReader
{
    private readonly ResourceFileParser _parser = new();

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
        => _parser.Parse(language);

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
        => _parser.Parse(reader, metadata);

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
        => Task.FromResult(Read(reader, metadata));
}
