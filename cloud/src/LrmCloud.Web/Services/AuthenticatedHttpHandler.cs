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
    private bool _isRefreshing;

    public AuthenticatedHttpHandler(TokenStorageService tokenStorage, IServiceProvider serviceProvider)
    {
        _tokenStorage = tokenStorage;
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Skip auth header for auth endpoints (except /auth/me and /auth/logout)
        var path = request.RequestUri?.PathAndQuery ?? "";
        var isAuthEndpoint = path.Contains("/auth/") &&
                            !path.Contains("/auth/me") &&
                            !path.Contains("/auth/logout");

        if (!isAuthEndpoint)
        {
            await AddAuthHeaderAsync(request);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Handle 401 Unauthorized - try to refresh token
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !isAuthEndpoint && !_isRefreshing)
        {
            if (await TryRefreshTokenAsync())
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
        if (_isRefreshing)
            return false;

        _isRefreshing = true;
        try
        {
            if (!await _tokenStorage.CanRefreshAsync())
                return false;

            // Get AuthService from service provider (avoiding circular dependency)
            var authService = _serviceProvider.GetService<AuthService>();
            if (authService == null)
                return false;

            return await authService.RefreshTokenAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
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
