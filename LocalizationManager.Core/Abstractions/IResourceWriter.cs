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
/// Writes resource files to storage.
/// </summary>
public interface IResourceWriter
{
    /// <summary>
    /// Write a ResourceFile back to storage.
    /// </summary>
    /// <param name="file">Resource file to write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteAsync(ResourceFile file, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version for backward compatibility.
    /// </summary>
    /// <param name="file">Resource file to write.</param>
    void Write(ResourceFile file);

    /// <summary>
    /// Create a new language file.
    /// </summary>
    /// <param name="baseName">Base name of the resource file (e.g., "Resources").</param>
    /// <param name="cultureCode">Culture code for the new language (e.g., "fr", "de").</param>
    /// <param name="targetPath">Directory path for the new file.</param>
    /// <param name="sourceFile">Optional source file to copy entries from.</param>
    /// <param name="copyEntries">Whether to copy entries from source file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateLanguageFileAsync(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a language file.
    /// </summary>
    /// <param name="language">Language information for the file to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default);
}
