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
using LocalizationManager.Core.Backends.Android;
using LocalizationManager.Core.Backends.iOS;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Po;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Backends.Xliff;
using LocalizationManager.Core.Configuration;

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
        ["json"] = () => new JsonResourceBackend(),
        ["android"] = () => new AndroidResourceBackend(),
        ["ios"] = () => new IosResourceBackend(),
        ["po"] = () => new PoResourceBackend(),
        ["xliff"] = () => new XliffResourceBackend()
    };

    /// <inheritdoc />
    public IResourceBackend GetBackend(string name)
    {
        return GetBackend(name, null);
    }

    /// <inheritdoc />
    public IResourceBackend GetBackend(string name, ConfigurationModel? config)
    {
        var lowerName = name.ToLowerInvariant();

        return lowerName switch
        {
            "resx" => new ResxResourceBackend(),
            "json" or "jsonlocalization" => new JsonResourceBackend(config?.Json),
            "i18next" => new JsonResourceBackend(config?.Json ?? new Configuration.JsonFormatConfiguration { I18nextCompatible = true }),
            "android" => new AndroidResourceBackend(
                (config?.Android?.BaseName ?? "strings") + ".xml",
                config?.DefaultLanguageCode),
            "ios" => new IosResourceBackend(
                (config?.Ios?.BaseName ?? "Localizable") + ".strings",
                config?.DefaultLanguageCode),
            "po" or "gettext" => new PoResourceBackend(config?.Po, config?.DefaultLanguageCode),
            "xliff" or "xlf" => new XliffResourceBackend(config?.Xliff),
            _ => throw new NotSupportedException(
                $"Backend '{name}' is not supported. Available: {string.Join(", ", _backends.Keys)}")
        };
    }

    /// <inheritdoc />
    public IResourceBackend ResolveFromPath(string path)
    {
        return ResolveFromPath(path, null);
    }

    /// <inheritdoc />
    public IResourceBackend ResolveFromPath(string path, ConfigurationModel? config)
    {
        if (!Directory.Exists(path))
            return GetBackend("resx", config); // Default fallback

        // Priority order: most specific first (Android/iOS before PO/XLIFF/JSON/RESX)
        // PO and XLIFF are before JSON because some projects may have JSON config files
        // JSON backend needs special handling for auto-detection of i18next format
        var priorities = new[] { "android", "ios", "po", "xliff", "json", "resx" };

        foreach (var name in priorities)
        {
            var backend = GetBackend(name, config);
            if (backend.CanHandle(path))
            {
                // For PO, recreate with path for auto-detection of folder structure
                if (name == "po")
                    return new PoResourceBackend(path, config?.DefaultLanguageCode);

                // For XLIFF, recreate with path for auto-detection of version/format
                if (name == "xliff")
                    return new XliffResourceBackend(path);

                // For JSON, recreate with path for auto-detection of i18next format
                if (name == "json")
                    return new JsonResourceBackend(path);

                return backend;
            }
        }

        // Default to RESX for backward compatibility
        return GetBackend("resx", config);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableBackends() => _backends.Keys;

    /// <inheritdoc />
    public bool IsBackendAvailable(string name) => _backends.ContainsKey(name);
}
