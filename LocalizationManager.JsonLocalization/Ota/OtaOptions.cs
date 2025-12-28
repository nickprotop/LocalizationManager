// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// Configuration options for OTA (Over-The-Air) localization.
/// </summary>
public class OtaOptions
{
    /// <summary>
    /// The LRM Cloud endpoint URL.
    /// Default: "https://lrm-cloud.com"
    /// </summary>
    public string Endpoint { get; set; } = "https://lrm-cloud.com";

    /// <summary>
    /// The API key for authentication (must start with "lrm_").
    /// Required for OTA access.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The project path: @username/project for user projects, or org/project for organization projects.
    /// Examples: "@nick/my-app", "acme/webapp"
    /// </summary>
    public string Project { get; set; } = "";

    /// <summary>
    /// How often to check for updates.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to fall back to embedded/file system resources when OTA is unavailable.
    /// Default: true
    /// </summary>
    public bool FallbackToLocal { get; set; } = true;

    /// <summary>
    /// HTTP request timeout.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Circuit breaker failure threshold before opening.
    /// Default: 5 failures
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// How long to keep the circuit breaker open before trying again.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional: Specific languages to fetch. If null, fetches all available.
    /// </summary>
    public IEnumerable<string>? Languages { get; set; }

    /// <summary>
    /// Parses the Project path and returns (isUser, owner, project).
    /// </summary>
    internal (bool IsUser, string Owner, string ProjectName) ParseProject()
    {
        if (string.IsNullOrWhiteSpace(Project))
            throw new ArgumentException("Project path is required. Expected format: @owner/project or org/project");

        var isUser = Project.StartsWith("@");
        var path = isUser ? Project[1..] : Project;
        var slash = path.IndexOf('/');

        if (slash <= 0 || slash >= path.Length - 1)
            throw new ArgumentException("Invalid project format. Expected: @owner/project or org/project");

        return (isUser, path[..slash], path[(slash + 1)..]);
    }

    /// <summary>
    /// Builds the full API URL for the bundle endpoint.
    /// </summary>
    internal string BuildBundleUrl()
    {
        var (isUser, owner, projectName) = ParseProject();
        var type = isUser ? "users" : "orgs";
        return $"{Endpoint.TrimEnd('/')}/api/ota/{type}/{owner}/{projectName}/bundle";
    }

    /// <summary>
    /// Builds the full API URL for the version endpoint.
    /// </summary>
    internal string BuildVersionUrl()
    {
        var (isUser, owner, projectName) = ParseProject();
        var type = isUser ? "users" : "orgs";
        return $"{Endpoint.TrimEnd('/')}/api/ota/{type}/{owner}/{projectName}/version";
    }

    /// <summary>
    /// Validates the options.
    /// </summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("API key is required for OTA localization");

        if (!ApiKey.StartsWith("lrm_"))
            throw new ArgumentException("Invalid API key format. API keys must start with 'lrm_'");

        // This will throw if project format is invalid
        ParseProject();
    }
}
