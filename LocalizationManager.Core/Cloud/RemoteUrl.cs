// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Represents a parsed remote URL for LRM Cloud.
/// Format: https://host[:port]/[org-or-user]/[project-name]
/// </summary>
public class RemoteUrl
{
    /// <summary>
    /// Host (e.g., "lrm-cloud.com", "staging.lrm-cloud.com", "localhost")
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Port number (default: 443 for https, 80 for http)
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Whether to use HTTPS
    /// </summary>
    public bool UseHttps { get; init; }

    /// <summary>
    /// Organization slug (null for personal projects)
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Username (for personal projects with @ prefix)
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Project name
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Whether this is a personal project (@username format)
    /// </summary>
    public bool IsPersonalProject => Username != null;

    /// <summary>
    /// Base API URL (https://host:port/api)
    /// </summary>
    public string ApiBaseUrl => $"{(UseHttps ? "https" : "http")}://{Host}{(IsDefaultPort ? "" : $":{Port}")}/api";

    /// <summary>
    /// Full project API URL
    /// </summary>
    public string ProjectApiUrl
    {
        get
        {
            if (IsPersonalProject)
                return $"{ApiBaseUrl}/users/{Username}/projects/{ProjectName}";
            else
                return $"{ApiBaseUrl}/projects/{Organization}/{ProjectName}";
        }
    }

    /// <summary>
    /// Whether the port is the default for the protocol
    /// </summary>
    private bool IsDefaultPort => (UseHttps && Port == 443) || (!UseHttps && Port == 80);

    /// <summary>
    /// Original URL string
    /// </summary>
    public string OriginalUrl { get; init; } = string.Empty;

    /// <summary>
    /// Convert back to URL string
    /// </summary>
    public override string ToString()
    {
        var protocol = UseHttps ? "https" : "http";
        var port = IsDefaultPort ? "" : $":{Port}";
        var owner = IsPersonalProject ? $"@{Username}" : Organization;
        return $"{protocol}://{Host}{port}/{owner}/{ProjectName}";
    }
}
