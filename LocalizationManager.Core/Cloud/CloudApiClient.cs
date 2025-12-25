// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LocalizationManager.Core.Cloud.Models;

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
    private string? _apiKey;
    private string? _projectDirectory;
    private Func<Task<bool>>? _onTokenRefreshed;
    private int? _cachedProjectId;

    public CloudApiClient(RemoteUrl remoteUrl, HttpClient? httpClient = null)
    {
        _remoteUrl = remoteUrl ?? throw new ArgumentNullException(nameof(remoteUrl));
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_remoteUrl.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
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

    /// <summary>
    /// Sets the API key for authentication (takes priority over JWT).
    /// Uses X-API-Key header.
    /// </summary>
    public void SetApiKey(string? apiKey)
    {
        _apiKey = apiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Remove Bearer authorization if set
            _httpClient.DefaultRequestHeaders.Authorization = null;
            // Remove existing X-API-Key header if any
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            // Add new X-API-Key header
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        }
    }

    /// <summary>
    /// Gets the current API key.
    /// </summary>
    public string? GetApiKey() => _apiKey;

    /// <summary>
    /// Returns true if using API key authentication instead of JWT.
    /// </summary>
    public bool IsUsingApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    #region Project API

    /// <summary>
    /// Gets all projects accessible by the current user.
    /// </summary>
    public async Task<List<CloudProject>> GetUserProjectsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ApiBaseUrl}/projects";
        var response = await GetAsync<List<CloudProject>>(url, cancellationToken);
        return response ?? new List<CloudProject>();
    }

    /// <summary>
    /// Gets project information from the cloud.
    /// </summary>
    public async Task<CloudProject> GetProjectAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<CloudProject>(_remoteUrl.ProjectApiUrl, cancellationToken);
        if (response == null)
            throw new CloudApiException("Failed to retrieve project information");

        // Cache the project ID for sync operations
        _cachedProjectId = response.Id;
        return response;
    }

    /// <summary>
    /// Gets the project ID, fetching from the API if not already cached.
    /// </summary>
    private async Task<int> GetProjectIdAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedProjectId.HasValue)
            return _cachedProjectId.Value;

        var project = await GetProjectAsync(cancellationToken);
        return project.Id;
    }

    /// <summary>
    /// Builds the sync API URL for the current project.
    /// Uses /api/projects/{projectId} route for sync operations.
    /// </summary>
    private async Task<string> GetSyncApiUrlAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var projectId = await GetProjectIdAsync(cancellationToken);
        return $"{_remoteUrl.ApiBaseUrl}/projects/{projectId}/sync/{endpoint}";
    }

    /// <summary>
    /// Builds the snapshot API URL for the current project.
    /// Uses /api/projects/{projectId} route for snapshot operations.
    /// </summary>
    private async Task<string> GetSnapshotApiUrlAsync(string endpoint = "", CancellationToken cancellationToken = default)
    {
        var projectId = await GetProjectIdAsync(cancellationToken);
        var baseUrl = $"{_remoteUrl.ApiBaseUrl}/projects/{projectId}/snapshots";
        return string.IsNullOrEmpty(endpoint) ? baseUrl : $"{baseUrl}/{endpoint}";
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

    #region Key-Level Sync API

    /// <summary>
    /// Pushes entry-level changes to the cloud with three-way merge support.
    /// </summary>
    public async Task<Models.KeySyncPushResponse> KeySyncPushAsync(
        Models.KeySyncPushRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync("push", cancellationToken);
        var response = await PostAsync<Models.KeySyncPushResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to push changes");
    }

    /// <summary>
    /// Pulls all entries from the cloud for key-level merge.
    /// </summary>
    /// <param name="since">Optional timestamp for delta sync (only entries modified after this time).</param>
    /// <param name="limit">Optional limit for pagination.</param>
    /// <param name="offset">Optional offset for pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Models.KeySyncPullResponse> KeySyncPullAsync(
        DateTime? since = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync("pull", cancellationToken);
        var queryParams = new List<string>();

        if (since.HasValue)
        {
            queryParams.Add($"since={since.Value:O}");
        }
        if (limit.HasValue)
        {
            queryParams.Add($"limit={limit.Value}");
        }
        if (offset.HasValue)
        {
            queryParams.Add($"offset={offset.Value}");
        }

        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var response = await GetAsync<Models.KeySyncPullResponse>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to pull entries");
    }

    /// <summary>
    /// Resolves conflicts after a push operation.
    /// </summary>
    public async Task<Models.ConflictResolutionResponse> KeySyncResolveAsync(
        Models.ConflictResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync("resolve", cancellationToken);
        var response = await PostAsync<Models.ConflictResolutionResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to resolve conflicts");
    }

    /// <summary>
    /// Gets the sync history for the current project.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Models.SyncHistoryListResponse> GetSyncHistoryAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync($"history?page={page}&pageSize={pageSize}", cancellationToken);
        var response = await GetAsync<Models.SyncHistoryListResponse>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to get sync history");
    }

    /// <summary>
    /// Gets detailed information about a specific history entry.
    /// </summary>
    /// <param name="historyId">The history entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Models.SyncHistoryDetailDto> GetSyncHistoryDetailAsync(
        string historyId,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync($"history/{historyId}", cancellationToken);
        var response = await GetAsync<Models.SyncHistoryDetailDto>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to get history details");
    }

    /// <summary>
    /// Reverts the project to the state before a specific push.
    /// </summary>
    /// <param name="historyId">The history entry ID to revert.</param>
    /// <param name="message">Optional message describing the revert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Models.RevertResponse> RevertSyncHistoryAsync(
        string historyId,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSyncApiUrlAsync($"history/{historyId}/revert", cancellationToken);
        var request = new Models.RevertRequest { Message = message };
        var response = await PostAsync<Models.RevertResponse>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to revert changes");
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

    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    public async Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ApiBaseUrl}/auth/me";
        var response = await GetAsync<UserProfile>(url, cancellationToken);
        return response ?? throw new CloudApiException("Failed to retrieve user profile");
    }

    /// <summary>
    /// Gets all organizations the current user is a member of.
    /// </summary>
    public async Task<List<CloudOrganization>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ApiBaseUrl}/organizations";
        var response = await GetAsync<List<CloudOrganization>>(url, cancellationToken);
        return response ?? new List<CloudOrganization>();
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check if auto-refresh is enabled
        if (string.IsNullOrEmpty(_projectDirectory))
        {
            return false;
        }

        try
        {
            // Load current cloud config to get refresh token
            var cloudConfig = await CloudConfigManager.LoadAsync(_projectDirectory, cancellationToken);

            if (string.IsNullOrWhiteSpace(cloudConfig.RefreshToken))
            {
                return false;
            }

            // Check if refresh token is still valid
            if (cloudConfig.RefreshTokenExpiresAt.HasValue && cloudConfig.RefreshTokenExpiresAt.Value <= DateTime.UtcNow)
            {
                return false;
            }

            // Call refresh endpoint directly (bypass normal request path to avoid recursion)
            var url = $"{_remoteUrl.ApiBaseUrl}/auth/refresh";
            var request = new RefreshTokenRequest { RefreshToken = cloudConfig.RefreshToken };

            // Remove auth header temporarily to avoid sending expired token
            var currentAuth = _httpClient.DefaultRequestHeaders.Authorization;
            _httpClient.DefaultRequestHeaders.Authorization = null;

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions, cancellationToken);
                var loginResponse = apiResponse?.Data;

                if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.Token))
                {
                    return false;
                }

                // Update the access token in this client
                SetAccessToken(loginResponse.Token);

                // Save the new tokens to config
                await CloudConfigManager.SetAuthenticationAsync(
                    _projectDirectory,
                    loginResponse.Token,
                    loginResponse.ExpiresAt,
                    loginResponse.RefreshToken,
                    loginResponse.RefreshTokenExpiresAt,
                    cancellationToken);

                // Notify callback if registered
                if (_onTokenRefreshed != null)
                {
                    await _onTokenRefreshed();
                }

                return true;
            }
            finally
            {
                // Restore auth header if refresh failed
                if (_httpClient.DefaultRequestHeaders.Authorization == null && currentAuth != null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = currentAuth;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Snapshot API

    /// <summary>
    /// Lists all snapshots for the current project.
    /// </summary>
    public async Task<List<CloudSnapshot>> ListSnapshotsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetSnapshotApiUrlAsync(cancellationToken: cancellationToken);
        var url = $"{baseUrl}?page={page}&pageSize={pageSize}";
        var response = await GetAsync<List<CloudSnapshot>>(url, cancellationToken);
        return response ?? new List<CloudSnapshot>();
    }

    /// <summary>
    /// Gets details of a specific snapshot.
    /// </summary>
    public async Task<SnapshotDetail?> GetSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSnapshotApiUrlAsync(snapshotId, cancellationToken);
        var response = await GetAsync<SnapshotDetail>(url, cancellationToken);
        return response;
    }

    /// <summary>
    /// Creates a new manual snapshot.
    /// </summary>
    public async Task<CloudSnapshot> CreateSnapshotAsync(
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSnapshotApiUrlAsync(cancellationToken: cancellationToken);
        var request = new CreateSnapshotApiRequest { Description = description };
        var response = await PostAsync<CloudSnapshot>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to create snapshot");
    }

    /// <summary>
    /// Restores from a snapshot.
    /// </summary>
    public async Task<CloudSnapshot> RestoreSnapshotAsync(
        string snapshotId,
        bool createBackup = true,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSnapshotApiUrlAsync($"{snapshotId}/restore", cancellationToken);
        var request = new RestoreSnapshotApiRequest
        {
            CreateBackup = createBackup,
            Message = message
        };
        var response = await PostAsync<CloudSnapshot>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to restore snapshot");
    }

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    public async Task DeleteSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSnapshotApiUrlAsync(snapshotId, cancellationToken);
        await DeleteAsync(url, cancellationToken);
    }

    /// <summary>
    /// Compares two snapshots.
    /// </summary>
    public async Task<SnapshotDiff?> DiffSnapshotsAsync(
        string fromSnapshotId,
        string toSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var url = await GetSnapshotApiUrlAsync($"{fromSnapshotId}/diff/{toSnapshotId}", cancellationToken);
        var response = await GetAsync<SnapshotDiff>(url, cancellationToken);
        return response;
    }

    #endregion

    #region HTTP Helpers

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            // Try to refresh token and retry once on 401
            if (!response.IsSuccessStatusCode && await ShouldRetryAfterTokenRefreshAsync(response, cancellationToken))
            {
                response = await _httpClient.GetAsync(url, cancellationToken);
            }

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

            // Try to refresh token and retry once on 401
            if (!response.IsSuccessStatusCode && await ShouldRetryAfterTokenRefreshAsync(response, cancellationToken))
            {
                response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            }

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

            // Try to refresh token and retry once on 401
            if (!response.IsSuccessStatusCode && await ShouldRetryAfterTokenRefreshAsync(response, cancellationToken))
            {
                response = await _httpClient.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            }

            await EnsureSuccessAsync(response, cancellationToken);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, cancellationToken);
            return apiResponse != null ? apiResponse.Data : default;
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private async Task DeleteAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            // Try to refresh token and retry once on 401
            if (!response.IsSuccessStatusCode && await ShouldRetryAfterTokenRefreshAsync(response, cancellationToken))
            {
                response = await _httpClient.DeleteAsync(url, cancellationToken);
            }

            await EnsureSuccessAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a 401 response can be retried after token refresh.
    /// Returns true if the token was refreshed and the request should be retried.
    /// </summary>
    private async Task<bool> ShouldRetryAfterTokenRefreshAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && !IsUsingApiKey  // Don't refresh for API key auth
            && !string.IsNullOrEmpty(_projectDirectory))
        {
            return await TryRefreshTokenAsync(cancellationToken);
        }
        return false;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
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
public class ApiMeta
{
    public DateTime Timestamp { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public int? TotalCount { get; set; }
    public int? TotalPages { get; set; }
}

#region Models

/// <summary>
/// Represents a cloud project.
/// </summary>
public class CloudProject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }

    // Format and sync settings
    public string Format { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "en";
    public string LocalizationPath { get; set; } = ".";

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
    public string Format { get; set; } = "json";
    public string DefaultLanguage { get; set; } = "en";
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
    public string? Message { get; set; }
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

    /// <summary>
    /// Number of translations excluded due to workflow requirements.
    /// Only populated when project has review workflow enabled.
    /// </summary>
    public int ExcludedTranslationCount { get; set; }

    /// <summary>
    /// Informational message about excluded translations due to workflow.
    /// Null if no translations were excluded.
    /// </summary>
    public string? WorkflowMessage { get; set; }
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
/// Extended user profile with subscription and usage information.
/// </summary>
public class UserProfile
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool EmailVerified { get; set; }
    public string AuthType { get; set; } = "email";
    public long? GitHubId { get; set; }
    public string Plan { get; set; } = "free";
    // LRM Translation usage (counts against plan)
    public int TranslationCharsUsed { get; set; }
    public int TranslationCharsLimit { get; set; }
    public DateTime? TranslationCharsResetAt { get; set; }
    // BYOK usage (tracked but unlimited)
    public long ByokCharsUsed { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Organization information for the current user.
/// </summary>
public class CloudOrganization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public string Plan { get; set; } = "free";
    public int MemberCount { get; set; }
    public string UserRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request for token refresh.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

#endregion

#region Snapshot Models

/// <summary>
/// Represents a cloud snapshot.
/// </summary>
public class CloudSnapshot
{
    public int Id { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string? Description { get; set; }
    public string SnapshotType { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int KeyCount { get; set; }
    public int TranslationCount { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Snapshot details including file list.
/// </summary>
public class SnapshotDetail : CloudSnapshot
{
    public List<SnapshotFile> Files { get; set; } = new();
}

/// <summary>
/// File within a snapshot.
/// </summary>
public class SnapshotFile
{
    public string Path { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// Response from listing snapshots (paginated).
/// Matches ApiResponse&lt;List&lt;T&gt;&gt; from the API.
/// </summary>
public class SnapshotListResponse
{
    public List<CloudSnapshot> Data { get; set; } = new();
    public ApiMeta? Meta { get; set; }

    // Convenience properties for backward compatibility
    public List<CloudSnapshot> Items => Data;
    public int Page => Meta?.Page ?? 1;
    public int PageSize => Meta?.PageSize ?? 20;
    public int TotalCount => Meta?.TotalCount ?? 0;
}


/// <summary>
/// Request to create a snapshot.
/// </summary>
public class CreateSnapshotApiRequest
{
    public string? Description { get; set; }
}

/// <summary>
/// Request to restore a snapshot.
/// </summary>
public class RestoreSnapshotApiRequest
{
    public bool CreateBackup { get; set; } = true;
    public string? Message { get; set; }
}

/// <summary>
/// Diff between two snapshots.
/// </summary>
public class SnapshotDiff
{
    public string FromSnapshotId { get; set; } = string.Empty;
    public string ToSnapshotId { get; set; } = string.Empty;
    public List<SnapshotDiffFile> Files { get; set; } = new();
    public int KeysAdded { get; set; }
    public int KeysRemoved { get; set; }
    public int KeysModified { get; set; }
}

/// <summary>
/// File difference in a snapshot comparison.
/// </summary>
public class SnapshotDiffFile
{
    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
}

#endregion
