using System.Text.Json;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Manages loading and parsing of configuration files for LocalizationManager.
/// </summary>
public static class ConfigurationManager
{
    private const string DefaultConfigFileName = "lrm.json";

    /// <summary>
    /// Loads configuration from a file path or discovers it automatically.
    /// </summary>
    /// <param name="configPath">Optional explicit path to configuration file.</param>
    /// <param name="resourcePath">The resource path where to look for default config file.</param>
    /// <returns>A tuple containing the loaded configuration (if any) and the path it was loaded from.</returns>
    public static (ConfigurationModel? Config, string? LoadedFrom) LoadConfiguration(
        string? configPath,
        string resourcePath)
    {
        string? pathToLoad = null;

        // If explicit config path provided, use it
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {configPath}");
            }
            pathToLoad = configPath;
        }
        else
        {
            // Try to find default config file in resource path
            var defaultConfigPath = Path.Combine(resourcePath, DefaultConfigFileName);
            if (File.Exists(defaultConfigPath))
            {
                pathToLoad = defaultConfigPath;
            }
        }

        // If no config file found, return null
        if (pathToLoad == null)
        {
            return (null, null);
        }

        // Load and parse the configuration file
        try
        {
            var jsonContent = File.ReadAllText(pathToLoad);
            var config = JsonSerializer.Deserialize<ConfigurationModel>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            return (config ?? new ConfigurationModel(), Path.GetFullPath(pathToLoad));
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse configuration file '{pathToLoad}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load configuration file '{pathToLoad}': {ex.Message}", ex);
        }
    }
}
