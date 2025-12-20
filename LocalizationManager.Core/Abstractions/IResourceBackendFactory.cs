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

using LocalizationManager.Core.Configuration;

/// <summary>
/// Factory for creating resource backends.
/// </summary>
public interface IResourceBackendFactory
{
    /// <summary>
    /// Get backend by name.
    /// </summary>
    /// <param name="name">Backend name (e.g., "resx", "json").</param>
    /// <returns>The requested backend.</returns>
    /// <exception cref="ArgumentException">Thrown when backend is not found.</exception>
    IResourceBackend GetBackend(string name);

    /// <summary>
    /// Get backend by name with configuration.
    /// </summary>
    /// <param name="name">Backend name (e.g., "resx", "json").</param>
    /// <param name="config">Configuration model with format-specific settings.</param>
    /// <returns>The requested backend configured with the provided settings.</returns>
    /// <exception cref="ArgumentException">Thrown when backend is not found.</exception>
    IResourceBackend GetBackend(string name, ConfigurationModel? config);

    /// <summary>
    /// Auto-detect backend from existing files in path.
    /// </summary>
    /// <param name="path">Path to search for resource files.</param>
    /// <returns>Detected backend or default backend.</returns>
    IResourceBackend ResolveFromPath(string path);

    /// <summary>
    /// Auto-detect backend from existing files in path with configuration.
    /// </summary>
    /// <param name="path">Path to search for resource files.</param>
    /// <param name="config">Configuration model with format-specific settings.</param>
    /// <returns>Detected backend or default backend.</returns>
    IResourceBackend ResolveFromPath(string path, ConfigurationModel? config);

    /// <summary>
    /// List all available backends.
    /// </summary>
    /// <returns>Names of available backends.</returns>
    IEnumerable<string> GetAvailableBackends();

    /// <summary>
    /// Check if a backend is available.
    /// </summary>
    /// <param name="name">Backend name to check.</param>
    /// <returns>True if backend is available.</returns>
    bool IsBackendAvailable(string name);
}
