// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LrmCloud.Shared.DTOs.Ota;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for OTA (Over-The-Air) localization bundle generation.
/// </summary>
public interface IOtaService
{
    /// <summary>
    /// Gets the OTA bundle for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="projectPath">Project path for display (@username/project or org/project)</param>
    /// <param name="languages">Optional language filter</param>
    /// <param name="since">Optional timestamp for delta updates</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>OTA bundle or null if project not found</returns>
    Task<OtaBundleDto?> GetBundleAsync(
        int projectId,
        string projectPath,
        IEnumerable<string>? languages = null,
        DateTime? since = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the version timestamp for a project (for efficient polling).
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Version DTO or null if project not found</returns>
    Task<OtaVersionDto?> GetVersionAsync(int projectId, CancellationToken ct = default);

    /// <summary>
    /// Computes an ETag for the bundle based on version.
    /// </summary>
    string ComputeETag(string version);
}
