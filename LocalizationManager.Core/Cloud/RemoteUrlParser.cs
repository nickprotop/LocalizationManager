// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Parses remote URL strings into structured RemoteUrl objects.
/// Supports formats:
/// - https://lrm.cloud/org-name/project-name
/// - https://lrm.cloud/@username/project-name
/// - https://self-hosted.com:8080/org/project
/// </summary>
public static partial class RemoteUrlParser
{
    // Regex pattern: https://host:port/owner/project
    // Owner can be "org-name" or "@username"
    [GeneratedRegex(@"^(?<protocol>https?):\/\/(?<host>[a-zA-Z0-9\.\-]+)(?::(?<port>\d+))?\/(?<owner>@?[a-zA-Z0-9\-_]+)\/(?<project>[a-zA-Z0-9\-_]+)$", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    /// <summary>
    /// Parse a remote URL string.
    /// </summary>
    /// <param name="url">URL string to parse</param>
    /// <returns>Parsed RemoteUrl object</returns>
    /// <exception cref="ArgumentException">Thrown if URL format is invalid</exception>
    public static RemoteUrl Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        var match = UrlRegex().Match(url.Trim());
        if (!match.Success)
        {
            throw new ArgumentException(
                "Invalid remote URL format. Expected: https://host/org/project or https://host/@username/project",
                nameof(url));
        }

        var protocol = match.Groups["protocol"].Value;
        var host = match.Groups["host"].Value;
        var portString = match.Groups["port"].Value;
        var owner = match.Groups["owner"].Value;
        var project = match.Groups["project"].Value;

        var useHttps = protocol == "https";
        var port = string.IsNullOrEmpty(portString)
            ? (useHttps ? 443 : 80)
            : int.Parse(portString);

        // Check if owner is a username (starts with @)
        var isPersonal = owner.StartsWith('@');
        var username = isPersonal ? owner.Substring(1) : null;
        var organization = isPersonal ? null : owner;

        return new RemoteUrl
        {
            Host = host,
            Port = port,
            UseHttps = useHttps,
            Organization = organization,
            Username = username,
            ProjectName = project,
            OriginalUrl = url
        };
    }

    /// <summary>
    /// Try to parse a remote URL string without throwing exceptions.
    /// </summary>
    /// <param name="url">URL string to parse</param>
    /// <param name="result">Parsed RemoteUrl object if successful</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParse(string url, out RemoteUrl? result)
    {
        try
        {
            result = Parse(url);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Validate a remote URL string format.
    /// </summary>
    /// <param name="url">URL string to validate</param>
    /// <returns>True if URL format is valid</returns>
    public static bool IsValid(string url)
    {
        return TryParse(url, out _);
    }
}
