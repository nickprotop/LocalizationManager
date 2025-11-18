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

using LocalizationManager.Core.Scanning.Models;

namespace LocalizationManager.Core.Models;

/// <summary>
/// Information about code usage of a duplicate key and its case variants.
/// </summary>
public class DuplicateKeyCodeUsage
{
    /// <summary>
    /// The normalized key (lowercase) used for grouping.
    /// </summary>
    public string NormalizedKey { get; set; } = string.Empty;

    /// <summary>
    /// All case variants of this key found in resource files.
    /// </summary>
    public List<string> ResourceVariants { get; set; } = new();

    /// <summary>
    /// Code references for each case variant.
    /// Dictionary: Exact Key -> List of file:line references
    /// </summary>
    public Dictionary<string, List<KeyReference>> CodeReferences { get; set; } = new();

    /// <summary>
    /// Whether code scanning was performed.
    /// </summary>
    public bool CodeScanned { get; set; }

    /// <summary>
    /// Gets the total number of code references across all variants.
    /// </summary>
    public int TotalCodeReferences => CodeReferences.Sum(kvp => kvp.Value.Count);

    /// <summary>
    /// Gets the variants that have code references.
    /// </summary>
    public List<string> UsedVariants => CodeReferences
        .Where(kvp => kvp.Value.Any())
        .Select(kvp => kvp.Key)
        .ToList();

    /// <summary>
    /// Gets the variants that have no code references.
    /// </summary>
    public List<string> UnusedVariants => ResourceVariants
        .Where(v => !CodeReferences.ContainsKey(v) || !CodeReferences[v].Any())
        .ToList();
}
