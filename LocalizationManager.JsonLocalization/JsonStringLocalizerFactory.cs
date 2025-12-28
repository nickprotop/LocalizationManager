// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Collections.Concurrent;
using System.Reflection;
using LocalizationManager.JsonLocalization.Core;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Factory for creating JsonStringLocalizer instances.
/// </summary>
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonLocalizationOptions? _options;
    private readonly IResourceLoader? _customLoader;
    private readonly string? _customBaseName;
    private readonly JsonFormatConfiguration? _customConfig;
    private readonly ConcurrentDictionary<string, JsonLocalizer> _localizerCache = new();

    /// <summary>
    /// Creates a new JsonStringLocalizerFactory with the specified options.
    /// </summary>
    public JsonStringLocalizerFactory(IOptions<JsonLocalizationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates a new JsonStringLocalizerFactory with a custom resource loader (for OTA support).
    /// </summary>
    /// <param name="loader">The resource loader to use.</param>
    /// <param name="baseName">Base name for resources.</param>
    /// <param name="config">JSON format configuration.</param>
    public JsonStringLocalizerFactory(IResourceLoader loader, string baseName, JsonFormatConfiguration config)
    {
        _customLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        _customBaseName = baseName ?? "strings";
        _customConfig = config ?? new JsonFormatConfiguration();
    }

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource)
    {
        if (resourceSource == null)
            throw new ArgumentNullException(nameof(resourceSource));

        var assembly = resourceSource.Assembly;
        var baseName = GetBaseName(resourceSource);

        return CreateLocalizer(baseName, assembly);
    }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location)
    {
        if (string.IsNullOrEmpty(baseName))
            throw new ArgumentNullException(nameof(baseName));

        // Try to get assembly from location
        Assembly? assembly = null;
        if (!string.IsNullOrEmpty(location))
        {
            try
            {
                assembly = Assembly.Load(new AssemblyName(location));
            }
            catch
            {
                // Fall back to entry assembly
            }
        }

        assembly ??= _options?.ResourceAssembly ?? Assembly.GetEntryAssembly();

        return CreateLocalizer(baseName, assembly);
    }

    /// <summary>
    /// Creates or retrieves a cached JsonStringLocalizer.
    /// </summary>
    private IStringLocalizer CreateLocalizer(string baseName, Assembly? assembly)
    {
        var cacheKey = $"{assembly?.GetName().Name ?? "default"}:{baseName}";

        var localizer = _localizerCache.GetOrAdd(cacheKey, _ =>
        {
            // Use custom loader if provided (OTA mode)
            if (_customLoader != null)
            {
                return new JsonLocalizer(_customLoader, _customBaseName ?? baseName, _customConfig!);
            }

            // Use options-based loader (standard mode)
            IResourceLoader loader;

            if (_options!.UseEmbeddedResources)
            {
                var resourceAssembly = assembly ?? _options.ResourceAssembly ?? Assembly.GetEntryAssembly()!;
                loader = new EmbeddedResourceLoader(resourceAssembly, _options.ResourcesPath);
            }
            else
            {
                // For file system resources, use the application base directory
                // This is more reliable than assembly location, especially for web apps
                var basePath = AppContext.BaseDirectory;
                var resourcesPath = Path.Combine(basePath, _options.ResourcesPath);
                loader = new FileSystemResourceLoader(resourcesPath);
            }

            return new JsonLocalizer(loader, baseName, _options.GetFormatConfiguration());
        });

        return new JsonStringLocalizer(localizer);
    }

    /// <summary>
    /// Gets the base name for a resource source type.
    /// </summary>
    private string GetBaseName(Type resourceSource)
    {
        // Use custom base name if in OTA mode
        if (_customBaseName != null)
        {
            return _customBaseName;
        }

        // Use the configured base name if the type doesn't have a custom one
        var typeName = resourceSource.Name;

        // For generic IStringLocalizer (without type parameter), use the configured base name
        if (resourceSource == typeof(object))
        {
            return _options!.BaseName;
        }

        // Check for common patterns like "HomeController" -> use configured base name
        // This allows sharing a single JSON file across multiple controllers
        if (typeName.EndsWith("Controller") || typeName.EndsWith("Model") || typeName.EndsWith("ViewModel"))
        {
            return _options!.BaseName;
        }

        // Otherwise, use the type name as the base name
        return typeName;
    }
}
