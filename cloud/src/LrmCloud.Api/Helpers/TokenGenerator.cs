using System.Security.Cryptography;

namespace LrmCloud.Api.Helpers;

public static class TokenGenerator
{
    /// <summary>
    /// Generate a cryptographically secure random token
    /// </summary>
    /// <param name="byteLength">Length in bytes (default: 32)</param>
    /// <returns>URL-safe base64 encoded token</returns>
    public static string GenerateSecureToken(int byteLength = 32)
    {
        var tokenBytes = new byte[byteLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);

        // URL-safe base64 encoding
        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
