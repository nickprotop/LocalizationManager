// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using System.Reflection;
using LocalizationManager.JsonLocalization.Core;
using LocalizationManager.JsonLocalization.Ota;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Options for configuring JSON localization.
/// </summary>
public class JsonLocalizationOptions
{
    /// <summary>
    /// Path to the directory containing JSON resource files.
    /// Used when loading resources from the file system.
    /// Default: "Resources"
    /// </summary>
    public string ResourcesPath { get; set; } = "Resources";

    /// <summary>
    /// Base name of the resource files (without extension or culture code).
    /// Example: "strings" produces "strings.json", "strings.fr.json", etc.
    /// Default: "strings"
    /// </summary>
    public string BaseName { get; set; } = "strings";

    /// <summary>
    /// Whether to load resources from embedded assembly resources instead of the file system.
    /// When true, ResourcesPath is used as the namespace prefix.
    /// Default: false
    /// </summary>
    public bool UseEmbeddedResources { get; set; } = false;

    /// <summary>
    /// The assembly to load embedded resources from.
    /// Only used when UseEmbeddedResources is true.
    /// Default: Entry assembly
    /// </summary>
    public Assembly? ResourceAssembly { get; set; }

    /// <summary>
    /// The default culture to use when the current culture is not available.
    /// Default: InvariantCulture
    /// </summary>
    public CultureInfo DefaultCulture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Enable i18next compatibility mode.
    /// When true, supports i18next-style interpolation ({{var}}) and plural suffixes.
    /// Default: false
    /// </summary>
    public bool I18nextCompatible { get; set; } = false;

    /// <summary>
    /// Whether to use nested key structure in JSON files.
    /// When true, keys like "Errors.NotFound" map to nested objects.
    /// Default: true
    /// </summary>
    public bool UseNestedKeys { get; set; } = true;

    /// <summary>
    /// OTA (Over-The-Air) localization options.
    /// When configured, translations are fetched from LRM Cloud at runtime.
    /// </summary>
    public OtaOptions? Ota { get; set; }

    /// <summary>
    /// Configures OTA (Over-The-Air) localization with LRM Cloud.
    /// Translations are fetched from the cloud at runtime and updated periodically.
    /// </summary>
    /// <param name="endpoint">The LRM Cloud endpoint URL (default: https://lrm-cloud.com)</param>
    /// <param name="apiKey">The API key for authentication (must start with lrm_)</param>
    /// <param name="project">Project path: @username/project for user projects, or org/project for organizations</param>
    /// <returns>This options instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddJsonLocalization(options => {
    ///     options.UseOta(
    ///         endpoint: "https://lrm-cloud.com",
    ///         apiKey: "lrm_your_api_key",
    ///         project: "@username/my-project"
    ///     );
    /// });
    /// </code>
    /// </example>
    public JsonLocalizationOptions UseOta(string endpoint, string apiKey, string project)
    {
        Ota = new OtaOptions
        {
            Endpoint = endpoint,
            ApiKey = apiKey,
            Project = project
        };
        return this;
    }

    /// <summary>
    /// Configures OTA (Over-The-Air) localization with detailed options.
    /// </summary>
    /// <param name="configure">Action to configure OTA options.</param>
    /// <returns>This options instance for chaining.</returns>
    public JsonLocalizationOptions UseOta(Action<OtaOptions> configure)
    {
        Ota = new OtaOptions();
        configure(Ota);
        return this;
    }

    /// <summary>
    /// Gets the JSON format configuration based on these options.
    /// </summary>
    internal JsonFormatConfiguration GetFormatConfiguration()
    {
        return new JsonFormatConfiguration
        {
            BaseName = BaseName,
            UseNestedKeys = UseNestedKeys,
            I18nextCompatible = I18nextCompatible,
            IncludeMeta = false,  // Not needed for reading
            PreserveComments = false  // Not needed for reading
        };
    }
}
