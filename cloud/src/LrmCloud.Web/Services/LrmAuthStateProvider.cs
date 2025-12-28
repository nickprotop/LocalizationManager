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

    public LrmAuthStateProvider(TokenStorageService tokenStorage, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _tokenStorage = tokenStorage;
        _httpClient = httpClient;
        _serviceProvider = serviceProvider;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Check if token is expired
        if (await _tokenStorage.IsTokenExpiredAsync())
        {
            // Token expired, try to refresh if possible
            if (await _tokenStorage.CanRefreshAsync())
            {
                try
                {
                    // Attempt to refresh the token using AuthService
                    // AuthService uses TokenRefreshCoordinator internally to prevent concurrent refreshes
                    var authService = _serviceProvider.GetService<AuthService>();
                    if (authService != null && await authService.RefreshTokenAsync())
                    {
                        // Refresh succeeded - the AuthService already notified us
                        // and updated the cached user, so we can proceed
                        token = await _tokenStorage.GetAccessTokenAsync();
                    }
                    else
                    {
                        // Refresh failed - return unauthenticated
                        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                    }
                }
                catch
                {
                    // Error during refresh - return unauthenticated
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }
            }
            else
            {
                // Can't refresh - return unauthenticated
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
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
        var authenticatedUser = CreateClaimsPrincipal(user);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(authenticatedUser)));
    }

    public void NotifyUserLogout()
    {
        _cachedUser = null;
        _isInitialized = false;
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
