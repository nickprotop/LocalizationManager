// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// Resource loader that fetches translations from OTA cache.
/// Falls back to local resources when OTA is unavailable.
/// </summary>
public class OtaResourceLoader : IResourceLoader
{
    private readonly OtaClient _otaClient;
    private readonly IResourceLoader? _fallbackLoader;
    private readonly ILogger<OtaResourceLoader> _logger;

    /// <summary>
    /// Creates a new OTA resource loader.
    /// </summary>
    /// <param name="otaClient">The OTA client to use.</param>
    /// <param name="fallbackLoader">Optional fallback loader for when OTA is unavailable.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OtaResourceLoader(
        OtaClient otaClient,
        IResourceLoader? fallbackLoader = null,
        ILogger<OtaResourceLoader>? logger = null)
    {
        _otaClient = otaClient ?? throw new ArgumentNullException(nameof(otaClient));
        _fallbackLoader = fallbackLoader;
        _logger = logger ?? NullLogger<OtaResourceLoader>.Instance;
    }

    /// <summary>
    /// Gets the resource stream for a specific culture.
    /// </summary>
    public Stream? GetResourceStream(string baseName, string culture)
    {
        // Try OTA first
        var translations = _otaClient.GetTranslations(culture);

        // Fall back to default language if culture not found
        if (translations == null && !string.IsNullOrEmpty(culture))
        {
            var bundle = _otaClient.CachedBundle;
            if (bundle != null)
            {
                translations = _otaClient.GetTranslations(bundle.DefaultLanguage);
            }
        }

        if (translations != null && translations.Count > 0)
        {
            // Convert translations to JSON stream
            var json = ConvertToJson(translations);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        // Fall back to local resources
        if (_fallbackLoader != null)
        {
            _logger.LogDebug(
                "OTA cache miss for culture '{Culture}', falling back to local resources",
                culture);
            return _fallbackLoader.GetResourceStream(baseName, culture);
        }

        _logger.LogWarning(
            "OTA cache miss for culture '{Culture}' and no fallback loader configured",
            culture);
        return null;
    }

    /// <summary>
    /// Gets available cultures from OTA or fallback.
    /// </summary>
    public IEnumerable<string> GetAvailableCultures(string baseName)
    {
        var otaCultures = _otaClient.GetAvailableLanguages().ToList();

        if (otaCultures.Count > 0)
        {
            return otaCultures;
        }

        // Fall back to local
        if (_fallbackLoader != null)
        {
            _logger.LogDebug("No OTA cultures available, falling back to local resources");
            return _fallbackLoader.GetAvailableCultures(baseName);
        }

        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Converts OTA translations to JSON format compatible with JsonResourceReader.
    /// Plural forms are output in CLDR format (e.g., { "one": "...", "other": "..." })
    /// which JsonResourceReader auto-detects via HasCLDRPluralForms.
    /// </summary>
    private static string ConvertToJson(Dictionary<string, object> translations)
    {
        var result = new Dictionary<string, object>();

        foreach (var (key, value) in translations)
        {
            if (value is JsonElement element)
            {
                // Handle JsonElement from deserialization
                if (element.ValueKind == JsonValueKind.String)
                {
                    result[key] = element.GetString() ?? "";
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    // Plural forms - output in CLDR format directly
                    // JsonResourceReader auto-detects via HasCLDRPluralForms
                    var pluralForms = new Dictionary<string, string>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            pluralForms[prop.Name] = prop.Value.GetString() ?? "";
                        }
                    }
                    result[key] = pluralForms;
                }
                else
                {
                    result[key] = element.ToString();
                }
            }
            else if (value is string strValue)
            {
                result[key] = strValue;
            }
            else if (value is Dictionary<string, string> pluralDict)
            {
                // Already a plural dictionary - output CLDR format directly
                result[key] = pluralDict;
            }
            else
            {
                result[key] = value?.ToString() ?? "";
            }
        }

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
}
