// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// HTTP client for LRM Cloud API with URL-based routing.
/// Handles authentication, request/response serialization, and error handling.
/// </summary>
public class CloudApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly RemoteUrl _remoteUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _accessToken;
    private string? _projectDirectory;
    private Func<Task<bool>>? _onTokenRefreshed;

    public CloudApiClient(RemoteUrl remoteUrl, HttpClient? httpClient = null)
    {
        _remoteUrl = remoteUrl ?? throw new ArgumentNullException(nameof(remoteUrl));
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_remoteUrl.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Sets the authentication token for API requests.
    /// </summary>
    public void SetAccessToken(string? accessToken)
    {
        _accessToken = accessToken;

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    /// <summary>
    /// Gets the current access token.
    /// </summary>
    public string? GetAccessToken() => _accessToken;

    #region Project API

    /// <summary>
    /// Gets project information from the cloud.
    /// </summary>
    public async Task<CloudProject> GetProjectAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<CloudProject>(_remoteUrl.ProjectApiUrl, cancellationToken);
        return response ?? throw new CloudApiException("Failed to retrieve project information");
    }

    /// <summary>
    /// Creates a new project in the cloud.
    /// </summary>
    public async Task<CloudProject> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var url = _remoteUrl.IsPersonalProject
            ? $"{_remoteUrl.ApiBaseUrl}/users/{_remoteUrl.Username}/projects"
            : $"{_remoteUrl.ApiBaseUrl}/projects/{_remoteUrl.Organization}";

        var response = await PostAsync<CloudProject>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to create project");
    }

    /// <summary>
    /// Updates project settings in the cloud.
    /// </summary>
    public async Task<CloudProject> UpdateProjectAsync(UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<CloudProject>(_remoteUrl.ProjectApiUrl, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to update project");
    }

    #endregion

    #region Configuration Sync API

    /// <summary>
    /// Gets the remote configuration (lrm.json) from the cloud.
    /// </summary>
    public async Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/configuration";
        var response = await GetAsync<ConfigurationSnapshot>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to retrieve configuration");
    }

    /// <summary>
    /// Updates the remote configuration (lrm.json) in the cloud.
    /// </summary>
    public async Task<ConfigurationSnapshot> UpdateConfigurationAsync(
        string configJson,
        string? baseVersion = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/configuration";
        var request = new UpdateConfigurationRequest
        {
            ConfigJson = configJson,
            BaseVersion = baseVersion
        };

        var response = await PutAsync<ConfigurationSnapshot>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to update configuration");
    }

    /// <summary>
    /// Gets configuration history from the cloud.
    /// </summary>
    public async Task<List<ConfigurationHistoryEntry>> GetConfigurationHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/configuration/history?limit={limit}";
        var response = await GetAsync<List<ConfigurationHistoryEntry>>(url, cancellationToken);
        return response ?? new List<ConfigurationHistoryEntry>();
    }

    #endregion

    #region Resource Sync API (V2 - File-Based)

    /// <summary>
    /// Pushes files to the cloud (V2 - file-based sync with incremental changes).
    /// </summary>
    public async Task<PushResponse> PushResourcesAsync(
        PushRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/sync/push";
        var response = await PostAsync<PushResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to push resources");
    }

    /// <summary>
    /// Pulls files from the cloud (V2 - generates files from database).
    /// </summary>
    public async Task<PullResponse> PullResourcesAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/sync/pull";
        var response = await GetAsync<PullResponse>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to pull resources");
    }

    /// <summary>
    /// Gets sync status from the cloud.
    /// </summary>
    public async Task<SyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/sync/status";
        var response = await GetAsync<SyncStatus>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to retrieve sync status");
    }

    #endregion

    #region Auth API

    /// <summary>
    /// Authenticates with email and password.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ApiBaseUrl}/auth/login";
        var request = new LoginRequest { Email = email, Password = password };

        var response = await PostAsync<LoginResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Login failed");
    }

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// </summary>
    public async Task<LoginResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ApiBaseUrl}/auth/refresh";
        var request = new RefreshTokenRequest { RefreshToken = refreshToken };

        var response = await PostAsync<LoginResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Token refresh failed");
    }

    /// <summary>
    /// Enables automatic token refresh when JWT expires.
    /// </summary>
    public void EnableAutoRefresh(string projectDirectory, Func<Task<bool>>? onTokenRefreshed = null)
    {
        _projectDirectory = projectDirectory;
        _onTokenRefreshed = onTokenRefreshed;
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_projectDirectory))
            return false;

        try
        {
            var refreshToken = await AuthTokenManager.GetRefreshTokenAsync(
                _projectDirectory, _remoteUrl.Host, cancellationToken);

            if (string.IsNullOrEmpty(refreshToken))
                return false;

            var response = await RefreshTokenAsync(refreshToken, cancellationToken);

            // Save new tokens
            await AuthTokenManager.SetAuthenticationAsync(
                _projectDirectory,
                _remoteUrl.Host,
                response.Token,
                response.ExpiresAt,
                response.RefreshToken,
                response.RefreshTokenExpiresAt,
                cancellationToken);

            // Update client
            SetAccessToken(response.Token);

            // Notify callback
            if (_onTokenRefreshed != null)
                await _onTokenRefreshed();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    #region HTTP Helpers

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, cancellationToken);
            return apiResponse != null ? apiResponse.Data : default;
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private async Task<T?> PostAsync<T>(string url, object request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, cancellationToken);
            return apiResponse != null ? apiResponse.Data : default;
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private async Task<T?> PutAsync<T>(string url, object request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, cancellationToken);
            return apiResponse != null ? apiResponse.Data : default;
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            // If 401 and auto-refresh is enabled, try to refresh token
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                && !string.IsNullOrEmpty(_projectDirectory))
            {
                var refreshed = await TryRefreshTokenAsync(cancellationToken);
                if (refreshed)
                {
                    // Token refreshed - throw special exception so caller can retry
                    throw new CloudApiException("Token expired and refreshed", 401);
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;

            ErrorResponse? errorResponse = null;
            try
            {
                errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorContent, _jsonOptions);
            }
            catch
            {
                // Ignore JSON parsing errors
            }

            var message = errorResponse?.Message ?? $"HTTP {statusCode}: {response.ReasonPhrase}";
            throw new CloudApiException(message, statusCode);
        }
    }

    #endregion

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// API response wrapper matching the cloud API structure.
/// </summary>
internal class ApiResponse<T>
{
    public T Data { get; set; } = default!;
    public ApiMeta? Meta { get; set; }
}

