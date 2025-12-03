// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using Microsoft.Extensions.Localization;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// IStringLocalizer implementation backed by JSON localization files.
/// </summary>
public class JsonStringLocalizer : IStringLocalizer
{
    private readonly JsonLocalizer _localizer;

    /// <summary>
    /// Creates a new JsonStringLocalizer wrapping the specified JsonLocalizer.
    /// </summary>
    public JsonStringLocalizer(JsonLocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            // Use CurrentUICulture for each lookup to support per-request culture
            var value = _localizer.GetString(name, CultureInfo.CurrentUICulture);
            return new LocalizedString(name, value, resourceNotFound: value == name);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            // Use CurrentUICulture for each lookup to support per-request culture
            var value = _localizer.GetString(name, CultureInfo.CurrentUICulture);
            if (value != name && arguments.Length > 0)
            {
                try
                {
                    value = string.Format(CultureInfo.CurrentUICulture, value, arguments);
                }
                catch (FormatException)
                {
                    // If formatting fails, return the unformatted value
                }
            }
            return new LocalizedString(name, value, resourceNotFound: value == name);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var returnedKeys = new HashSet<string>();

        // Current culture
        foreach (var (key, value) in _localizer.GetAllStrings())
        {
            if (returnedKeys.Add(key))
            {
                yield return new LocalizedString(key, value, resourceNotFound: false);
            }
        }

        // Parent cultures
        if (includeParentCultures)
        {
            var culture = _localizer.Culture.Parent;
            while (culture != CultureInfo.InvariantCulture)
            {
                foreach (var (key, value) in _localizer.GetAllStrings(culture))
                {
                    if (returnedKeys.Add(key))
                    {
                        yield return new LocalizedString(key, value, resourceNotFound: false);
                    }
                }
                culture = culture.Parent;
            }

            // Default culture
            foreach (var (key, value) in _localizer.GetAllStrings(CultureInfo.InvariantCulture))
            {
                if (returnedKeys.Add(key))
                {
                    yield return new LocalizedString(key, value, resourceNotFound: false);
                }
            }
        }
    }
}

/// <summary>
/// IStringLocalizer{T} implementation backed by JSON localization files.
/// </summary>
/// <typeparam name="T">The type to provide localized strings for.</typeparam>
public class JsonStringLocalizer<T> : JsonStringLocalizer, IStringLocalizer<T>
{
    /// <summary>
    /// Creates a new typed JsonStringLocalizer.
    /// </summary>
    public JsonStringLocalizer(JsonLocalizer localizer) : base(localizer)
    {
    }
}

/// <summary>
/// Adapter for IStringLocalizer{T} that uses IStringLocalizerFactory for proper DI support.
/// </summary>
/// <typeparam name="T">The type to provide localized strings for.</typeparam>
internal class StringLocalizerAdapter<T> : IStringLocalizer<T>
{
    private readonly IStringLocalizer _localizer;

    public StringLocalizerAdapter(IStringLocalizerFactory factory)
    {
        _localizer = factory.Create(typeof(T));
    }

    public LocalizedString this[string name] => _localizer[name];
    public LocalizedString this[string name, params object[] arguments] => _localizer[name, arguments];
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _localizer.GetAllStrings(includeParentCultures);
}
