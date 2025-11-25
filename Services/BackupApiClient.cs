using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for backup management operations.
/// </summary>
public class BackupApiClient
{
    private readonly HttpClient _httpClient;

    public BackupApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// List backups for a specific file or all files.
    /// </summary>
    public async Task<BackupListResponse?> ListBackupsAsync(string? fileName = null)
    {
        var url = "/api/backup";
        if (!string.IsNullOrEmpty(fileName))
        {
            url += $"?fileName={Uri.EscapeDataString(fileName)}";
        }
        return await _httpClient.GetFromJsonAsync<BackupListResponse>(url);
    }

    /// <summary>
    /// Create a new backup.
    /// </summary>
    public async Task<CreateBackupResponse?> CreateBackupAsync(CreateBackupRequest? request = null)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/backup", request ?? new CreateBackupRequest());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateBackupResponse>();
    }

    /// <summary>
    /// Get backup details.
    /// </summary>
    public async Task<BackupInfo?> GetBackupAsync(string fileName, int version)
    {
        return await _httpClient.GetFromJsonAsync<BackupInfo>($"/api/backup/{Uri.EscapeDataString(fileName)}/{version}");
    }

    /// <summary>
    /// Restore a backup (with optional preview).
    /// </summary>
    public async Task<RestoreBackupResponse?> RestoreBackupAsync(string fileName, int version, bool preview = false)
    {
        var request = new RestoreBackupRequest { Preview = preview };
        var response = await _httpClient.PostAsJsonAsync($"/api/backup/{Uri.EscapeDataString(fileName)}/{version}/restore", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestoreBackupResponse>();
    }

    /// <summary>
    /// Delete a specific backup version.
    /// </summary>
    public async Task<DeleteBackupResponse?> DeleteBackupAsync(string fileName, int version)
    {
        var response = await _httpClient.DeleteAsync($"/api/backup/{Uri.EscapeDataString(fileName)}/{version}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeleteBackupResponse>();
    }

    /// <summary>
    /// Delete all backups for a file.
    /// </summary>
    public async Task<OperationResponse?> DeleteAllBackupsAsync(string fileName)
    {
        var response = await _httpClient.DeleteAsync($"/api/backup/{Uri.EscapeDataString(fileName)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OperationResponse>();
    }
}
