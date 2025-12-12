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

using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Abstractions;

/// <summary>
/// Reads resource files and parses them into ResourceFile objects.
/// </summary>
public interface IResourceReader
{
    /// <summary>
    /// Parse a resource file into a ResourceFile object.
    /// </summary>
    /// <param name="language">Language information including file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed resource file.</returns>
    Task<ResourceFile> ReadAsync(
        LanguageInfo language,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous version for backward compatibility.
    /// </summary>
    /// <param name="language">Language information including file path.</param>
    /// <returns>Parsed resource file.</returns>
    ResourceFile Read(LanguageInfo language);

    /// <summary>
    /// Parse resource content from a TextReader (stream-based, no file access).
    /// </summary>
    /// <param name="reader">TextReader containing the resource content.</param>
    /// <param name="metadata">Language metadata (FilePath not required).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed resource file.</returns>
    Task<ResourceFile> ReadAsync(
        TextReader reader,
        LanguageInfo metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous stream-based parsing.
    /// </summary>
    /// <param name="reader">TextReader containing the resource content.</param>
    /// <param name="metadata">Language metadata (FilePath not required).</param>
    /// <returns>Parsed resource file.</returns>
    ResourceFile Read(TextReader reader, LanguageInfo metadata);
}
