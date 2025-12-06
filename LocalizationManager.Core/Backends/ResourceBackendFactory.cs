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

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;

namespace LocalizationManager.Core.Backends;

/// <summary>
/// Factory for creating resource backends.
/// Supports auto-detection based on existing files.
/// </summary>
public class ResourceBackendFactory : IResourceBackendFactory
{
    private readonly Dictionary<string, Func<IResourceBackend>> _backends = new(StringComparer.OrdinalIgnoreCase)
    {
        ["resx"] = () => new ResxResourceBackend(),
        ["json"] = () => new JsonResourceBackend()
    };

    /// <inheritdoc />
    public IResourceBackend GetBackend(string name)
    {
        if (_backends.TryGetValue(name, out var factory))
            return factory();

        throw new NotSupportedException(
            $"Backend '{name}' is not supported. Available: {string.Join(", ", _backends.Keys)}");
    }

    /// <inheritdoc />
    public IResourceBackend ResolveFromPath(string path)
    {
        // Check for existing files
        if (Directory.Exists(path))
        {
            // Check for JSON resource files (exclude lrm*.json config files)
            var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase));

            if (jsonFiles.Any() && _backends.ContainsKey("json"))
                return new JsonResourceBackend(path);  // Pass path for auto-detection

            if (Directory.GetFiles(path, "*.resx", SearchOption.TopDirectoryOnly).Any())
                return GetBackend("resx");
        }

        // Default to RESX for backward compatibility
        return GetBackend("resx");
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableBackends() => _backends.Keys;

    /// <inheritdoc />
    public bool IsBackendAvailable(string name) => _backends.ContainsKey(name);
}
