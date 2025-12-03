// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Reflection;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Loads JSON localization resources embedded in an assembly.
/// </summary>
public class EmbeddedResourceLoader : IResourceLoader
{
    private readonly Assembly _assembly;
    private readonly string _resourceNamespace;
    private readonly string[] _manifestResourceNames;

    /// <summary>
    /// Creates an embedded resource loader for the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="resourceNamespace">The namespace prefix for resources (e.g., "MyApp.Resources").</param>
    public EmbeddedResourceLoader(Assembly assembly, string resourceNamespace)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _resourceNamespace = resourceNamespace ?? throw new ArgumentNullException(nameof(resourceNamespace));
        _manifestResourceNames = assembly.GetManifestResourceNames();
    }

    /// <summary>
    /// Creates an embedded resource loader for the calling assembly.
    /// </summary>
    /// <param name="resourceNamespace">The namespace prefix for resources.</param>
    public EmbeddedResourceLoader(string resourceNamespace)
        : this(Assembly.GetCallingAssembly(), resourceNamespace)
    {
    }

    /// <inheritdoc />
    public Stream? GetResourceStream(string baseName, string culture)
    {
        var resourceName = GetResourceName(baseName, culture);
        return _assembly.GetManifestResourceStream(resourceName);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures(string baseName)
    {
        var prefix = $"{_resourceNamespace}.{baseName}";
        var suffix = ".json";

        foreach (var resourceName in _manifestResourceNames)
        {
            if (!resourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var middle = resourceName.Substring(prefix.Length, resourceName.Length - prefix.Length - suffix.Length);

            if (string.IsNullOrEmpty(middle))
            {
                // Default culture: prefix + ".json"
                yield return "";
            }
            else if (middle.StartsWith("."))
            {
                // Culture-specific: prefix + ".culture.json"
                yield return middle.Substring(1);
            }
        }
    }

    /// <summary>
    /// Gets the full resource name for a resource with the specified culture.
    /// </summary>
    private string GetResourceName(string baseName, string culture)
    {
        return string.IsNullOrEmpty(culture)
            ? $"{_resourceNamespace}.{baseName}.json"
            : $"{_resourceNamespace}.{baseName}.{culture}.json";
    }
}
