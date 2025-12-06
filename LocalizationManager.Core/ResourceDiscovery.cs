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
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core;

/// <summary>
/// Auto-discovers resource files and languages in a specified directory.
/// </summary>
public class ResourceDiscovery
{
    /// <summary>
    /// Discovers all .resx files and their associated languages in the specified path.
    /// </summary>
    /// <param name="searchPath">Path to search for .resx files.</param>
    /// <returns>List of discovered language information.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the search path doesn't exist.</exception>
    public List<LanguageInfo> DiscoverLanguages(string searchPath)
    {
        if (!Directory.Exists(searchPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {searchPath}");
        }

        var languages = new List<LanguageInfo>();

        // Find all .resx files (exclude .Designer.cs files)
        var resxFiles = Directory.GetFiles(searchPath, "*.resx", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!resxFiles.Any())
        {
            return languages;
        }

        // Group by base name (e.g., "SharedResource" from "SharedResource.resx" and "SharedResource.el.resx")
        var groups = resxFiles
            .GroupBy(f => GetBaseName(f))
            .ToList();

        foreach (var group in groups)
        {
            var baseName = group.Key;

            foreach (var file in group)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('.');

                if (parts.Length == 1)
                {
                    // Default language (e.g., SharedResource.resx)
                    languages.Add(new LanguageInfo
                    {
                        BaseName = baseName,
                        Code = "",
                        Name = "Default",
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

    /// <summary>
    /// Extracts the base name from a file path (without culture code or extension).
    /// </summary>
    private string GetBaseName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Return the first part before any dots
        return fileName.Split('.')[0];
    }

    /// <summary>
    /// Gets the display name for a culture code using .NET CultureInfo.
    /// </summary>
    private string GetCultureName(string code)
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
