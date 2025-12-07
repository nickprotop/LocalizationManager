using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LrmCloud.Shared.Configuration;

/// <summary>
/// Extension methods for loading and registering CloudConfiguration.
/// </summary>
public static class ConfigurationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Loads CloudConfiguration from config.json file.
    /// </summary>
    /// <param name="configPath">Path to config.json</param>
    /// <returns>Parsed configuration</returns>
    /// <exception cref="FileNotFoundException">If config.json doesn't exist</exception>
    /// <exception cref="InvalidOperationException">If config.json is invalid</exception>
    public static CloudConfiguration LoadCloudConfiguration(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"Configuration file not found: {configPath}. Run setup.sh to create it.",
                configPath);
        }

        var json = File.ReadAllText(configPath);

        var config = JsonSerializer.Deserialize<CloudConfiguration>(json, JsonOptions);

        if (config is null)
        {
            throw new InvalidOperationException(
                $"Failed to parse configuration file: {configPath}. Ensure it's valid JSON.");
        }

        ValidateConfiguration(config, configPath);

        return config;
    }

    /// <summary>
    /// Adds CloudConfiguration to the service collection as a singleton.
    /// Also registers individual configuration sections for dependency injection.
    /// </summary>
    public static IServiceCollection AddCloudConfiguration(
        this IServiceCollection services,
        CloudConfiguration config)
    {
        // Register root configuration
        services.AddSingleton(config);

        // Register individual sections for convenience
        services.AddSingleton(config.Server);
        services.AddSingleton(config.Database);
        services.AddSingleton(config.Redis);
        services.AddSingleton(config.Storage);
        services.AddSingleton(config.Encryption);
        services.AddSingleton(config.Auth);
        services.AddSingleton(config.Mail);
        services.AddSingleton(config.Features);
        services.AddSingleton(config.Limits);

        return services;
    }

    /// <summary>
    /// Adds config.json as a configuration source.
    /// Use this for IConfiguration-based access (legacy compatibility).
    /// </summary>
    public static IConfigurationBuilder AddCloudConfigurationFile(
        this IConfigurationBuilder builder,
        string configPath)
    {
        if (File.Exists(configPath))
        {
            builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
        }

        return builder;
    }

    private static void ValidateConfiguration(CloudConfiguration config, string configPath)
    {
        var errors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.Server.Urls))
            errors.Add("server.urls is required");

        if (string.IsNullOrWhiteSpace(config.Database.ConnectionString))
            errors.Add("database.connectionString is required");

        if (string.IsNullOrWhiteSpace(config.Redis.ConnectionString))
            errors.Add("redis.connectionString is required");

        if (string.IsNullOrWhiteSpace(config.Storage.Endpoint))
            errors.Add("storage.endpoint is required");

        if (string.IsNullOrWhiteSpace(config.Storage.AccessKey))
            errors.Add("storage.accessKey is required");

        if (string.IsNullOrWhiteSpace(config.Storage.SecretKey))
            errors.Add("storage.secretKey is required");

        if (string.IsNullOrWhiteSpace(config.Encryption.TokenKey))
            errors.Add("encryption.tokenKey is required");

        if (string.IsNullOrWhiteSpace(config.Auth.JwtSecret))
            errors.Add("auth.jwtSecret is required");

        if (config.Auth.JwtSecret?.Length < 32)
            errors.Add("auth.jwtSecret must be at least 32 characters");

        if (string.IsNullOrWhiteSpace(config.Mail.Host))
            errors.Add("mail.host is required");

        if (string.IsNullOrWhiteSpace(config.Mail.FromAddress))
            errors.Add("mail.fromAddress is required");

        // Check for placeholder values
        if (config.Database.ConnectionString.Contains("CHANGE_ME"))
            errors.Add("database.connectionString contains placeholder value. Run setup.sh to generate.");

        if (config.Redis.ConnectionString.Contains("CHANGE_ME"))
            errors.Add("redis.connectionString contains placeholder value. Run setup.sh to generate.");

        if (config.Auth.JwtSecret?.Contains("CHANGE_ME") == true)
            errors.Add("auth.jwtSecret contains placeholder value. Run setup.sh to generate.");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuration validation failed for {configPath}:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }
}
