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
    /// Sets complete authentication information including refresh token.
    /// </summary>
    public static async Task SetAuthenticationAsync(
        string projectDirectory,
        string host,
        string accessToken,
        DateTime? expiresAt,
        string? refreshToken,
        DateTime? refreshTokenExpiresAt,
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

        // Update or add token with refresh token
        tokens[host] = new TokenInfo
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
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
    /// Gets the refresh token for the specified remote host.
    /// </summary>
    public static async Task<string?> GetRefreshTokenAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
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
                // Check if refresh token is expired
                if (tokenInfo.RefreshTokenExpiresAt.HasValue && tokenInfo.RefreshTokenExpiresAt.Value < DateTime.UtcNow)
                {
                    return null; // Refresh token expired
                }

                return tokenInfo.RefreshToken;
            }
        }
        catch (JsonException)
        {
            // Invalid token file, return null
        }

        return null;
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
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public string? ApiKey { get; set; }
    }

    // =========================================================================
    // API Key Methods
    // =========================================================================

    /// <summary>
    /// Gets the API key from the LRM_CLOUD_API_KEY environment variable.
    /// </summary>
    public static string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("LRM_CLOUD_API_KEY");
    }

    /// <summary>
    /// Gets the stored API key for the specified remote host.
    /// </summary>
    public static async Task<string?> GetApiKeyAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
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
                return tokenInfo.ApiKey;
            }
        }
        catch (JsonException)
        {
            // Invalid token file, return null
        }

        return null;
    }

    /// <summary>
    /// Sets the API key for the specified remote host.
    /// </summary>
    public static async Task SetApiKeyAsync(
        string projectDirectory,
        string host,
        string apiKey,
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

        // Update or create entry with API key
        if (tokens.TryGetValue(host, out var existingToken))
        {
            existingToken.ApiKey = apiKey;
            existingToken.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            tokens[host] = new TokenInfo
            {
                ApiKey = apiKey,
                UpdatedAt = DateTime.UtcNow
            };
        }

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
    /// Removes the API key for the specified remote host.
    /// </summary>
    public static async Task RemoveApiKeyAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
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

            if (tokens != null && tokens.TryGetValue(host, out var tokenInfo))
            {
                tokenInfo.ApiKey = null;
                tokenInfo.UpdatedAt = DateTime.UtcNow;

                // If no tokens left for this host, remove the entry
                if (string.IsNullOrEmpty(tokenInfo.AccessToken) && string.IsNullOrEmpty(tokenInfo.ApiKey))
                {
                    tokens.Remove(host);
                }

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
    /// Checks if an API key exists for the specified remote host (environment or stored).
    /// </summary>
    public static async Task<bool> HasApiKeyAsync(string projectDirectory, string host, CancellationToken cancellationToken = default)
    {
        // Check environment variable first
        var envKey = GetApiKeyFromEnvironment();
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return true;
        }

        // Check stored key
        var storedKey = await GetApiKeyAsync(projectDirectory, host, cancellationToken);
        return !string.IsNullOrWhiteSpace(storedKey);
    }
}
