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
/// Represents a complete .resx resource file with all its entries.
/// </summary>
public class ResourceFile
{
    /// <summary>
    /// Language information for this resource file.
    /// </summary>
    public required LanguageInfo Language { get; set; }

    /// <summary>
    /// Collection of all resource entries in this file.
    /// </summary>
    public List<ResourceEntry> Entries { get; set; } = new();

    /// <summary>
    /// Gets the number of entries in this resource file.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    /// Gets the number of non-empty entries.
    /// </summary>
    public int CompletedCount => Entries.Count(e => !e.IsEmpty);

    /// <summary>
    /// Gets the translation completion percentage.
    /// </summary>
    public double CompletionPercentage => Count > 0 ? (double)CompletedCount / Count * 100 : 0;
}
