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

namespace LocalizationManager.Core.Models;

/// <summary>
/// Represents metadata about a detected language resource file.
/// </summary>
public class LanguageInfo
{
    /// <summary>
    /// Base name of the resource file (e.g., "SharedResource").
    /// </summary>
    public required string BaseName { get; set; }

    /// <summary>
    /// Culture code (e.g., "en", "el", "fr").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name of the language (e.g., "English (en)", "Greek (el)").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Indicates if this is the default language (no culture suffix).
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Full file path to the .resx file.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Gets a display-friendly language code.
    /// Returns "default" for the default language (empty code), otherwise returns the code.
    /// </summary>
    public string GetDisplayCode()
    {
        return string.IsNullOrEmpty(Code) ? "default" : Code;
    }
}
