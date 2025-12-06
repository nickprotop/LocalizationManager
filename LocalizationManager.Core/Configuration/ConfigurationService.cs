// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.Core.Translation;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Service for managing configuration with runtime reload support
/// </summary>
public class ConfigurationService
{
    private readonly string _basePath;
    private ConfigurationModel? _currentConfig;
    private string? _configPath;
    private DateTime _lastModified;
    private readonly object _lock = new();

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public ConfigurationService(string basePath)
    {
        _basePath = basePath;
        LoadConfiguration();
    }

    /// <summary>
    /// Gets the current configuration, reloading if the file has changed
    /// </summary>
    public ConfigurationModel GetConfiguration()
    {
        lock (_lock)
        {
            // Check if config file has been modified
            if (_configPath != null && System.IO.File.Exists(_configPath))
            {
                var lastWrite = System.IO.File.GetLastWriteTimeUtc(_configPath);
                if (lastWrite > _lastModified)
                {
                    // File changed, reload
                    LoadConfiguration();
                }
            }

            return _currentConfig ?? new ConfigurationModel();
        }
    }

    /// <summary>
    /// Saves configuration to lrm.json and triggers reload
    /// </summary>
    public void SaveConfiguration(ConfigurationModel config)
    {
        lock (_lock)
        {
            var configPath = Path.Combine(_basePath, "lrm.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(config, options);
            System.IO.File.WriteAllText(configPath, json);

            // Update internal state
            _currentConfig = config;
            _configPath = configPath;
            _lastModified = System.IO.File.GetLastWriteTimeUtc(configPath);

            // Notify listeners
            OnConfigurationChanged(new ConfigurationChangedEventArgs
            {
                NewConfiguration = config,
                ConfigPath = configPath
            });
        }
    }

    /// <summary>
    /// Creates a new configuration file
    /// </summary>
    public void CreateConfiguration(ConfigurationModel config)
    {
        var configPath = Path.Combine(_basePath, "lrm.json");
        if (System.IO.File.Exists(configPath))
        {
            throw new InvalidOperationException("Configuration file already exists");
        }

        SaveConfiguration(config);
    }

    /// <summary>
    /// Validates configuration before applying
    /// </summary>
    public (bool isValid, List<string> errors) ValidateConfiguration(ConfigurationModel config)
    {
        var errors = new List<string>();

        // Validate translation provider if specified
        if (!string.IsNullOrEmpty(config.Translation?.DefaultProvider))
        {
            if (!TranslationProviderFactory.IsProviderSupported(config.Translation.DefaultProvider))
            {
                errors.Add($"Unknown translation provider: {config.Translation.DefaultProvider}");
            }
        }

        // Validate web port if specified
        if (config.Web?.Port.HasValue == true && (config.Web.Port.Value < 1 || config.Web.Port.Value > 65535))
        {
            errors.Add($"Invalid port number: {config.Web.Port}. Must be between 1 and 65535.");
        }

        // Validate CORS origins if CORS is enabled
        if (config.Web?.Cors?.Enabled == true && (config.Web.Cors.AllowedOrigins == null || config.Web.Cors.AllowedOrigins.Count == 0))
        {
            errors.Add("CORS is enabled but no allowed origins are specified.");
        }

        return (errors.Count == 0, errors);
    }

    private void LoadConfiguration()
    {
        var (config, loadedFrom) = ConfigurationManager.LoadConfiguration(null, _basePath);
        _currentConfig = config;
        _configPath = loadedFrom;

        if (_configPath != null && System.IO.File.Exists(_configPath))
        {
            _lastModified = System.IO.File.GetLastWriteTimeUtc(_configPath);
        }
    }

    protected virtual void OnConfigurationChanged(ConfigurationChangedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, e);
    }
}

public class ConfigurationChangedEventArgs : EventArgs
{
    public ConfigurationModel NewConfiguration { get; set; } = null!;
    public string ConfigPath { get; set; } = string.Empty;
}
