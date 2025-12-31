using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using LrmCloud.Shared.DTOs.Auth;

namespace LrmCloud.Web.Services;

/// <summary>
/// Custom AuthenticationStateProvider for managing user authentication state in Blazor WASM
/// </summary>
public class LrmAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStorageService _tokenStorage;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private UserDto? _cachedUser;
    private bool _isInitialized;

    // Cache auth state to prevent multiple simultaneous evaluations
    private AuthenticationState? _cachedAuthState;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(500);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public LrmAuthStateProvider(TokenStorageService tokenStorage, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _tokenStorage = tokenStorage;
        _httpClient = httpClient;
        _serviceProvider = serviceProvider;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Return cached state if still valid (prevents multiple simultaneous evaluations)
        if (_cachedAuthState != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedAuthState;
        }

        // Use lock to prevent concurrent cache population
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedAuthState != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedAuthState;
            }

            var authState = await GetAuthenticationStateCoreAsync();
            _cachedAuthState = authState;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            return authState;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<AuthenticationState> GetAuthenticationStateCoreAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Check if token is expired
        // NOTE: We do NOT trigger refresh here - that's handled by AuthenticatedHttpHandler
        // This prevents multiple refresh attempts from different components
        if (await _tokenStorage.IsTokenExpiredAsync())
        {
            // Token expired - return unauthenticated
            // The next API call via AuthenticatedHttpHandler will trigger refresh
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Try to get user info from cache or fetch from API
        if (_cachedUser == null && !_isInitialized)
        {
            _cachedUser = await FetchCurrentUserAsync();
            // Only mark as initialized if fetch succeeded - allows retry on failure
            _isInitialized = _cachedUser != null;
        }

        if (_cachedUser == null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        return new AuthenticationState(CreateClaimsPrincipal(_cachedUser));
    }

    public void NotifyUserAuthentication(UserDto user)
    {
        _cachedUser = user;
        _cachedAuthState = null; // Invalidate cache
        _cacheExpiry = DateTime.MinValue;
        var authenticatedUser = CreateClaimsPrincipal(user);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authenticatedUser)));
    }

    public void NotifyUserLogout()
    {
        _cachedUser = null;
        _isInitialized = false;
        _cachedAuthState = null; // Invalidate cache
        _cacheExpiry = DateTime.MinValue;
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new("plan", user.Plan),
            new("email_verified", user.EmailVerified.ToString().ToLower()),
            new("is_superadmin", user.IsSuperAdmin.ToString().ToLower()),
            new("must_change_password", user.MustChangePassword.ToString().ToLower())
        };

        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }

        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            claims.Add(new Claim("avatar_url", user.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new ClaimsPrincipal(identity);
    }

    private async Task<UserDto?> FetchCurrentUserAsync()
    {
        try
        {
            var token = await _tokenStorage.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            // Create a new request with the auth header
            var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LrmCloud.Shared.Api.ApiResponse<UserDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors during initial fetch
        }

        return null;
    }
}
