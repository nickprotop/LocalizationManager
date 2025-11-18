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
/// Represents the result of a resource file validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Keys that are present in the default language but missing in translations.
    /// Dictionary: Language Code -> List of Missing Keys
    /// </summary>
    public Dictionary<string, List<string>> MissingKeys { get; set; } = new();

    /// <summary>
    /// Keys that exist in translations but not in the default language.
    /// Dictionary: Language Code -> List of Extra Keys
    /// </summary>
    public Dictionary<string, List<string>> ExtraKeys { get; set; } = new();

    /// <summary>
    /// Duplicate keys found within the same file.
    /// Dictionary: Language Code -> List of Duplicate Keys
    /// </summary>
    public Dictionary<string, List<string>> DuplicateKeys { get; set; } = new();

    /// <summary>
    /// Keys with empty or null values.
    /// Dictionary: Language Code -> List of Empty Keys
    /// </summary>
    public Dictionary<string, List<string>> EmptyValues { get; set; } = new();

    /// <summary>
    /// Keys with placeholder mismatches between source and translation.
    /// Dictionary: Language Code -> Dictionary of Key -> Error Message
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> PlaceholderMismatches { get; set; } = new();

    /// <summary>
    /// Code usage information for duplicate keys.
    /// Dictionary: Duplicate Key (lowercase) -> DuplicateKeyCodeUsage
    /// </summary>
    public Dictionary<string, DuplicateKeyCodeUsage> DuplicateKeyCodeUsages { get; set; } = new();

    /// <summary>
    /// Indicates if the validation passed without any issues.
    /// </summary>
    public bool IsValid =>
        !MissingKeys.Any(kv => kv.Value.Any()) &&
        !ExtraKeys.Any(kv => kv.Value.Any()) &&
        !DuplicateKeys.Any(kv => kv.Value.Any()) &&
        !EmptyValues.Any(kv => kv.Value.Any()) &&
        !PlaceholderMismatches.Any(kv => kv.Value.Any());

    /// <summary>
    /// Gets the total number of issues found.
    /// </summary>
    public int TotalIssues =>
        MissingKeys.Sum(kv => kv.Value.Count) +
        ExtraKeys.Sum(kv => kv.Value.Count) +
        DuplicateKeys.Sum(kv => kv.Value.Count) +
        EmptyValues.Sum(kv => kv.Value.Count) +
        PlaceholderMismatches.Sum(kv => kv.Value.Count);
}
