using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.DTOs.Resources;
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
            // Determine ownership
            int? projectUserId = null;
            int? projectOrganizationId = null;

            if (request.OrganizationId.HasValue)
            {
                // Organization project - verify user has permission
                if (!await _organizationService.IsAdminOrOwnerAsync(request.OrganizationId.Value, userId))
                {
                    return (false, null, "Only organization admins and owners can create projects");
                }

                projectOrganizationId = request.OrganizationId;
            }
            else
            {
                // Personal project - check project limit based on user's plan
                var user = await _db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Plan })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return (false, null, "User not found");
                }

                var currentProjectCount = await _db.Projects.CountAsync(p => p.UserId == userId);
                var maxProjects = _config.Limits.GetMaxProjects(user.Plan);

                if (currentProjectCount >= maxProjects)
                {
                    _logger.LogWarning("User {UserId} has reached project limit ({CurrentCount}/{MaxProjects}) for plan {Plan}",
                        userId, currentProjectCount, maxProjects, user.Plan);
                    return (false, null, $"Project limit reached ({currentProjectCount}/{maxProjects}). Upgrade your plan to create more projects.");
                }

                projectUserId = userId;
            }

            // Validate slug uniqueness within the owner's scope
            var slugLower = request.Slug.ToLowerInvariant();
            bool slugExists;
            if (projectOrganizationId.HasValue)
            {
                slugExists = await _db.Projects.AnyAsync(p =>
                    p.OrganizationId == projectOrganizationId && p.Slug == slugLower);
            }
            else
            {
                slugExists = await _db.Projects.AnyAsync(p =>
                    p.UserId == projectUserId && p.Slug == slugLower);
            }

            if (slugExists)
            {
                return (false, null, "A project with this slug already exists");
            }

            // Create project (format is client concern, not stored on server)
            var project = new Project
            {
                Slug = slugLower,
                Name = request.Name,
                Description = request.Description,
                UserId = projectUserId,
                OrganizationId = projectOrganizationId,
                DefaultLanguage = request.DefaultLanguage,
                GitHubRepo = request.GitHubRepo,
                GitHubDefaultBranch = request.GitHubDefaultBranch ?? "main",
                GitHubBasePath = request.GitHubBasePath,
                GitHubFormat = request.GitHubFormat,
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
        {
            return null;
        }

        // Check access
        if (!await CanViewProjectAsync(projectId, userId))
        {
            return null;
        }

        return MapToProjectDto(project, userId);
    }

    public async Task<ProjectDto?> GetProjectByNameAsync(string username, string projectSlug, int userId)
    {
        try
        {
            // Look up user by username
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                return null;
            }

            // Look up project by slug for that user (personal projects only)
            var slugLower = projectSlug.ToLowerInvariant();
            var project = await _db.Projects
                .Include(p => p.Organization)
                .Include(p => p.ResourceKeys)
                    .ThenInclude(k => k.Translations)
                .FirstOrDefaultAsync(p => p.Slug == slugLower && p.UserId == user.Id);

            if (project == null)
            {
                return null;
            }

            // Verify access
            if (!await CanViewProjectAsync(project.Id, userId))
            {
                return null;
            }

            return MapToProjectDto(project, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project by slug {Username}/{ProjectSlug}", username, projectSlug);
            return null;
        }
    }

    public async Task<ProjectDto?> GetProjectByOrgSlugAsync(string orgSlug, string projectSlug, int userId)
    {
        try
        {
            // Look up organization by slug
            var slugLower = orgSlug.ToLowerInvariant();
            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Slug == slugLower);
            if (organization == null)
            {
                return null;
            }

            // Look up project by slug for that organization
            var projectSlugLower = projectSlug.ToLowerInvariant();
            var project = await _db.Projects
                .Include(p => p.Organization)
                .Include(p => p.ResourceKeys)
                    .ThenInclude(k => k.Translations)
                .FirstOrDefaultAsync(p => p.Slug == projectSlugLower && p.OrganizationId == organization.Id);

            if (project == null)
            {
                return null;
            }

            // Verify access
            if (!await CanViewProjectAsync(project.Id, userId))
            {
                return null;
            }

            return MapToProjectDto(project, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project by org slug {OrgSlug}/{ProjectSlug}", orgSlug, projectSlug);
            return null;
        }
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

    public async Task<PagedResult<ProjectDto>> GetUserProjectsPagedAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        int? organizationId = null,
        string? sortBy = null,
        bool sortDescending = false)
    {
        // Get organization IDs the user belongs to
        var memberOrgIds = await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync();

        // Base query: personal projects + org projects
        var query = _db.Projects
            .Include(p => p.Organization)
            .Include(p => p.ResourceKeys)
                .ThenInclude(k => k.Translations)
            .Where(p =>
                p.UserId == userId ||
                (p.OrganizationId != null && memberOrgIds.Contains(p.OrganizationId.Value)));

        // Filter by organization if specified
        if (organizationId.HasValue)
        {
            if (organizationId.Value == 0)
            {
                // Personal projects only
                query = query.Where(p => p.OrganizationId == null);
            }
            else
            {
                // Specific organization
                query = query.Where(p => p.OrganizationId == organizationId.Value);
            }
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "name" => sortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            "updatedat" => sortDescending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),
            _ => sortDescending
                ? query.OrderBy(p => p.CreatedAt)
                : query.OrderByDescending(p => p.CreatedAt)
        };

        // Apply pagination
        var projects = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Map to DTOs
        var items = projects.Select(p => MapToProjectDto(p, userId)).ToList();

        return new PagedResult<ProjectDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, ProjectDto? Project, string? ErrorMessage)> UpdateProjectAsync(
        int projectId, int userId, UpdateProjectRequest request)
    {
        try
        {
            // Check permission
            if (!await CanEditProjectAsync(projectId, userId))
            {
                return (false, null, "You don't have permission to edit this project");
            }

            var project = await _db.Projects
                .Include(p => p.Organization)
                .Include(p => p.ResourceKeys)
                    .ThenInclude(k => k.Translations)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return (false, null, "Project not found");
            }

            // Update fields (Slug and Format are immutable after creation)
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                project.Name = request.Name;
            }

            if (request.Description != null)
            {
                project.Description = request.Description;
            }

            // DefaultLanguage is immutable after creation - changes would corrupt translation data

            if (request.GitHubRepo != null)
            {
                project.GitHubRepo = request.GitHubRepo;
            }

            if (!string.IsNullOrWhiteSpace(request.GitHubDefaultBranch))
            {
                project.GitHubDefaultBranch = request.GitHubDefaultBranch;
            }

            if (request.AutoTranslate.HasValue)
            {
                project.AutoTranslate = request.AutoTranslate.Value;
            }

            if (request.AutoCreatePr.HasValue)
            {
                project.AutoCreatePr = request.AutoCreatePr.Value;
            }

            if (request.InheritOrganizationGlossary.HasValue)
            {
                project.InheritOrganizationGlossary = request.InheritOrganizationGlossary.Value;
            }

            // Handle organization assignment/transfer
            if (request.UpdateOrganization)
            {
                if (request.OrganizationId.HasValue)
                {
                    // Verify user has admin/owner access to the target organization
                    if (!await _organizationService.IsAdminOrOwnerAsync(request.OrganizationId.Value, userId))
                    {
                        return (false, null, "You don't have permission to assign projects to this organization");
                    }

                    // Check if slug would conflict in the target organization
                    var slugExists = await _db.Projects.AnyAsync(p =>
                        p.OrganizationId == request.OrganizationId.Value &&
                        p.Slug == project.Slug &&
                        p.Id != projectId);

                    if (slugExists)
                    {
                        return (false, null, "A project with this slug already exists in the target organization");
                    }

                    project.OrganizationId = request.OrganizationId.Value;
                    project.UserId = null; // Remove personal ownership

                    _logger.LogInformation("Project {ProjectId} transferred to organization {OrgId} by user {UserId}",
                        projectId, request.OrganizationId.Value, userId);
                }
                else
                {
                    // Converting to personal project - verify user owns the project or is org admin
                    // Only the original creator can convert back to personal, or we make it personal for the requester
                    var slugExists = await _db.Projects.AnyAsync(p =>
                        p.UserId == userId &&
                        p.Slug == project.Slug &&
                        p.Id != projectId);

                    if (slugExists)
                    {
                        return (false, null, "You already have a personal project with this slug");
                    }

                    project.OrganizationId = null;
                    project.UserId = userId;

                    _logger.LogInformation("Project {ProjectId} converted to personal project for user {UserId}", projectId, userId);
                }
            }

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
            {
                return (false, "You don't have permission to delete this project");
            }

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return (false, "Project not found");
            }

            // Delete related audit logs first (not cascaded by FK)
            var auditLogs = await _db.AuditLogs
                .Where(a => a.ProjectId == projectId)
                .ToListAsync();
            _db.AuditLogs.RemoveRange(auditLogs);

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
            {
                return (false, "You don't have permission to sync this project");
            }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null)
            {
                return (false, "Project not found");
            }

            if (string.IsNullOrEmpty(project.GitHubRepo))
            {
                return (false, "Project does not have a GitHub repository configured");
            }

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
        {
            return false;
        }

        // Personal project - only owner can view
        if (project.UserId.HasValue)
        {
            return project.UserId.Value == userId;
        }

        // Organization project - any member can view
        if (project.OrganizationId.HasValue)
        {
            return await _organizationService.IsMemberAsync(project.OrganizationId.Value, userId);
        }

        return false;
    }

    public async Task<bool> CanEditProjectAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return false;
        }

        // Personal project - only owner can edit
        if (project.UserId.HasValue)
        {
            return project.UserId.Value == userId;
        }

        // Organization project - admin or owner can edit
        if (project.OrganizationId.HasValue)
        {
            return await _organizationService.IsAdminOrOwnerAsync(project.OrganizationId.Value, userId);
        }

        return false;
    }

    public async Task<bool> CanManageResourcesAsync(int projectId, int userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return false;
        }

        // Personal project - only owner can manage
        if (project.UserId.HasValue)
        {
            return project.UserId.Value == userId;
        }

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

        // Calculate completion percentage across ALL languages
        // Expected = keys × languages (accounting for plural forms)
        // Actual = translations with non-empty values
        double completionPercentage = 0;

        // Get all unique languages for this project
        var languages = project.ResourceKeys
            .SelectMany(k => k.Translations)
            .Select(t => t.LanguageCode)
            .Distinct()
            .ToList();

        var languageCount = languages.Count;

        if (languageCount > 0 && keyCount > 0)
        {
            int expectedTranslations = 0;
            int actualTranslations = 0;

            foreach (var key in project.ResourceKeys)
            {
                if (key.IsPlural)
                {
                    // For plural keys, count the plural forms used
                    var pluralForms = key.Translations
                        .Select(t => t.PluralForm)
                        .Where(f => !string.IsNullOrEmpty(f))
                        .Distinct()
                        .Count();
                    // At minimum expect 2 forms (one, other)
                    pluralForms = Math.Max(pluralForms, 2);

                    expectedTranslations += languageCount * pluralForms;
                }
                else
                {
                    expectedTranslations += languageCount;
                }

                actualTranslations += key.Translations.Count(t => !string.IsNullOrWhiteSpace(t.Value));
            }

            completionPercentage = expectedTranslations > 0
                ? Math.Round((double)actualTranslations / expectedTranslations * 100, 2)
                : 0;
        }

        // Parse validation cache if available
        int validationErrors = 0, validationWarnings = 0;
        if (!string.IsNullOrEmpty(project.ValidationCacheJson))
        {
            try
            {
                var validationCache = JsonSerializer.Deserialize<ValidationResultDto>(project.ValidationCacheJson);
                if (validationCache?.Summary != null)
                {
                    validationErrors = validationCache.Summary.Errors;
                    validationWarnings = validationCache.Summary.Warnings;
                }
            }
            catch
            {
                // Ignore parse errors - cache may be corrupted
            }
        }

        return new ProjectDto
        {
            Id = project.Id,
            Slug = project.Slug,
            Name = project.Name,
            Description = project.Description,
            UserId = project.UserId,
            OrganizationId = project.OrganizationId,
            OrganizationName = project.Organization?.Name,
            DefaultLanguage = project.DefaultLanguage,
            GitHubRepo = project.GitHubRepo,
            GitHubDefaultBranch = project.GitHubDefaultBranch,
            GitHubBasePath = project.GitHubBasePath,
            GitHubFormat = project.GitHubFormat,
            AutoTranslate = project.AutoTranslate,
            AutoCreatePr = project.AutoCreatePr,
            InheritOrganizationGlossary = project.InheritOrganizationGlossary,
            SyncStatus = project.SyncStatus,
            SyncError = project.SyncError,
            LastSyncedAt = project.LastSyncedAt,
            KeyCount = keyCount,
            TranslationCount = translationCount,
            CompletionPercentage = completionPercentage,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            ValidationErrors = validationErrors,
            ValidationWarnings = validationWarnings,
            IsSampleProject = project.IsSampleProject
        };
    }

    public async Task<SyncStatusDto?> GetSyncStatusAsync(int projectId, int userId)
    {
        if (!await CanViewProjectAsync(projectId, userId))
        {
            return null;
        }

        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return null;
        }

        // Get last push (sync from CLI)
        var lastPush = await _db.SyncHistory
            .Where(s => s.ProjectId == projectId && s.OperationType == "push")
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        // Get last activity (any sync operation)
        var lastPull = await _db.SyncHistory
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        // Calculate changes (simplified - would need more complex logic in production)
        var localChanges = 0; // Would compare with last sync snapshot
        var remoteChanges = 0; // Would compare with last sync snapshot

        return new SyncStatusDto
        {
            IsSynced = localChanges == 0 && remoteChanges == 0,
            LastPush = lastPush == default ? null : lastPush,
            LastPull = lastPull == default ? null : lastPull,
            LocalChanges = localChanges,
            RemoteChanges = remoteChanges
        };
    }
}
