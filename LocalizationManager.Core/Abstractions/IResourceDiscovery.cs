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

using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Abstractions;

/// <summary>
/// Discovers resource files in a directory.
/// </summary>
public interface IResourceDiscovery
{
    /// <summary>
    /// Discover all language files in the specified path. This is a flat per-file
    /// enumeration: in directories with multiple base names (e.g. CustomerResources.resx
    /// and GlassResources.resx) you will get multiple entries with the same culture code.
    /// Callers that want a count of distinct languages/cultures must use
    /// <see cref="DiscoverResourceGroups"/> instead.
    /// </summary>
    /// <param name="searchPath">Path to search for resource files.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of discovered language files.</returns>
    Task<List<LanguageInfo>> DiscoverLanguagesAsync(
        string searchPath,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous version for backward compatibility.
    /// </summary>
    /// <param name="searchPath">Path to search for resource files.</param>
    /// <returns>List of discovered language files.</returns>
    List<LanguageInfo> DiscoverLanguages(string searchPath);

    /// <summary>
    /// Discover all resource groups in the specified path. A resource group is a
    /// set of files sharing the same base name (e.g. SharedResource.resx,
    /// SharedResource.el.resx). Directories may contain multiple unrelated
    /// groups (e.g. CustomerResources, GlassResources) that share the same set
    /// of cultures.
    /// </summary>
    ResourceDirectory DiscoverResourceGroups(string searchPath)
    {
        var files = DiscoverLanguages(searchPath);
        var groups = files
            .GroupBy(f => f.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ResourceGroup
            {
                BaseName = g.Key,
                Files = g.ToList()
            })
            .ToList();
        var codes = files
            .Select(f => f.Code ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ResourceDirectory { Groups = groups, CultureCodes = codes };
    }

    /// <summary>Async version of <see cref="DiscoverResourceGroups"/>.</summary>
    Task<ResourceDirectory> DiscoverResourceGroupsAsync(string searchPath, CancellationToken ct = default)
        => Task.Run(() => DiscoverResourceGroups(searchPath), ct);
}
