using LrmCloud.Shared.DTOs.Resources;

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
    /// Gets a specific resource key with all translations.
    /// </summary>
    Task<ResourceKeyDetailDto?> GetResourceKeyAsync(int projectId, string keyName, int userId);

    /// <summary>
    /// Creates a new resource key.
    /// </summary>
    Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> CreateResourceKeyAsync(
        int projectId, int userId, CreateResourceKeyRequest request);

    /// <summary>
    /// Updates a resource key.
    /// </summary>
    Task<(bool Success, ResourceKeyDto? Key, string? ErrorMessage)> UpdateResourceKeyAsync(
        int projectId, string keyName, int userId, UpdateResourceKeyRequest request);

    /// <summary>
    /// Deletes a resource key and all its translations.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeleteResourceKeyAsync(
        int projectId, string keyName, int userId);

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
    /// </summary>
    Task<ValidationResultDto> ValidateProjectAsync(int projectId, int userId);

    // ============================================================
    // CLI Sync Operations
    // ============================================================

    /// <summary>
    /// Gets all resources for a project (for CLI pull).
    /// </summary>
    Task<List<ResourceDto>> GetResourcesAsync(int projectId, string? languageCode, int userId);

    /// <summary>
    /// Pushes resources from CLI to cloud.
    /// </summary>
    Task<(bool Success, PushResourcesResponse? Response, string? ErrorMessage)> PushResourcesAsync(
        int projectId, int userId, PushResourcesRequest request);
}
