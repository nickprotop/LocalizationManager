// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Manages authentication tokens for cloud API access.
/// Tokens are stored in .lrm/config.json (git-ignored).
/// </summary>
public static class AuthTokenManager
{
    private const string TokenDirectory = ".lrm";
    private const string TokenFileName = "auth.json";

    /// <summary>
    /// Gets the authentication token for the specified remote host.
    /// </summary>
    public static async Task<string?> GetTokenAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
    {
        var tokenPath = GetTokenPath(projectDirectory);
        if (!File.Exists(tokenPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(tokenPath, cancellationToken);
            var tokens = JsonSerializer.Deserialize<Dictionary<string, TokenInfo>>(json);

            if (tokens != null && tokens.TryGetValue(host, out var tokenInfo))
            {
                // Check if token is expired
                if (tokenInfo.ExpiresAt.HasValue && tokenInfo.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return null; // Token expired
                }

                return tokenInfo.AccessToken;
            }
        }
        catch (JsonException)
        {
            // Invalid token file, return null
        }

        return null;
    }

    /// <summary>
    /// Sets the authentication token for the specified remote host.
    /// </summary>
    public static async Task SetTokenAsync(
        string projectDirectory,
        string host,
        string accessToken,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var tokenPath = GetTokenPath(projectDirectory);
        var directory = Path.GetDirectoryName(tokenPath);

        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Dictionary<string, TokenInfo> tokens;

        // Load existing tokens if file exists
        if (File.Exists(tokenPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(tokenPath, cancellationToken);
                tokens = JsonSerializer.Deserialize<Dictionary<string, TokenInfo>>(json) ?? new Dictionary<string, TokenInfo>();
            }
            catch (JsonException)
            {
                tokens = new Dictionary<string, TokenInfo>();
            }
        }
        else
        {
            tokens = new Dictionary<string, TokenInfo>();
        }

        // Update or add token
        tokens[host] = new TokenInfo
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            UpdatedAt = DateTime.UtcNow
        };

        // Save tokens
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var updatedJson = JsonSerializer.Serialize(tokens, options);
        await File.WriteAllTextAsync(tokenPath, updatedJson, cancellationToken);

        // Set file permissions to user-only (Unix-like systems)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(tokenPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignore errors setting permissions
            }
        }
    }

    /// <summary>
    /// Removes the authentication token for the specified remote host.
    /// </summary>
    public static async Task RemoveTokenAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
    {
        var tokenPath = GetTokenPath(projectDirectory);
        if (!File.Exists(tokenPath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(tokenPath, cancellationToken);
            var tokens = JsonSerializer.Deserialize<Dictionary<string, TokenInfo>>(json);

            if (tokens != null && tokens.Remove(host))
            {
                if (tokens.Count == 0)
                {
                    // Delete file if no tokens left
                    File.Delete(tokenPath);
                }
                else
                {
                    // Save updated tokens
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var updatedJson = JsonSerializer.Serialize(tokens, options);
                    await File.WriteAllTextAsync(tokenPath, updatedJson, cancellationToken);
                }
            }
        }
        catch (JsonException)
        {
            // Invalid token file, ignore
        }
    }

    /// <summary>
    /// Checks if a token exists for the specified remote host.
    /// </summary>
    public static async Task<bool> HasTokenAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(projectDirectory, host, cancellationToken);
        return !string.IsNullOrWhiteSpace(token);
    }

    private static string GetTokenPath(string projectDirectory)
    {
        return Path.Combine(projectDirectory, TokenDirectory, TokenFileName);
    }

    private class TokenInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
