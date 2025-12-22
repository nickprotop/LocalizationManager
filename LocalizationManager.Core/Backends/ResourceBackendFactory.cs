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
using LocalizationManager.Core.Backends.Resx;
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
        ["ios"] = () => new IosResourceBackend()
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
        // Check for existing files
        if (Directory.Exists(path))
        {
            // Check for Android project structure (res/values/strings.xml)
            if (IsAndroidProject(path))
                return GetBackend("android", config);

            // Check for iOS project structure (*.lproj/Localizable.strings)
            if (IsIosProject(path))
                return GetBackend("ios", config);

            // Check for JSON resource files (exclude lrm*.json config files)
            var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("lrm", StringComparison.OrdinalIgnoreCase));

            if (jsonFiles.Any() && _backends.ContainsKey("json"))
                return new JsonResourceBackend(path);  // Pass path for auto-detection

            if (Directory.GetFiles(path, "*.resx", SearchOption.TopDirectoryOnly).Any())
                return GetBackend("resx", config);
        }

        // Default to RESX for backward compatibility
        return GetBackend("resx", config);
    }

    /// <summary>
    /// Checks if the path contains an Android project structure.
    /// </summary>
    private static bool IsAndroidProject(string path)
    {
        // Check if path is already the res folder
        if (Path.GetFileName(path).Equals("res", StringComparison.OrdinalIgnoreCase))
        {
            return HasAndroidValuesFolder(path);
        }

        // Check for res/values*/strings.xml
        var resPath = Path.Combine(path, "res");
        if (Directory.Exists(resPath) && HasAndroidValuesFolder(resPath))
            return true;

        // Check for app/src/main/res structure (standard Android)
        var mainResPath = Path.Combine(path, "app", "src", "main", "res");
        if (Directory.Exists(mainResPath) && HasAndroidValuesFolder(mainResPath))
            return true;

        // Check for src/main/res structure (module)
        var srcMainResPath = Path.Combine(path, "src", "main", "res");
        if (Directory.Exists(srcMainResPath) && HasAndroidValuesFolder(srcMainResPath))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if the res folder contains any values folder with strings.xml.
    /// </summary>
    private static bool HasAndroidValuesFolder(string resPath)
    {
        // Check for values/ folder (default language)
        if (File.Exists(Path.Combine(resPath, "values", "strings.xml")))
            return true;

        // Check for any values-* folder (language-specific)
        try
        {
            foreach (var dir in Directory.GetDirectories(resPath, "values*"))
            {
                if (File.Exists(Path.Combine(dir, "strings.xml")))
                    return true;
            }
        }
        catch
        {
            // Ignore directory access errors
        }

        return false;
    }

    /// <summary>
    /// Checks if the path contains an iOS project structure.
    /// </summary>
    private static bool IsIosProject(string path)
    {
        // Check for *.lproj folders with Localizable.strings
        var lprojFolders = Directory.GetDirectories(path, "*.lproj");
        foreach (var folder in lprojFolders)
        {
            if (File.Exists(Path.Combine(folder, "Localizable.strings")) ||
                File.Exists(Path.Combine(folder, "Localizable.stringsdict")))
            {
                return true;
            }
        }

        // Check Resources subfolder
        var resourcesPath = Path.Combine(path, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            foreach (var folder in Directory.GetDirectories(resourcesPath, "*.lproj"))
            {
                if (File.Exists(Path.Combine(folder, "Localizable.strings")) ||
                    File.Exists(Path.Combine(folder, "Localizable.stringsdict")))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableBackends() => _backends.Keys;

    /// <inheritdoc />
    public bool IsBackendAvailable(string name) => _backends.ContainsKey(name);
}
