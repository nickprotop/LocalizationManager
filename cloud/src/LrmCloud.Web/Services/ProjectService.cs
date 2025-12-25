using System.Net.Http.Json;
using System.Text.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.DTOs.Sync;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for project CRUD operations
/// </summary>
public class ProjectService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectService(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
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

    /// <summary>
    /// Gets projects with pagination support
    /// </summary>
    public async Task<PagedResult<ProjectDto>> GetProjectsPagedAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        int? organizationId = null,
        string? sortBy = null,
        bool sortDesc = false)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        if (organizationId.HasValue)
            queryParams.Add($"organizationId={organizationId}");

        if (!string.IsNullOrWhiteSpace(sortBy))
            queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");

        if (sortDesc)
            queryParams.Add("sortDesc=true");

        var queryString = string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"projects/paged?{queryString}");

        if (!response.IsSuccessStatusCode)
            return new PagedResult<ProjectDto> { Page = page, PageSize = pageSize };

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProjectDto>>>();
        return result?.Data ?? new PagedResult<ProjectDto> { Page = page, PageSize = pageSize };
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
            var response = await _httpClient.PostAsJsonAsync("projects", request, _jsonOptions);

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
            var response = await _httpClient.PutAsJsonAsync($"projects/{id}", request, _jsonOptions);

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

    /// <summary>
    /// Imports resource files into a project
    /// </summary>
    public async Task<ServiceResult<ImportResult>> ImportFilesAsync(int projectId, List<FileDto> files)
    {
        try
        {
            var request = new PushRequest
            {
                ModifiedFiles = files,
                Message = "Initial import from web UI"
            };

            var response = await _httpClient.PostAsJsonAsync($"projects/{projectId}/import", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<PushResponse>>();
                if (result?.Data != null)
                {
                    // Fetch the updated project to get accurate key count
                    var project = await GetProjectAsync(projectId);

                    return ServiceResult<ImportResult>.Success(new ImportResult
                    {
                        KeyCount = project?.KeyCount ?? 0,
                        LanguageCount = files.Count, // Number of files imported
                        ModifiedCount = result.Data.ModifiedCount
                    });
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return ServiceResult<ImportResult>.Failure(error);
        }
        catch (Exception ex)
        {
            return ServiceResult<ImportResult>.Failure($"Failed to import files: {ex.Message}");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            // Check for "detail" (custom API error message)
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? "An error occurred";
            }

            // Check for "errors" (validation errors)
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                var errorMessages = new List<string>();
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var msg in field.Value.EnumerateArray())
                        {
                            errorMessages.Add(msg.GetString() ?? field.Name);
                        }
                    }
                }
                if (errorMessages.Count > 0)
                {
                    return string.Join(". ", errorMessages);
                }
            }

            // Check for "title" (general error title)
            if (doc.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString() ?? "An error occurred";
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

    // LRM Translation usage (managed service)
    public int LrmCharsUsed { get; set; }
    public int LrmCharsLimit { get; set; } = 10000; // Default free tier

    // Other providers usage (BYOK + free community)
    public long OtherCharsUsed { get; set; }

    // Legacy properties for backward compatibility
    public int CharsUsed => LrmCharsUsed;
    public int CharsLimit => LrmCharsLimit;
}

/// <summary>
/// Result of file import operation
/// </summary>
public class ImportResult
{
    public int KeyCount { get; set; }
    public int LanguageCount { get; set; }
    public int ModifiedCount { get; set; }
}
