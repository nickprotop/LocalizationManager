using System.Net.Http.Json;
using LrmCloud.Shared.DTOs.Glossary;

namespace LrmCloud.Web.Services;

/// <summary>
/// HTTP client service for glossary management.
/// Supports both project-level and organization-level glossaries.
/// </summary>
public class GlossaryService
{
    private readonly HttpClient _http;
    private readonly ILogger<GlossaryService> _logger;

    public GlossaryService(HttpClient http, ILogger<GlossaryService> logger)
    {
        _http = http;
        _logger = logger;
    }

    #region Project Glossary

    /// <summary>
    /// Get all glossary terms for a project (includes inherited organization terms).
    /// </summary>
    public async Task<GlossaryListResponse?> GetProjectGlossaryAsync(int projectId, bool includeInherited = true)
    {
        try
        {
            var url = $"projects/{projectId}/glossary?includeInherited={includeInherited}";
            return await _http.GetFromJsonAsync<GlossaryListResponse>(url);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get project glossary for project {ProjectId}", projectId);
            return null;
        }
    }

    /// <summary>
    /// Create a new project-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto?> CreateProjectTermAsync(int projectId, CreateGlossaryTermRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"projects/{projectId}/glossary", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GlossaryTermDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create project glossary term for project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Update a project-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto?> UpdateProjectTermAsync(int projectId, int termId, UpdateGlossaryTermRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"projects/{projectId}/glossary/{termId}", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GlossaryTermDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update project glossary term {TermId}", termId);
            throw;
        }
    }

    /// <summary>
    /// Delete a project-level glossary term.
    /// </summary>
    public async Task<bool> DeleteProjectTermAsync(int projectId, int termId)
    {
        try
        {
            var response = await _http.DeleteAsync($"projects/{projectId}/glossary/{termId}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete project glossary term {TermId}", termId);
            return false;
        }
    }

    #endregion

    #region Organization Glossary

    /// <summary>
    /// Get all glossary terms for an organization.
    /// </summary>
    public async Task<GlossaryListResponse?> GetOrganizationGlossaryAsync(int organizationId)
    {
        try
        {
            return await _http.GetFromJsonAsync<GlossaryListResponse>($"organizations/{organizationId}/glossary");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get organization glossary for org {OrganizationId}", organizationId);
            return null;
        }
    }

    /// <summary>
    /// Create a new organization-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto?> CreateOrganizationTermAsync(int organizationId, CreateGlossaryTermRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"organizations/{organizationId}/glossary", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GlossaryTermDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create organization glossary term for org {OrganizationId}", organizationId);
            throw;
        }
    }

    /// <summary>
    /// Update an organization-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto?> UpdateOrganizationTermAsync(int organizationId, int termId, UpdateGlossaryTermRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"organizations/{organizationId}/glossary/{termId}", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GlossaryTermDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update organization glossary term {TermId}", termId);
            throw;
        }
    }

    /// <summary>
    /// Delete an organization-level glossary term.
    /// </summary>
    public async Task<bool> DeleteOrganizationTermAsync(int organizationId, int termId)
    {
        try
        {
            var response = await _http.DeleteAsync($"organizations/{organizationId}/glossary/{termId}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete organization glossary term {TermId}", termId);
            return false;
        }
    }

    #endregion

    #region Common

    /// <summary>
    /// Get a single glossary term by ID.
    /// </summary>
    public async Task<GlossaryTermDto?> GetTermAsync(int termId)
    {
        try
        {
            return await _http.GetFromJsonAsync<GlossaryTermDto>($"glossary/{termId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get glossary term {TermId}", termId);
            return null;
        }
    }

    /// <summary>
    /// Find glossary terms that match the given source text.
    /// </summary>
    public async Task<GlossaryUsageSummary?> FindMatchingTermsAsync(
        int projectId,
        string sourceLanguage,
        string targetLanguage,
        string sourceText)
    {
        try
        {
            var url = $"projects/{projectId}/glossary/match?sourceLanguage={Uri.EscapeDataString(sourceLanguage)}&targetLanguage={Uri.EscapeDataString(targetLanguage)}";
            var response = await _http.PostAsJsonAsync(url, sourceText);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GlossaryUsageSummary>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to find matching glossary terms for project {ProjectId}", projectId);
            return null;
        }
    }

    #endregion
}
