using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Projects;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for project CRUD operations
/// </summary>
public class ProjectService
{
    private readonly HttpClient _httpClient;

    public ProjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(int? organizationId = null)
    {
        var url = organizationId.HasValue
            ? $"projects?organizationId={organizationId}"
            : "projects";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProjectDto>>>();
        return result?.Data ?? new List<ProjectDto>();
    }

    public async Task<ProjectDto?> GetProjectAsync(int id)
    {
        var response = await _httpClient.GetAsync($"projects/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDto>>();
        return result?.Data;
    }

    public async Task<ServiceResult<ProjectDto>> CreateProjectAsync(CreateProjectRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("projects", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<ProjectDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ProjectDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectDto>.Failure($"Failed to create project: {ex.Message}");
        }
    }

    public async Task<ServiceResult<ProjectDto>> UpdateProjectAsync(int id, UpdateProjectRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"projects/{id}", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDto>>();
                if (result?.Data != null)
                {
                    return ServiceResult<ProjectDto>.Success(result.Data);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ProjectDto>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectDto>.Failure($"Failed to update project: {ex.Message}");
        }
    }

    public async Task<ServiceResult> DeleteProjectAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"projects/{id}");

            if (response.IsSuccessStatusCode)
            {
                return ServiceResult.Success("Project deleted successfully");
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure($"Failed to delete project: {ex.Message}");
        }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        try
        {
            var projects = await GetProjectsAsync();

            return new DashboardStats
            {
                ProjectCount = projects.Count,
                TotalKeys = projects.Sum(p => p.KeyCount),
                AverageCompletion = projects.Count > 0
                    ? projects.Average(p => p.CompletionPercentage)
                    : 0
            };
        }
        catch
        {
            return new DashboardStats();
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("\"detail\""))
            {
                var problem = System.Text.Json.JsonDocument.Parse(content);
                if (problem.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "An error occurred";
                }
            }
            return content.Length < 200 ? content : "An error occurred";
        }
        catch
        {
            return $"Request failed with status {response.StatusCode}";
        }
    }
}

/// <summary>
/// Generic service result for operations
/// </summary>
public class ServiceResult<T>
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public T? Data { get; private set; }

    public static ServiceResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ServiceResult<T> Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Service result for operations without data
/// </summary>
public class ServiceResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? Message { get; private set; }

    public static ServiceResult Success(string? message = null) => new()
    {
        IsSuccess = true,
        Message = message
    };

    public static ServiceResult Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Dashboard statistics
/// </summary>
public class DashboardStats
{
    public int ProjectCount { get; set; }
    public int TotalKeys { get; set; }
    public double AverageCompletion { get; set; }
    public int CharsUsed { get; set; }
    public int CharsLimit { get; set; } = 10000; // Default free tier
}
