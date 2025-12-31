using System.Net.Http.Headers;

namespace LrmCloud.Web.Services;

/// <summary>
/// HTTP message handler that automatically adds the JWT token to outgoing requests
/// and handles token refresh when needed
/// </summary>
public class AuthenticatedHttpHandler : DelegatingHandler
{
    private readonly TokenStorageService _tokenStorage;
    private readonly IServiceProvider _serviceProvider;

    public AuthenticatedHttpHandler(TokenStorageService tokenStorage, IServiceProvider serviceProvider)
    {
        _tokenStorage = tokenStorage;
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Skip auth header for auth endpoints (except ones that require authentication)
        var path = request.RequestUri?.PathAndQuery ?? "";
        var isAuthEndpoint = path.Contains("/auth/") &&
                            !path.Contains("/auth/me") &&
                            !path.Contains("/auth/logout") &&
                            !path.Contains("/auth/github/link/initiate") &&
                            !path.Contains("/auth/github/unlink") &&
                            !path.Contains("/auth/change-password") &&
                            !path.Contains("/auth/profile") &&
                            !path.Contains("/auth/change-email");

        if (!isAuthEndpoint)
        {
            // Wait if a refresh is in progress before adding auth header
            // This prevents sending requests with stale tokens during refresh
            var coordinator = _serviceProvider.GetService<TokenRefreshCoordinator>();
            if (coordinator?.IsRefreshInProgress == true)
            {
                await coordinator.WaitForRefreshAsync(TokenRefreshCoordinator.MaxRefreshWaitTime, cancellationToken);
            }

            // Proactive refresh: if token is expired or about to expire, refresh before sending
            // Check coordinator state first to avoid unnecessary attempts
            var authService = _serviceProvider.GetService<AuthService>();
            if (await _tokenStorage.IsTokenExpiredAsync() &&
                await _tokenStorage.CanRefreshAsync() &&
                authService?.ShouldAttemptRefresh() == true)
            {
                await TryRefreshTokenAsync();
            }

            await AddAuthHeaderAsync(request);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Handle 401 Unauthorized - try to refresh token (if not permanently failed)
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            var authSvc = _serviceProvider.GetService<AuthService>();
            if (authSvc?.ShouldAttemptRefresh() == true && await TryRefreshTokenAsync())
            {
                // Retry the request with new token
                request = await CloneRequestAsync(request);
                await AddAuthHeaderAsync(request);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }

    private async Task AddAuthHeaderAsync(HttpRequestMessage request)
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        if (!await _tokenStorage.CanRefreshAsync())
            return false;

        // Get AuthService from service provider (avoiding circular dependency)
        // AuthService now uses TokenRefreshCoordinator internally for synchronization
        var authService = _serviceProvider.GetService<AuthService>();
        if (authService == null)
            return false;

        return await authService.RefreshTokenAsync();
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Copy headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStreamAsync();
            if (content.CanSeek)
            {
                content.Position = 0;
            }
            clone.Content = new StreamContent(content);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
