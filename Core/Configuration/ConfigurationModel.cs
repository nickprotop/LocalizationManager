namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Represents the configuration model for LocalizationManager.
/// This class will be expanded in the future to include various configuration options.
/// </summary>
public class ConfigurationModel
{
    /// <summary>
    /// The language code to display for the default language (e.g., "en", "fr").
    /// If not set, displays "default". Only affects display output, not internal logic.
    /// </summary>
    public string? DefaultLanguageCode { get; set; }
}
