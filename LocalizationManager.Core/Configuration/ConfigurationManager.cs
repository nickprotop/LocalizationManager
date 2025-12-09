// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Manages loading, merging, and saving of configuration files.
/// Supports hybrid configuration: lrm.json (team) + .lrm/config.json (personal overrides).
/// </summary>
public static class ConfigurationManager
{
    private const string DefaultConfigFileName = "lrm.json";
    private const string PersonalConfigDirectory = ".lrm";
    private const string PersonalConfigFileName = "config.json";
    private const string RemotesFileName = "remotes.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

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
            var config = JsonSerializer.Deserialize<ConfigurationModel>(jsonContent, ReadOptions);

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

    /// <summary>
    /// Loads configuration with hybrid support (team + personal overrides + remotes).
    /// </summary>
    public static async Task<ConfigurationModel> LoadConfigurationAsync(
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
            throw new ArgumentNullException(nameof(projectDirectory));

        // Load team config (lrm.json)
        var teamConfig = await LoadTeamConfigurationAsync(projectDirectory, cancellationToken);

        // Load personal config (.lrm/config.json) if exists
        var personalConfig = await LoadPersonalConfigurationAsync(projectDirectory, cancellationToken);

        // Merge: personal overrides team
        var mergedConfig = personalConfig != null
            ? MergeConfigurations(teamConfig, personalConfig)
            : teamConfig;

        return mergedConfig;
    }

    /// <summary>
    /// Loads team configuration from lrm.json.
    /// </summary>
    public static async Task<ConfigurationModel> LoadTeamConfigurationAsync(
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(projectDirectory, DefaultConfigFileName);
        if (!File.Exists(path))
        {
            return new ConfigurationModel();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var config = JsonSerializer.Deserialize<ConfigurationModel>(json, ReadOptions);
            return config ?? new ConfigurationModel();
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Failed to parse {DefaultConfigFileName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads personal configuration from .lrm/config.json.
    /// </summary>
    public static async Task<ConfigurationModel?> LoadPersonalConfigurationAsync(
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(projectDirectory, PersonalConfigDirectory, PersonalConfigFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var config = JsonSerializer.Deserialize<ConfigurationModel>(json, ReadOptions);
            return config;
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Failed to parse {PersonalConfigFileName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves team configuration to lrm.json.
    /// </summary>
    public static async Task SaveTeamConfigurationAsync(
        string projectDirectory,
        ConfigurationModel config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var path = Path.Combine(projectDirectory, DefaultConfigFileName);
        var json = JsonSerializer.Serialize(config, WriteOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary>
    /// Saves personal configuration to .lrm/config.json.
    /// </summary>
    public static async Task SavePersonalConfigurationAsync(
        string projectDirectory,
        ConfigurationModel config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var directory = Path.Combine(projectDirectory, PersonalConfigDirectory);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var path = Path.Combine(directory, PersonalConfigFileName);
        var json = JsonSerializer.Serialize(config, WriteOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary>
    /// Loads remotes configuration from .lrm/remotes.json with fallback to lrm.json cloud settings.
    /// </summary>
    public static async Task<RemotesConfiguration> LoadRemotesConfigurationAsync(
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        // First, try to load from .lrm/remotes.json
        var remotesPath = Path.Combine(projectDirectory, PersonalConfigDirectory, RemotesFileName);
        if (File.Exists(remotesPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(remotesPath, cancellationToken);
                var remotes = JsonSerializer.Deserialize<RemotesConfiguration>(json, ReadOptions);
                if (remotes != null)
                {
                    return remotes;
                }
            }
            catch (JsonException ex)
            {
                throw new ConfigurationException($"Failed to parse {RemotesFileName}: {ex.Message}", ex);
            }
        }

        // No remote configured
        return new RemotesConfiguration();
    }

    /// <summary>
    /// Saves remotes configuration to .lrm/remotes.json.
    /// </summary>
    public static async Task SaveRemotesConfigurationAsync(
        string projectDirectory,
        RemotesConfiguration remotes,
        CancellationToken cancellationToken = default)
    {
        if (remotes == null)
            throw new ArgumentNullException(nameof(remotes));

        var directory = Path.Combine(projectDirectory, PersonalConfigDirectory);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var path = Path.Combine(directory, RemotesFileName);
        var json = JsonSerializer.Serialize(remotes, WriteOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary>
    /// Ensures .lrm directory is in .gitignore.
    /// </summary>
    public static async Task EnsureGitIgnoreAsync(
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        var gitIgnorePath = Path.Combine(projectDirectory, ".gitignore");
        var lrmEntry = ".lrm/";

        if (!File.Exists(gitIgnorePath))
        {
            await File.WriteAllTextAsync(gitIgnorePath, lrmEntry + Environment.NewLine, cancellationToken);
            return;
        }

        var content = await File.ReadAllTextAsync(gitIgnorePath, cancellationToken);
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Check if .lrm/ is already in .gitignore
        if (lines.Any(line => line.Trim() == lrmEntry || line.Trim() == ".lrm"))
        {
            return;
        }

        // Add .lrm/ to .gitignore
        await File.AppendAllTextAsync(gitIgnorePath, Environment.NewLine + lrmEntry + Environment.NewLine, cancellationToken);
    }

    /// <summary>
    /// Merges personal configuration overrides into team configuration.
    /// </summary>
    private static ConfigurationModel MergeConfigurations(ConfigurationModel team, ConfigurationModel personal)
    {
        return new ConfigurationModel
        {
            DefaultLanguageCode = personal.DefaultLanguageCode ?? team.DefaultLanguageCode,
            ResourceFormat = personal.ResourceFormat ?? team.ResourceFormat,
            Translation = personal.Translation ?? team.Translation,
            Scanning = personal.Scanning ?? team.Scanning,
            Validation = personal.Validation ?? team.Validation,
            Web = personal.Web ?? team.Web,
            Json = personal.Json ?? team.Json
        };
    }
}

/// <summary>
/// Exception thrown when configuration operations fail.
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message)
    {
    }

    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
