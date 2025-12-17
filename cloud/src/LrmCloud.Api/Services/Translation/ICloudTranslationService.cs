using LrmCloud.Shared.DTOs.Translation;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Service for cloud-based translation operations.
/// Wraps LocalizationManager.Core translation providers with cloud-specific features:
/// - API key hierarchy (project → user → organization)
/// - Usage tracking and limits
/// - Caching
/// </summary>
public interface ICloudTranslationService
{
    /// <summary>
    /// Get list of available translation providers with their configuration status.
    /// </summary>
    Task<List<TranslationProviderDto>> GetAvailableProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null);

    /// <summary>
    /// Translate resource keys for a project.
    /// </summary>
    Task<TranslateResponseDto> TranslateKeysAsync(
        int projectId,
        int userId,
        TranslateRequestDto request);

    /// <summary>
    /// Translate a single text (for preview/testing).
    /// </summary>
    Task<TranslateSingleResponseDto> TranslateSingleAsync(
        int userId,
        TranslateSingleRequestDto request,
        int? projectId = null);

    /// <summary>
    /// Get translation usage statistics for a user.
    /// </summary>
    Task<TranslationUsageDto> GetUsageAsync(int userId);

    /// <summary>
    /// Get usage breakdown by provider.
    /// </summary>
    Task<List<ProviderUsageDto>> GetUsageByProviderAsync(int userId);
}
