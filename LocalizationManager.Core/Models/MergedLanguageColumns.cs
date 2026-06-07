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
/// A single display column after merging the files of one resource group by
/// effective language code. When two files map to the same code (e.g. the
/// suffix-less default file labeled with the configured DefaultLanguageCode and
/// an explicit culture file with the same code), they collapse into one column;
/// the default file's value wins and the collision is surfaced via
/// <see cref="HasConflict"/>.
/// </summary>
public sealed class LanguageColumn
{
    /// <summary>Effective code: the file Code, or "default" when blank.</summary>
    public required string Code { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>Path of the file whose value is shown for this column (default wins).</summary>
    public required string WinningFilePath { get; init; }

    /// <summary>True when more than one file mapped to this code.</summary>
    public bool HasConflict { get; init; }

    /// <summary>Paths of the other files that collided with the winner (excludes the winner).</summary>
    public IReadOnlyList<string> ConflictingFilePaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Merges a resource group's files into display columns. Shared by the web API,
/// TUI and cloud so column semantics are identical everywhere.
/// </summary>
public static class MergedLanguageColumns
{
    public static string EffectiveCode(LanguageInfo f) => f.GetDisplayCode();

    public static IReadOnlyList<LanguageColumn> Build(IEnumerable<LanguageInfo> files)
    {
        var columns = new List<LanguageColumn>();

        foreach (var grp in files.GroupBy(EffectiveCode, StringComparer.OrdinalIgnoreCase))
        {
            // Default file wins; otherwise first file in the group.
            var ordered = grp.OrderByDescending(f => f.IsDefault).ToList();
            var winner = ordered[0];
            var losers = ordered.Skip(1).Select(f => f.FilePath ?? string.Empty).ToList();

            columns.Add(new LanguageColumn
            {
                Code = grp.Key,
                Name = winner.Name,
                IsDefault = ordered.Any(f => f.IsDefault),
                WinningFilePath = winner.FilePath ?? string.Empty,
                HasConflict = ordered.Count > 1,
                ConflictingFilePaths = losers
            });
        }

        // default column first, then alphabetical
        return columns
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
