using System.Net.Http.Json;
using System.Text.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.GitHub;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for GitHub integration operations (connect, sync, pull)
/// </summary>
public class GitHubIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public GitHubIntegrationService(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    // =========================================================================
    // Token/Account Status
    // =========================================================================

    /// <summary>
    /// Get current GitHub token status for the user.
    /// </summary>
    public async Task<GitHubTokenStatus?> GetTokenStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("github/status");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubTokenStatus>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// List repositories accessible to the user.
    /// </summary>
    public async Task<List<GitHubRepoDto>> ListRepositoriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("github/repos");
            if (!response.IsSuccessStatusCode)
                return new List<GitHubRepoDto>();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<GitHubRepoDto>>>(_jsonOptions);
            return result?.Data ?? new List<GitHubRepoDto>();
        }
        catch
        {
            return new List<GitHubRepoDto>();
        }
    }

    /// <summary>
    /// List branches for a repository.
    /// </summary>
    public async Task<List<GitHubBranchDto>> ListBranchesAsync(string owner, string repo)
    {
        try
        {
            var response = await _httpClient.GetAsync($"github/repos/{owner}/{repo}/branches");
            if (!response.IsSuccessStatusCode)
                return new List<GitHubBranchDto>();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<GitHubBranchDto>>>(_jsonOptions);
            return result?.Data ?? new List<GitHubBranchDto>();
        }
        catch
        {
            return new List<GitHubBranchDto>();
        }
    }

    /// <summary>
    /// Get directory contents from a repository.
    /// </summary>
    public async Task<List<GitHubFileDto>> GetDirectoryContentsAsync(string owner, string repo, string? path = null, string? branch = null)
    {
        try
        {
            var url = $"github/repos/{owner}/{repo}/contents";
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(path))
                queryParams.Add($"path={Uri.EscapeDataString(path)}");
            if (!string.IsNullOrEmpty(branch))
                queryParams.Add($"branch={Uri.EscapeDataString(branch)}");
            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new List<GitHubFileDto>();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<GitHubFileDto>>>(_jsonOptions);
            return result?.Data ?? new List<GitHubFileDto>();
        }
        catch
        {
            return new List<GitHubFileDto>();
        }
    }

    /// <summary>
    /// Auto-detect localization files in a repository.
    /// </summary>
    public async Task<DetectLocalizationResult?> DetectLocalizationFilesAsync(string owner, string repo, string branch, string? basePath = null)
    {
        try
        {
            var url = $"github/repos/{owner}/{repo}/detect?branch={Uri.EscapeDataString(branch)}";
            if (!string.IsNullOrEmpty(basePath))
                url += $"&basePath={Uri.EscapeDataString(basePath)}";

            var response = await _httpClient.PostAsync(url, null);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<DetectLocalizationResult>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    // =========================================================================
    // Project Connection
    // =========================================================================

    /// <summary>
    /// Get GitHub sync status for a project.
    /// </summary>
    public async Task<GitHubSyncStatus?> GetSyncStatusAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/github/status");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubSyncStatus>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Connect a GitHub repository to a project using OAuth.
    /// </summary>
    public async Task<(bool Success, string? Error)> ConnectRepositoryAsync(int projectId, ConnectGitHubRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/github/connect", request, _jsonOptions);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(errorContent) ? "Failed to connect repository" : errorContent);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Connect a GitHub repository to a project using Personal Access Token.
    /// </summary>
    public async Task<(bool Success, string? Error)> ConnectRepositoryWithPatAsync(int projectId, ConnectGitHubPatRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/github/connect-pat", request, _jsonOptions);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(errorContent) ? "Failed to connect repository" : errorContent);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Disconnect GitHub from a project.
    /// </summary>
    public async Task<(bool Success, string? Error)> DisconnectRepositoryAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"projects/{projectId}/github/disconnect", null);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(errorContent) ? "Failed to disconnect repository" : errorContent);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // =========================================================================
    // Sync Operations (Push to GitHub)
    // =========================================================================

    /// <summary>
    /// Trigger sync to GitHub (push changes).
    /// </summary>
    public async Task<GitHubSyncResult?> SyncToGitHubAsync(int projectId, TriggerSyncRequest? request = null)
    {
        try
        {
            request ??= new TriggerSyncRequest(null, true);
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/github/sync", request, _jsonOptions);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubSyncResult>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return new GitHubSyncResult(false, null, 0, "Failed to sync to GitHub");
        }
    }

    // =========================================================================
    // Pull Operations (Pull from GitHub)
    // =========================================================================

    /// <summary>
    /// Preview pull from GitHub (without applying changes).
    /// </summary>
    public async Task<GitHubPullResult?> PreviewPullAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/github/pull/preview");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new GitHubPullResult(false, $"Failed to preview: {errorContent}", 0, 0, 0, 0, 0, new List<GitHubPullConflict>(), null, new List<string>());
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubPullResult>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            return new GitHubPullResult(false, ex.Message, 0, 0, 0, 0, 0, new List<GitHubPullConflict>(), null, new List<string>());
        }
    }

    /// <summary>
    /// Pull from GitHub to Cloud.
    /// </summary>
    public async Task<GitHubPullResult?> PullFromGitHubAsync(int projectId, GitHubPullRequest? request = null)
    {
        try
        {
            request ??= new GitHubPullRequest(false, "prompt");
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/github/pull", request, _jsonOptions);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubPullResult>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            return new GitHubPullResult(false, ex.Message, 0, 0, 0, 0, 0, new List<GitHubPullConflict>(), null, new List<string>());
        }
    }

    /// <summary>
    /// Get pending conflicts for a project.
    /// </summary>
    public async Task<GitHubPullConflictSummary?> GetPendingConflictsAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/github/pull/conflicts");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubPullConflictSummary>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolve conflicts from a pull operation.
    /// </summary>
    public async Task<GitHubPullResult?> ResolveConflictsAsync(int projectId, GitHubPullConflictResolutionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/github/pull/resolve", request, _jsonOptions);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubPullResult>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            return new GitHubPullResult(false, ex.Message, 0, 0, 0, 0, 0, new List<GitHubPullConflict>(), null, new List<string>());
        }
    }

    // =========================================================================
    // User GitHub Account
    // =========================================================================

    /// <summary>
    /// Get enhanced GitHub status for user profile.
    /// </summary>
    public async Task<UserGitHubStatus?> GetUserGitHubStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("github/user-status");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserGitHubStatus>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get projects using the current user's GitHub token.
    /// </summary>
    public async Task<GitHubTokenUsage?> GetUserTokenUsageAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("github/usage");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubTokenUsage>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    // =========================================================================
    // Organization GitHub
    // =========================================================================

    /// <summary>
    /// Get GitHub status for an organization.
    /// </summary>
    public async Task<OrganizationGitHubStatus?> GetOrganizationGitHubStatusAsync(int organizationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"organizations/{organizationId}/github/status");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationGitHubStatus>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Connect organization GitHub using current user's token.
    /// </summary>
    public async Task<(bool Success, string? Error)> ConnectOrganizationGitHubAsync(int organizationId)
    {
        try
        {
            var request = new ConnectOrganizationGitHubRequest(true);
            var response = await _httpClient.PostAsJsonAsync($"organizations/{organizationId}/github/connect", request, _jsonOptions);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(errorContent) ? "Failed to connect organization GitHub" : errorContent);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Disconnect GitHub from organization.
    /// </summary>
    public async Task<(bool Success, string? Error)> DisconnectOrganizationGitHubAsync(int organizationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"organizations/{organizationId}/github/disconnect", null);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(errorContent) ? "Failed to disconnect organization GitHub" : errorContent);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Get projects using the organization's GitHub token.
    /// </summary>
    public async Task<GitHubTokenUsage?> GetOrganizationTokenUsageAsync(int organizationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"organizations/{organizationId}/github/usage");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubTokenUsage>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    // =========================================================================
    // Token Source Selection
    // =========================================================================

    /// <summary>
    /// Get available token sources for connecting a project.
    /// </summary>
    public async Task<AvailableTokenSources?> GetAvailableTokenSourcesAsync(int projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"projects/{projectId}/github/available-tokens");
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AvailableTokenSources>>(_jsonOptions);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }
}
