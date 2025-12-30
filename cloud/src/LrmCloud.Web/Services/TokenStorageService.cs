using System.Globalization;
using Blazored.LocalStorage;

namespace LrmCloud.Web.Services;

/// <summary>
/// Manages JWT token storage in browser localStorage
/// </summary>
public class TokenStorageService
{
    private const string AccessTokenKey = "lrm_access_token";
    private const string RefreshTokenKey = "lrm_refresh_token";
    private const string TokenExpiryKey = "lrm_token_expiry";
    private const string RefreshExpiryKey = "lrm_refresh_expiry";

    private readonly ILocalStorageService _localStorage;
    private volatile bool _isClearing;

    /// <summary>
    /// Indicates if a token clear operation is currently in progress.
    /// Used to prevent race conditions during logout.
    /// </summary>
    public bool IsClearing => _isClearing;

    public TokenStorageService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task StoreTokensAsync(string accessToken, string refreshToken, DateTime expiresAt, DateTime refreshExpiresAt)
    {
        await _localStorage.SetItemAsStringAsync(AccessTokenKey, accessToken);
        await _localStorage.SetItemAsStringAsync(RefreshTokenKey, refreshToken);
        await _localStorage.SetItemAsStringAsync(TokenExpiryKey, expiresAt.ToString("O"));
        await _localStorage.SetItemAsStringAsync(RefreshExpiryKey, refreshExpiresAt.ToString("O"));
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        return await _localStorage.GetItemAsStringAsync(AccessTokenKey);
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        return await _localStorage.GetItemAsStringAsync(RefreshTokenKey);
    }

    public async Task<DateTime?> GetTokenExpiryAsync()
    {
        var expiry = await _localStorage.GetItemAsStringAsync(TokenExpiryKey);
        if (string.IsNullOrEmpty(expiry)) return null;
        // Use RoundtripKind to preserve UTC timezone from ISO 8601 "Z" suffix
        return DateTimeOffset.TryParse(expiry, null, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.UtcDateTime
            : null;
    }

    public async Task<DateTime?> GetRefreshExpiryAsync()
    {
        var expiry = await _localStorage.GetItemAsStringAsync(RefreshExpiryKey);
        if (string.IsNullOrEmpty(expiry)) return null;
        // Use RoundtripKind to preserve UTC timezone from ISO 8601 "Z" suffix
        return DateTimeOffset.TryParse(expiry, null, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.UtcDateTime
            : null;
    }

    public async Task<bool> IsTokenExpiredAsync()
    {
        var expiry = await GetTokenExpiryAsync();
        if (!expiry.HasValue) return true;
        // Consider expired if within 30 seconds of expiry
        return DateTime.UtcNow.AddSeconds(30) >= expiry.Value;
    }

    public async Task<bool> CanRefreshAsync()
    {
        var refreshExpiry = await GetRefreshExpiryAsync();
        if (!refreshExpiry.HasValue) return false;
        return DateTime.UtcNow < refreshExpiry.Value;
    }

    public async Task ClearTokensAsync()
    {
        _isClearing = true;
        try
        {
            // Use Task.WhenAll to clear all tokens atomically
            // This prevents race conditions where IsAuthenticatedAsync might read
            // a partially cleared state during logout
            await Task.WhenAll(
                _localStorage.RemoveItemAsync(AccessTokenKey).AsTask(),
                _localStorage.RemoveItemAsync(RefreshTokenKey).AsTask(),
                _localStorage.RemoveItemAsync(TokenExpiryKey).AsTask(),
                _localStorage.RemoveItemAsync(RefreshExpiryKey).AsTask()
            );
        }
        finally
        {
            _isClearing = false;
        }
    }
}
