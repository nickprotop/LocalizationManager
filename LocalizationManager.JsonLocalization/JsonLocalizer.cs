// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using LocalizationManager.JsonLocalization.Core;
using LocalizationManager.JsonLocalization.Core.Models;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Standalone JSON localizer for accessing localized strings.
/// Supports file system and embedded resources, pluralization, and culture fallback.
/// </summary>
public class JsonLocalizer : IDisposable
{
    private readonly IResourceLoader _loader;
    private readonly JsonResourceReader _reader;
    private readonly string _baseName;
    private readonly JsonFormatConfiguration _config;
    private readonly Dictionary<string, ResourceFile> _cache = new();
    private readonly object _cacheLock = new();

    private CultureInfo _culture;
    private bool _disposed;

    /// <summary>
    /// Creates a JsonLocalizer using file system resources.
    /// </summary>
    /// <param name="resourcesPath">Path to the directory containing JSON resource files.</param>
    /// <param name="baseName">Base name of the resource files (default: "strings").</param>
    /// <param name="config">Optional JSON format configuration.</param>
    public JsonLocalizer(string resourcesPath, string baseName = "strings", JsonFormatConfiguration? config = null)
        : this(new FileSystemResourceLoader(resourcesPath), baseName, config)
    {
    }

    /// <summary>
    /// Creates a JsonLocalizer using embedded resources from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="resourceNamespace">The namespace prefix for resources.</param>
    /// <param name="baseName">Base name of the resource files (default: "strings").</param>
    /// <param name="config">Optional JSON format configuration.</param>
    public JsonLocalizer(Assembly assembly, string resourceNamespace, string baseName = "strings", JsonFormatConfiguration? config = null)
        : this(new EmbeddedResourceLoader(assembly, resourceNamespace), baseName, config)
    {
    }

