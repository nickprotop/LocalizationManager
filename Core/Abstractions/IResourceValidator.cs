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
/// Validates resource files for issues (missing translations, duplicates, etc.)
/// </summary>
public interface IResourceValidator
{
    /// <summary>
    /// Validate all resource files in the path.
    /// </summary>
    /// <param name="searchPath">Path containing resource files.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result with all detected issues.</returns>
    Task<ValidationResult> ValidateAsync(
        string searchPath,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous version for backward compatibility.
    /// </summary>
    /// <param name="searchPath">Path containing resource files.</param>
    /// <returns>Validation result with all detected issues.</returns>
    ValidationResult Validate(string searchPath);

    /// <summary>
    /// Validate a single resource file.
    /// </summary>
    /// <param name="file">Resource file to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result for the file.</returns>
    Task<ValidationResult> ValidateFileAsync(
        ResourceFile file,
        CancellationToken ct = default);
}
