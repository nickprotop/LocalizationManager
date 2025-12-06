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

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX backend implementation.
/// Provides access to RESX-specific discovery, reading, writing, and validation.
/// </summary>
public class ResxResourceBackend : IResourceBackend
{
    /// <inheritdoc />
    public string Name => "resx";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".resx" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    public ResxResourceBackend()
    {
        Discovery = new ResxResourceDiscovery();
        Reader = new ResxResourceReader();
        Writer = new ResxResourceWriter();
        Validator = new ResxResourceValidator();
    }
}
