// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Cloud configuration stored in .lrm/cloud.json.
/// Contains remote URL and authentication credentials.
/// </summary>
public class CloudConfig
{
    /// <summary>
    /// Remote URL (e.g., "https://lrm.cloud/org/project" or just "https://lrm.cloud").
    /// </summary>
    [JsonPropertyName("remote")]
    public string? Remote { get; set; }

    /// <summary>
    /// JWT access token.
    /// </summary>
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    /// <summary>
    /// JWT refresh token.
    /// </summary>
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Access token expiration time.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Refresh token expiration time.
    /// </summary>
    [JsonPropertyName("refreshTokenExpiresAt")]
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// API key for authentication (alternative to JWT).
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    // =========================================================================
    // Derived Properties
    // =========================================================================

    /// <summary>
    /// Gets the host from the remote URL.
    /// </summary>
    [JsonIgnore]
    public string? Host
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Remote))
                return null;

            try
            {
                var uri = new Uri(Remote);
                return uri.Host;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Gets the port from the remote URL (null for default ports).
    /// </summary>
    [JsonIgnore]
    public int? Port
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Remote))
                return null;

            try
            {
                var uri = new Uri(Remote);
                if (uri.IsDefaultPort)
                    return null;
                return uri.Port;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Whether the remote uses HTTPS.
    /// </summary>
    [JsonIgnore]
    public bool UseHttps
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Remote))
                return true; // Default

            try
            {
                var uri = new Uri(Remote);
                return uri.Scheme == "https";
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Whether the user is logged in (has token or API key).
    /// </summary>
    [JsonIgnore]
    public bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(ApiKey) ||
        (!string.IsNullOrWhiteSpace(AccessToken) && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow)) ||
        (!string.IsNullOrWhiteSpace(RefreshToken) && (RefreshTokenExpiresAt == null || RefreshTokenExpiresAt > DateTime.UtcNow));

    /// <summary>
    /// Whether a remote URL is configured.
    /// </summary>
    [JsonIgnore]
    public bool HasRemote => !string.IsNullOrWhiteSpace(Remote);

    /// <summary>
    /// Whether the remote is a full project URL (not just host).
    /// </summary>
    [JsonIgnore]
    public bool HasProject
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Remote))
                return false;

            try
            {
                var uri = new Uri(Remote);
                // A project URL has a path like /org/project or /@user/project
                var path = uri.AbsolutePath.Trim('/');
                return !string.IsNullOrEmpty(path) && path.Contains('/');
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Whether the access token is expired.
    /// </summary>
    [JsonIgnore]
    public bool IsTokenExpired =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        ExpiresAt.HasValue &&
        ExpiresAt.Value < DateTime.UtcNow;

    /// <summary>
    /// Gets the API base URL for the host.
    /// </summary>
    [JsonIgnore]
    public string? ApiBaseUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Remote))
                return null;

            try
            {
                var uri = new Uri(Remote);
                var scheme = uri.Scheme;
                var host = uri.Host;
                var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
                return $"{scheme}://{host}{port}/api";
            }
            catch
            {
                return null;
            }
        }
    }
}

/// <summary>
/// Manages cloud configuration stored in .lrm/cloud.json.
/// </summary>
public static class CloudConfigManager
{
    private const string ConfigDirectory = ".lrm";
    private const string ConfigFileName = "cloud.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Loads the cloud configuration from .lrm/cloud.json.
    /// Returns empty config if file doesn't exist.
    /// </summary>
    public static async Task<CloudConfig> LoadAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var configPath = GetConfigPath(projectDirectory);

        if (!File.Exists(configPath))
        {
            return new CloudConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            return JsonSerializer.Deserialize<CloudConfig>(json, JsonOptions) ?? new CloudConfig();
        }
        catch (JsonException)
        {
            // Invalid config file, return empty
            return new CloudConfig();
        }
    }

    /// <summary>
    /// Saves the cloud configuration to .lrm/cloud.json.
    /// </summary>
    public static async Task SaveAsync(string projectDirectory, CloudConfig config, CancellationToken cancellationToken = default)
    {
        var configPath = GetConfigPath(projectDirectory);
        var directory = Path.GetDirectoryName(configPath);

        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        config.UpdatedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        // Set file permissions to user-only (Unix-like systems)
        SetSecurePermissions(configPath);
    }

    /// <summary>
    /// Clears all authentication data (logout).
    /// </summary>
    public static async Task ClearAuthAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(projectDirectory, cancellationToken);

        config.AccessToken = null;
        config.RefreshToken = null;
        config.ExpiresAt = null;
        config.RefreshTokenExpiresAt = null;
        config.ApiKey = null;

        await SaveAsync(projectDirectory, config, cancellationToken);
    }

    /// <summary>
    /// Clears the entire cloud configuration (removes .lrm/cloud.json).
    /// </summary>
    public static Task ClearAllAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var configPath = GetConfigPath(projectDirectory);

        if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the remote URL in the configuration.
    /// </summary>
    public static async Task SetRemoteAsync(string projectDirectory, string remote, CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(projectDirectory, cancellationToken);
        config.Remote = remote;
        await SaveAsync(projectDirectory, config, cancellationToken);
    }

    /// <summary>
    /// Sets the authentication tokens in the configuration.
    /// </summary>
    public static async Task SetAuthenticationAsync(
        string projectDirectory,
        string accessToken,
        DateTime? expiresAt,
        string? refreshToken = null,
        DateTime? refreshTokenExpiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(projectDirectory, cancellationToken);

        config.AccessToken = accessToken;
        config.ExpiresAt = expiresAt;
        config.RefreshToken = refreshToken;
        config.RefreshTokenExpiresAt = refreshTokenExpiresAt;

        await SaveAsync(projectDirectory, config, cancellationToken);
    }

    /// <summary>
    /// Sets the API key in the configuration.
    /// </summary>
    public static async Task SetApiKeyAsync(string projectDirectory, string apiKey, CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(projectDirectory, cancellationToken);
        config.ApiKey = apiKey;
        await SaveAsync(projectDirectory, config, cancellationToken);
    }

    /// <summary>
    /// Gets the API key from the environment variable.
    /// </summary>
    public static string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("LRM_CLOUD_API_KEY");
    }

    /// <summary>
    /// Gets the effective API key (environment variable takes precedence).
    /// </summary>
    public static async Task<string?> GetEffectiveApiKeyAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        // Environment variable takes precedence
        var envKey = GetApiKeyFromEnvironment();
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        // Then check stored config
        var config = await LoadAsync(projectDirectory, cancellationToken);
        return config.ApiKey;
    }

    private static string GetConfigPath(string projectDirectory)
    {
        return Path.Combine(projectDirectory, ConfigDirectory, ConfigFileName);
    }

    private static void SetSecurePermissions(string path)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignore errors setting permissions
            }
        }
    }
}
