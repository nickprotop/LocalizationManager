using LrmCloud.Shared.DTOs.Translation;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Service for resolving API keys and configurations from the hierarchy:
/// Project → User → Organization → Platform
/// </summary>
public interface IApiKeyHierarchyService
{
    /// <summary>
    /// Resolve the API key for a provider using the hierarchy.
    /// </summary>
    /// <param name="provider">Provider name (e.g., "google", "deepl").</param>
    /// <param name="projectId">Project ID (optional, highest priority).</param>
    /// <param name="userId">User ID (second priority).</param>
    /// <param name="organizationId">Organization ID (third priority).</param>
    /// <returns>Tuple of (decrypted API key, source level) or (null, null) if not found.</returns>
    Task<(string? ApiKey, string? Source)> ResolveApiKeyAsync(
        string provider,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null);

    /// <summary>
    /// Resolve both API key and configuration for a provider, merging from all hierarchy levels.
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="projectId">Project ID (optional, highest priority).</param>
    /// <param name="userId">User ID (second priority).</param>
    /// <param name="organizationId">Organization ID (third priority).</param>
    /// <returns>Resolved configuration with merged settings and source information.</returns>
    Task<ResolvedProviderConfigDto> ResolveProviderConfigAsync(
        string provider,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null);

    /// <summary>
    /// Get all configured providers for a context with their sources.
    /// </summary>
    Task<Dictionary<string, string>> GetConfiguredProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null);

    /// <summary>
    /// Get provider configuration at a specific level.
    /// </summary>
    Task<ProviderConfigDto?> GetProviderConfigAsync(
        string provider,
        string level,
        int entityId);

    /// <summary>
    /// Set an API key at a specific level.
    /// </summary>
    Task SetApiKeyAsync(
        string provider,
        string plainApiKey,
        string level,
        int entityId);

    /// <summary>
    /// Set provider configuration at a specific level (API key and/or config).
    /// </summary>
    /// <param name="provider">Provider name.</param>
    /// <param name="level">Level: "user", "organization", or "project".</param>
    /// <param name="entityId">User ID, Organization ID, or Project ID.</param>
    /// <param name="apiKey">API key (optional - null to keep existing or not set).</param>
    /// <param name="config">Configuration dictionary (optional - null to keep existing).</param>
    Task SetProviderConfigAsync(
        string provider,
        string level,
        int entityId,
        string? apiKey = null,
        Dictionary<string, object?>? config = null);

    /// <summary>
    /// Remove an API key at a specific level.
    /// </summary>
    Task<bool> RemoveApiKeyAsync(
        string provider,
        string level,
        int entityId);

    /// <summary>
    /// Remove provider configuration (API key and config) at a specific level.
    /// </summary>
    Task<bool> RemoveProviderConfigAsync(
        string provider,
        string level,
        int entityId);

    /// <summary>
    /// Get summary of all providers for a specific level.
    /// </summary>
    Task<List<ProviderConfigSummaryDto>> GetProviderSummariesAsync(
        string level,
        int entityId,
        int? projectId = null,
        int? userId = null,
        int? organizationId = null);
}
