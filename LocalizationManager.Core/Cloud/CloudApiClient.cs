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

    #region Resource Sync API

    /// <summary>
    /// Gets resource files from the cloud.
    /// </summary>
    public async Task<List<ResourceFile>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/resources";
        var response = await GetAsync<List<ResourceFile>>(url, cancellationToken);
        return response ?? new List<ResourceFile>();
    }

    /// <summary>
    /// Uploads resource files to the cloud.
    /// </summary>
    public async Task<PushResult> PushResourcesAsync(
        List<ResourceFile> resources,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_remoteUrl.ProjectApiUrl}/resources/push";
        var request = new PushRequest
        {
            Resources = resources,
            Message = message
        };

        var response = await PostAsync<PushResult>(url, request, cancellationToken);
        return response ?? throw new CloudApiException("Failed to push resources");
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

    #region HTTP Helpers

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
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
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
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
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CloudApiException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
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
/// Represents a resource file.
/// </summary>
public class ResourceFile
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Hash { get; set; }
}

/// <summary>
/// Request to push resources.
/// </summary>
public class PushRequest
{
    public List<ResourceFile> Resources { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Result of push operation.
/// </summary>
public class PushResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int FilesUpdated { get; set; }
    public DateTime PushedAt { get; set; }
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

#endregion