    /// <summary>
    /// Creates a JsonLocalizer using a custom resource loader.
    /// </summary>
    /// <param name="loader">The resource loader to use.</param>
    /// <param name="baseName">Base name of the resource files (default: "strings").</param>
    /// <param name="config">Optional JSON format configuration.</param>
    public JsonLocalizer(IResourceLoader loader, string baseName = "strings", JsonFormatConfiguration? config = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _baseName = baseName ?? "strings";
        _config = config ?? new JsonFormatConfiguration();
        _reader = new JsonResourceReader(_config);
        _culture = CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Gets or sets the current culture for localization.
    /// </summary>
    public CultureInfo Culture
    {
        get => _culture;
        set => _culture = value ?? CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The localized string, or the key if not found.</returns>
    public string this[string key] => GetString(key);

    /// <summary>
    /// Gets a localized string by key with format arguments.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted localized string, or the key if not found.</returns>
    public string this[string key, params object[] args] => GetString(key, args);

    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The localized string, or the key if not found.</returns>
    public string GetString(string key)
    {
        return GetString(key, _culture);
    }

    /// <summary>
    /// Gets a localized string by key with format arguments.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted localized string, or the key if not found.</returns>
    public string GetString(string key, params object[] args)
    {
        var value = GetString(key);
        if (value == key || args.Length == 0)
            return value;

        try
        {
            return string.Format(_culture, value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    /// <summary>
    /// Gets a localized string by key for a specific culture.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="culture">The culture to use.</param>
    /// <returns>The localized string, or the key if not found.</returns>
    public string GetString(string key, CultureInfo culture)
    {
        var entry = FindEntry(key, culture);
        return entry?.Value ?? key;
    }

    /// <summary>
    /// Gets a pluralized string based on count.
    /// </summary>
    /// <param name="key">The resource key for the plural entry.</param>
    /// <param name="count">The count to determine plural form.</param>
    /// <param name="args">Additional format arguments (count is automatically included as {0}).</param>
    /// <returns>The pluralized and formatted string.</returns>
    public string Plural(string key, int count, params object[] args)
    {
        return Plural(key, count, _culture, args);
    }

    /// <summary>
    /// Gets a pluralized string based on count for a specific culture.
    /// </summary>
    /// <param name="key">The resource key for the plural entry.</param>
    /// <param name="count">The count to determine plural form.</param>
    /// <param name="culture">The culture to use.</param>
    /// <param name="args">Additional format arguments (count is automatically included as {0}).</param>
    /// <returns>The pluralized and formatted string.</returns>
    public string Plural(string key, int count, CultureInfo culture, params object[] args)
    {
        var entry = FindEntry(key, culture);
        if (entry == null)
            return key;

        string? pluralValue = null;

        // Check if this is a plural entry
        if (entry.IsPlural && !string.IsNullOrEmpty(entry.Value))
        {
            try
            {
                var pluralForms = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Value);
                if (pluralForms != null)
                {
                    var form = PluralResolver.GetPluralForm(count, culture);
                    pluralValue = pluralForms.GetValueOrDefault(form)
                               ?? pluralForms.GetValueOrDefault(PluralResolver.Categories.Other)
                               ?? entry.Value;
                }
            }
            catch (JsonException)
            {
                pluralValue = entry.Value;
            }
        }
        else
        {
            pluralValue = entry.Value ?? key;
        }

        // Format with count as first argument
        var allArgs = new object[args.Length + 1];
        allArgs[0] = count;
        Array.Copy(args, 0, allArgs, 1, args.Length);

        try
        {
            return string.Format(culture, pluralValue ?? key, allArgs);
        }
        catch (FormatException)
        {
            return pluralValue ?? key;
        }
    }

    /// <summary>
    /// Gets all available culture codes for this localizer.
    /// </summary>
    public IEnumerable<string> AvailableCultures => _loader.GetAvailableCultures(_baseName);

    /// <summary>
    /// Gets all localized strings for the current culture.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetAllStrings()
    {
        return GetAllStrings(_culture);
    }

    /// <summary>
    /// Gets all localized strings for a specific culture.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetAllStrings(CultureInfo culture)
    {
        var file = GetResourceFile(culture.Name);
        if (file == null)
            yield break;

        foreach (var entry in file.Entries)
        {
            yield return new KeyValuePair<string, string>(entry.Key, entry.Value ?? entry.Key);
        }
    }

    /// <summary>
    /// Clears the resource cache, forcing resources to be reloaded.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }

    /// <summary>
    /// Finds a resource entry with culture fallback.
    /// </summary>
    private ResourceEntry? FindEntry(string key, CultureInfo culture)
    {
        // Try exact culture (e.g., "en-US")
        var file = GetResourceFile(culture.Name);
        var entry = file?.FindEntry(key);
        if (entry != null)
            return entry;

        // Try parent culture (e.g., "en")
        if (culture.Parent != CultureInfo.InvariantCulture)
        {
            file = GetResourceFile(culture.Parent.Name);
            entry = file?.FindEntry(key);
            if (entry != null)
                return entry;
        }

        // Try default culture
        file = GetResourceFile("");
        return file?.FindEntry(key);
    }

    /// <summary>
    /// Gets or loads a resource file for the specified culture.
    /// </summary>
    private ResourceFile? GetResourceFile(string culture)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(culture, out var cached))
                return cached;

            var stream = _loader.GetResourceStream(_baseName, culture);
            if (stream == null)
                return null;

            using (stream)
            {
                var language = new LanguageInfo
                {
                    BaseName = _baseName,
                    Code = culture,
                    Name = string.IsNullOrEmpty(culture) ? "Default" : culture,
                    IsDefault = string.IsNullOrEmpty(culture),
                    FilePath = $"{_baseName}.{(string.IsNullOrEmpty(culture) ? "" : culture + ".")}json"
                };

                var file = _reader.Read(stream, language);
                _cache[culture] = file;
                return file;
            }
        }
    }

    /// <summary>
    /// Disposes the localizer and clears the cache.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ClearCache();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
