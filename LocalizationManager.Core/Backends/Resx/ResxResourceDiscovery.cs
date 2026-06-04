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

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX implementation of resource discovery.
/// Auto-discovers .resx files and their associated languages in a specified directory.
/// </summary>
public class ResxResourceDiscovery : IResourceDiscovery
{
    private readonly string? _defaultLanguageCode;

    /// <summary>
    /// Creates a RESX discovery instance.
    /// </summary>
    /// <param name="defaultLanguageCode">
    /// The default/source language code from configuration (e.g. "it"). When set, the
    /// suffix-less default file (e.g. CustomerResources.resx) is reported with this code
    /// instead of an empty string, so every client labels it as the real language rather
    /// than guessing "English". When null, the default file keeps an empty code.
    /// </param>
    public ResxResourceDiscovery(string? defaultLanguageCode = null)
    {
        _defaultLanguageCode = string.IsNullOrWhiteSpace(defaultLanguageCode) ? null : defaultLanguageCode;
    }

    /// <inheritdoc />
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        if (!Directory.Exists(searchPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {searchPath}");
        }

        var languages = new List<LanguageInfo>();

        // Find all .resx files recursively (exclude .Designer.cs files and well-known
        // non-source directories like backups/build output). Files may live in nested
        // subfolders of the resource path (e.g. Resources/Components/.../Login.it.resx).
        var resxFiles = Directory.GetFiles(searchPath, "*.resx", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !IsInExcludedDirectory(f, searchPath))
            .ToList();

        if (!resxFiles.Any())
        {
            return languages;
        }

        // Group by directory + base name so unrelated files that share a base name in
        // different subfolders (e.g. Customers/Resources.resx and Glass/Resources.resx)
        // are kept as separate resource groups rather than merged.
        var groups = resxFiles
            .GroupBy(f => GetGroupKey(f))
            .ToList();

        foreach (var group in groups)
        {
            var baseName = GetBaseName(group.First());

            foreach (var file in group)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('.');

                if (parts.Length == 1)
                {
                    // Default language (e.g., SharedResource.resx). Adopt the configured
                    // default language code when available; otherwise leave it blank.
                    languages.Add(new LanguageInfo
                    {
                        BaseName = baseName,
                        Code = _defaultLanguageCode ?? "",
                        Name = _defaultLanguageCode != null ? GetCultureName(_defaultLanguageCode) : "Default",
                        IsDefault = true,
                        FilePath = file
                    });
                }
                else if (parts.Length >= 2)
                {
                    // Culture-specific (e.g., SharedResource.el.resx)
                    var cultureCode = parts[^1]; // Last part

                    languages.Add(new LanguageInfo
                    {
                        BaseName = baseName,
                        Code = cultureCode,
                        Name = GetCultureName(cultureCode),
                        IsDefault = false,
                        FilePath = file
                    });
                }
            }
        }

        // Sort: default language first, then alphabetically
        return languages
            .OrderBy(l => l.BaseName)
            .ThenBy(l => l.IsDefault ? 0 : 1)
            .ThenBy(l => l.Code)
            .ToList();
    }

    /// <inheritdoc />
    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(string searchPath, CancellationToken ct = default)
        => Task.FromResult(DiscoverLanguages(searchPath));

    /// <summary>
    /// Extracts the base name from a file path (without culture code or extension).
    /// </summary>
    private static string GetBaseName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Return the first part before any dots
        return fileName.Split('.')[0];
    }

    /// <summary>
    /// Builds a grouping key from the containing directory and base name, so that files
    /// sharing a base name in different subfolders form separate resource groups.
    /// </summary>
    private static string GetGroupKey(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        return $"{directory}{Path.DirectorySeparatorChar}{GetBaseName(filePath)}";
    }

    /// <summary>
    /// Directories that should never be treated as resource sources when recursing
    /// (backups, build output, VCS and package directories).
    /// </summary>
    private static readonly string[] ExcludedDirectories =
        { ".lrm", ".backups", "bin", "obj", ".git", "node_modules" };

    /// <summary>
    /// Returns true if the file lives under an excluded directory relative to the search root.
    /// </summary>
    private static bool IsInExcludedDirectory(string filePath, string searchPath)
    {
        var relative = Path.GetRelativePath(searchPath, filePath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // All segments except the final file name are directory names.
        return segments.Take(segments.Length - 1)
            .Any(seg => ExcludedDirectories.Contains(seg, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the display name for a culture code using .NET CultureInfo.
    /// </summary>
    private static string GetCultureName(string code)
    {
        try
        {
            var culture = new CultureInfo(code);
            return $"{culture.NativeName} ({code})";
        }
        catch
        {
            // Fallback for invalid culture codes
            return code.ToUpper();
        }
    }
}
