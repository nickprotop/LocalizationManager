using LrmCloud.Shared.DTOs;
using LrmCloud.Shared.DTOs.Resources;
using LrmCloud.Shared.DTOs.Sync;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing resource keys and translations.
/// </summary>
public interface IResourceService
{
    // ============================================================
    // Resource Keys
    // ============================================================

    /// <summary>
    /// Gets all resource keys for a project.
    /// </summary>
    Task<List<ResourceKeyDto>> GetResourceKeysAsync(int projectId, int userId);

    /// <summary>
    /// Gets a specific resource key with all translations. <paramref name="baseName"/>
    /// disambiguates lookups in multi-group projects; empty string targets the
    /// default group (which is the only one in single-group projects).
    /// </summary>
    Task<ResourceKeyDetailDto?> GetResourceKeyAsync(int projectId, string keyName, int userId, string baseName = "");

    /// <summary>
    /// Gets resource keys with translations, paginated with search and sort support.
    /// </summary>
    Task<PagedResult<ResourceKeyDetailDto>> GetResourceKeysPagedAsync(
        int projectId,
        int userId,
        int page,
        int pageSize,
        string? search = null,
        string? sortBy = null,
        bool sortDescending = false,
        Dictionary<string, FilterParameter>? columnFilters = null);

    /// <summary>
    /// Creates a new resource key.
    /// </summary>
    Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> CreateResourceKeyAsync(
        int projectId, int userId, CreateResourceKeyRequest request);

    /// <summary>
    /// Updates a resource key. <paramref name="baseName"/> disambiguates the
    /// target row in multi-group projects.
    /// </summary>
    Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> UpdateResourceKeyAsync(
        int projectId, string keyName, int userId, UpdateResourceKeyRequest request, string baseName = "");

    /// <summary>
    /// Deletes a resource key and all its translations. <paramref name="baseName"/>
    /// disambiguates the target row in multi-group projects.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeleteResourceKeyAsync(
        int projectId, string keyName, int userId, string baseName = "");

    // ============================================================
    // Translations
    // ============================================================

    /// <summary>
    /// Updates a specific translation.
    /// </summary>
    Task<(bool Success, TranslationDto? Translation, string? ErrorMessage)> UpdateTranslationAsync(
        int projectId, string keyName, string languageCode, int userId, UpdateTranslationRequest request);

    /// <summary>
    /// Bulk update translations for a language.
    /// </summary>
    Task<(bool Success, int UpdatedCount, string? ErrorMessage)> BulkUpdateTranslationsAsync(
        int projectId, string languageCode, int userId, List<BulkTranslationUpdate> updates);

    // ============================================================
    // Stats & Validation
    // ============================================================

    /// <summary>
    /// Gets translation statistics for a project.
    /// </summary>
    Task<ProjectStatsDto> GetProjectStatsAsync(int projectId, int userId);

    /// <summary>
    /// Validates all resources in a project.
    /// Returns cached result if available and fresh, otherwise computes and caches.
    /// </summary>
    Task<ValidationResultDto> ValidateProjectAsync(int projectId, int userId);

    /// <summary>
    /// Forces a fresh validation, ignoring cache.
    /// </summary>
    Task<ValidationResultDto> ValidateProjectAsync(int projectId, int userId, bool forceRefresh);

    /// <summary>
    /// Invalidates the validation cache for a project.
    /// Call this after push/save operations.
    /// </summary>
    Task InvalidateValidationCacheAsync(int projectId);

    // ============================================================
    // Language Management
    // ============================================================

    /// <summary>
    /// Gets all languages in a project.
    /// </summary>
    Task<List<ProjectLanguageDto>> GetProjectLanguagesAsync(int projectId, int userId);

    /// <summary>
    /// Adds a new language to a project.
    /// </summary>
    Task<(bool Success, ProjectLanguageDto? Language, string? ErrorMessage)> AddLanguageAsync(
        int projectId, int userId, AddLanguageRequest request);

    /// <summary>
    /// Removes a language from a project (deletes all translations for that language).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> RemoveLanguageAsync(
        int projectId, int userId, RemoveLanguageRequest request);

    // ============================================================
    // REST API Operations (for web UI)
    // ============================================================

    /// <summary>
    /// Gets all resources for a project as structured DTOs.
    /// </summary>
    Task<List<ResourceDto>> GetResourcesAsync(int projectId, string? languageCode, int userId);

    // ============================================================
    // Batch Save with History
    // ============================================================

    /// <summary>
    /// Batch saves multiple changes with sync history recording.
    /// Used by the Blazor web editor to save changes with audit trail.
    /// </summary>
    Task<BatchSaveResponse> BatchSaveWithHistoryAsync(
        int projectId, int userId, BatchSaveRequest request);

}

/// <summary>
/// Represents a filter parameter for column filtering.
/// </summary>
public class FilterParameter
{
    /// <summary>
    /// Filter operator (contains, equals, startswith, endswith, etc.)
    /// </summary>
    public string Operator { get; set; } = "contains";

    /// <summary>
    /// Filter value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Second filter value for complex conditions (AND/OR)
    /// </summary>
    public string? SecondValue { get; set; }

    /// <summary>
    /// Second filter operator
    /// </summary>
    public string? SecondOperator { get; set; }

    /// <summary>
    /// Logical operator between filters (AND, OR)
    /// </summary>
    public string LogicalOperator { get; set; } = "AND";
}
