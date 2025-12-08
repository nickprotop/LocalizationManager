using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly IOrganizationService _organizationService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        AppDbContext db,
        IOrganizationService organizationService,
        CloudConfiguration config,
        ILogger<ProjectService> logger)
    {
        _db = db;
        _organizationService = organizationService;
        _config = config;
        _logger = logger;
    }

    // ============================================================
    // Project CRUD
    // ============================================================

    public async Task<(bool Success, ProjectDto? Project, string? ErrorMessage)> CreateProjectAsync(
        int userId, CreateProjectRequest request)
    {
        try
        {
            // Validate format
            if (!ProjectFormat.IsValid(request.Format))
                return (false, null, $"Invalid format. Must be one of: {string.Join(", ", ProjectFormat.All)}");

            // Determine ownership
            int? projectUserId = null;
            int? projectOrganizationId = null;

            if (request.OrganizationId.HasValue)
            {
                // Organization project - verify user has permission
                if (!await _organizationService.IsAdminOrOwnerAsync(request.OrganizationId.Value, userId))
                    return (false, null, "Only organization admins and owners can create projects");

                projectOrganizationId = request.OrganizationId;
            }
            else
            {
                // Personal project
                projectUserId = userId;
            }

            // Create project
            var project = new Project
            {
                Name = request.Name,
                Description = request.Description,
                UserId = projectUserId,
                OrganizationId = projectOrganizationId,
                Format = request.Format.ToLowerInvariant(),
                DefaultLanguage = request.DefaultLanguage,
                LocalizationPath = request.LocalizationPath,
                GitHubRepo = request.GitHubRepo,
                GitHubDefaultBranch = request.GitHubDefaultBranch ?? "main",
                SyncStatus = SyncStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} created by user {UserId}", project.Id, userId);

            // Convert to DTO
            var dto = MapToProjectDto(project, userId);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project for user {UserId}", userId);
            return (false, null, "An error occurred while creating the project");
        }
    }

    public async Task<ProjectDto?> GetProjectAsync(int projectId, int userId)
    {
        var project = await _db.Projects
            .Include(p => p.Organization)
            .Include(p => p.ResourceKeys)
                .ThenInclude(k => k.Translations)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return null;

        // Check access
        if (!await CanViewProjectAsync(projectId, userId))
            return null;

        return MapToProjectDto(project, userId);
    }

    public async Task<List<ProjectDto>> GetUserProjectsAsync(int userId)
    {
        // Get personal projects
        var personalProjects = await _db.Projects
            .Include(p => p.ResourceKeys)
                .ThenInclude(k => k.Translations)
            .Where(p => p.UserId == userId)
            .ToListAsync();

        // Get organization projects
        var organizationIds = await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync();

        var orgProjects = await _db.Projects
            .Include(p => p.Organization)
            .Include(p => p.ResourceKeys)
                .ThenInclude(k => k.Translations)
            .Where(p => p.OrganizationId != null && organizationIds.Contains(p.OrganizationId.Value))
            .ToListAsync();

        var allProjects = personalProjects.Concat(orgProjects).ToList();

        var dtos = new List<ProjectDto>();
        foreach (var project in allProjects)
        {
            dtos.Add(MapToProjectDto(project, userId));
        }

        return dtos.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<(bool Success, ProjectDto? Project, string? ErrorMessage)> UpdateProjectAsync(
        int projectId, int userId, UpdateProjectRequest request)
    {
        try
        {
            // Check permission
            if (!await CanEditProjectAsync(projectId, userId))
                return (false, null, "You don't have permission to edit this project");

            var project = await _db.Projects
                .Include(p => p.Organization)
                .Include(p => p.ResourceKeys)
                    .ThenInclude(k => k.Translations)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return (false, null, "Project not found");

            // Update fields
            if (!string.IsNullOrWhiteSpace(request.Name))
                project.Name = request.Name;

            if (request.Description != null)
                project.Description = request.Description;

            if (!string.IsNullOrWhiteSpace(request.Format))
            {
                if (!ProjectFormat.IsValid(request.Format))
                    return (false, null, $"Invalid format. Must be one of: {string.Join(", ", ProjectFormat.All)}");
                project.Format = request.Format.ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(request.DefaultLanguage))
                project.DefaultLanguage = request.DefaultLanguage;

            if (!string.IsNullOrWhiteSpace(request.LocalizationPath))
                project.LocalizationPath = request.LocalizationPath;

            if (request.GitHubRepo != null)
                project.GitHubRepo = request.GitHubRepo;

            if (!string.IsNullOrWhiteSpace(request.GitHubDefaultBranch))
                project.GitHubDefaultBranch = request.GitHubDefaultBranch;

            if (request.AutoTranslate.HasValue)
                project.AutoTranslate = request.AutoTranslate.Value;

            if (request.AutoCreatePr.HasValue)
                project.AutoCreatePr = request.AutoCreatePr.Value;

            project.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} updated by user {UserId}", projectId, userId);

            var dto = MapToProjectDto(project, userId);
            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", projectId);
            return (false, null, "An error occurred while updating the project");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteProjectAsync(int projectId, int userId)
    {
        try
        {
            // Check permission
            if (!await CanEditProjectAsync(projectId, userId))
                return (false, "You don't have permission to delete this project");

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return (false, "Project not found");

            // Hard delete (cascades to resource keys and translations)
            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} deleted by user {UserId}", projectId, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", projectId);
            return (false, "An error occurred while deleting the project");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TriggerSyncAsync(int projectId, int userId)
    {
        try
        {
            // Check permission
            if (!await CanEditProjectAsync(projectId, userId))
                return (false, "You don't have permission to sync this project");

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
                return (false, "Project not found");

            if (string.IsNullOrEmpty(project.GitHubRepo))
                return (false, "Project does not have a GitHub repository configured");

            // Update status to syncing
            project.SyncStatus = SyncStatus.Syncing;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Sync triggered for project {ProjectId} by user {UserId}", projectId, userId);

            // TODO: Phase 4 - Actual sync implementation will go here
            // For now, just mark as pending
            return (true, "Sync triggered successfully (implementation pending in Phase 4)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering sync for project {ProjectId}", projectId);
            return (false, "An error occurred while triggering sync");
        }
    }

    // ============================================================
    // Authorization Helpers
    // ============================================================

    public async Task<bool> CanViewProjectAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Personal project - only owner can view
        if (project.UserId.HasValue)
            return project.UserId.Value == userId;

        // Organization project - any member can view
        if (project.OrganizationId.HasValue)
            return await _organizationService.IsMemberAsync(project.OrganizationId.Value, userId);

        return false;
    }

    public async Task<bool> CanEditProjectAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Personal project - only owner can edit
        if (project.UserId.HasValue)
            return project.UserId.Value == userId;

        // Organization project - admin or owner can edit
        if (project.OrganizationId.HasValue)
            return await _organizationService.IsAdminOrOwnerAsync(project.OrganizationId.Value, userId);

        return false;
    }

    public async Task<bool> CanManageResourcesAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return false;

        // Personal project - only owner can manage
        if (project.UserId.HasValue)
            return project.UserId.Value == userId;

        // Organization project - member or above can manage
        if (project.OrganizationId.HasValue)
        {
            var role = await _organizationService.GetUserRoleAsync(project.OrganizationId.Value, userId);
            // Viewers cannot manage resources
            return role != null && role != OrganizationRole.Viewer;
        }

        return false;
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private ProjectDto MapToProjectDto(Project project, int userId)
    {
        // Calculate stats
        var keyCount = project.ResourceKeys.Count;
        var translationCount = project.ResourceKeys.Sum(k => k.Translations.Count);

        // Calculate completion percentage
        double completionPercentage = 0;
        if (keyCount > 0)
        {
            var translatedCount = project.ResourceKeys
                .SelectMany(k => k.Translations)
                .Count(t => t.Status != TranslationStatus.Pending && !string.IsNullOrWhiteSpace(t.Value));

            // Expected translations = keys * (unique languages)
            var uniqueLanguages = project.ResourceKeys
                .SelectMany(k => k.Translations)
                .Select(t => t.LanguageCode)
                .Distinct()
                .Count();

            if (uniqueLanguages > 0)
            {
                var expectedTranslations = keyCount * uniqueLanguages;
                completionPercentage = expectedTranslations > 0
                    ? Math.Round((double)translatedCount / expectedTranslations * 100, 2)
                    : 0;
            }
        }

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            UserId = project.UserId,
            OrganizationId = project.OrganizationId,
            OrganizationName = project.Organization?.Name,
            Format = project.Format,
            DefaultLanguage = project.DefaultLanguage,
            LocalizationPath = project.LocalizationPath,
            GitHubRepo = project.GitHubRepo,
            GitHubDefaultBranch = project.GitHubDefaultBranch,
            AutoTranslate = project.AutoTranslate,
            AutoCreatePr = project.AutoCreatePr,
            SyncStatus = project.SyncStatus,
            SyncError = project.SyncError,
            LastSyncedAt = project.LastSyncedAt,
            KeyCount = keyCount,
            TranslationCount = translationCount,
            CompletionPercentage = completionPercentage,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