/// <summary>
/// Metadata for API responses.
/// </summary>
internal class ApiMeta
{
    public DateTime Timestamp { get; set; }
}

#region Models

/// <summary>
/// Represents a cloud project.
/// </summary>
public class CloudProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OrganizationId { get; set; }
    public string? OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to create a new project.
/// </summary>
public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Request to update a project.
/// </summary>
public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Represents a configuration snapshot with versioning.
/// </summary>
public class ConfigurationSnapshot
{
    public string ConfigJson { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request to update configuration.
/// </summary>
public class UpdateConfigurationRequest
{
    public string ConfigJson { get; set; } = string.Empty;
    public string? BaseVersion { get; set; }
}

/// <summary>
/// Configuration history entry.
/// </summary>
public class ConfigurationHistoryEntry
{
    public string Version { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>
/// Represents a resource file for V2 sync.
/// </summary>
public class FileDto
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Hash { get; set; }
}

/// <summary>
/// Request to push resources (V2 - file-based with incremental changes).
/// </summary>
public class PushRequest
{
    public string? Configuration { get; set; }
    public List<FileDto> ModifiedFiles { get; set; } = new();
    public List<string> DeletedFiles { get; set; } = new();
}

/// <summary>
/// Response from push operation (V2).
/// </summary>
public class PushResponse
{
    public bool Success { get; set; }
    public int ModifiedCount { get; set; }
    public int DeletedCount { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response from pull operation (V2).
/// </summary>
public class PullResponse
{
    public string? Configuration { get; set; }
    public List<FileDto> Files { get; set; } = new();
}

/// <summary>
/// Sync status information.
/// </summary>
public class SyncStatus
{
    public bool IsSynced { get; set; }
    public DateTime? LastPush { get; set; }
    public DateTime? LastPull { get; set; }
    public int LocalChanges { get; set; }
    public int RemoteChanges { get; set; }
}

/// <summary>
/// Error response from API.
/// </summary>
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

/// <summary>
/// Request for email/password login.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Response from login operation.
/// </summary>
public class LoginResponse
{
    public UserInfo User { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}

/// <summary>
/// User information returned from authentication.
/// </summary>
public class UserInfo
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool EmailVerified { get; set; }
}

/// <summary>
/// Request for token refresh.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

#endregion
