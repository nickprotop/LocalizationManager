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
/// Represents a single resource key-value pair from a resource file (.resx or JSON).
/// </summary>
public class ResourceEntry
{
    /// <summary>
    /// The resource key name.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The resource value/translation.
    /// For plural entries, this contains the "other" form as the default value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Optional comment associated with the resource entry.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Indicates if this entry represents a plural form (JSON only).
    /// When true, PluralForms contains the different plural variations.
    /// </summary>
    public bool IsPlural { get; set; }

    /// <summary>
    /// Plural form values keyed by CLDR plural category (zero, one, two, few, many, other).
    /// Only populated when IsPlural is true.
    /// </summary>
    public Dictionary<string, string>? PluralForms { get; set; }

    /// <summary>
    /// Source text for plural forms (PO format: msgid_plural).
    /// Used for translation when the source differs from the key.
    /// For non-PO formats, this is typically null as PluralForms contains the source.
    /// </summary>
    public string? SourcePluralText { get; set; }

    /// <summary>
    /// Indicates if this entry is empty/null.
    /// For plural entries, checks if all plural forms are empty.
    /// </summary>
    public bool IsEmpty => IsPlural
        ? PluralForms == null || PluralForms.Count == 0 || PluralForms.Values.All(string.IsNullOrWhiteSpace)
        : string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Gets the effective value for display purposes.
    /// For plural entries, returns the "other" form or first available form.
    /// </summary>
    public string? DisplayValue => IsPlural
        ? PluralForms?.GetValueOrDefault("other") ?? PluralForms?.Values.FirstOrDefault() ?? Value
        : Value;
}
