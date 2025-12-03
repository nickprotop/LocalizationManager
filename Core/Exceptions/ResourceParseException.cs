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

namespace LocalizationManager.Core.Exceptions;

/// <summary>
/// Thrown when a resource file cannot be parsed (invalid JSON/XML, malformed structure).
/// </summary>
public class ResourceParseException : ResourceException
{
    /// <summary>
    /// Line number where the error occurred, if available.
    /// </summary>
    public int? LineNumber { get; }

    /// <summary>
    /// Character position where the error occurred, if available.
    /// </summary>
    public int? Position { get; }

    public ResourceParseException(
        string message,
        string filePath,
        int? lineNumber = null,
        int? position = null,
        Exception? inner = null)
        : base(message, filePath, inner)
    {
        LineNumber = lineNumber;
        Position = position;
    }

    public override string ToString()
    {
        var location = "";
        if (LineNumber.HasValue)
        {
            location = $" at line {LineNumber}";
            if (Position.HasValue)
            {
                location += $", position {Position}";
            }
        }
        return $"{Message}{location} in {FilePath}";
    }
}
