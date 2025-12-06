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

namespace LocalizationManager.Core.Abstractions;

/// <summary>
/// Main facade for resource file backends (RESX, JSON, etc.)
/// </summary>
public interface IResourceBackend
{
    /// <summary>
    /// Backend identifier (e.g., "resx", "json")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Supported file extensions (e.g., ".resx", ".json")
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Discovery service for finding resource files
    /// </summary>
    IResourceDiscovery Discovery { get; }

    /// <summary>
    /// Reader for parsing resource files
    /// </summary>
    IResourceReader Reader { get; }

    /// <summary>
    /// Writer for saving resource files
    /// </summary>
    IResourceWriter Writer { get; }

    /// <summary>
    /// Validator for checking resource files
    /// </summary>
    IResourceValidator Validator { get; }
}
