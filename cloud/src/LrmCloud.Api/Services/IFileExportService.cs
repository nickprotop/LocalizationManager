namespace LrmCloud.Api.Services;

/// <summary>
/// Service for exporting project translations to file content.
/// Uses LocalizationManager.Core backends for serialization.
/// Format is now a parameter, not stored in Project entity (client-agnostic API).
/// </summary>
public interface IFileExportService
{
    /// <summary>
    /// Export all translations for a project to file content.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="basePath">Base path for file generation (e.g., "src/locales")</param>
    /// <param name="format">Resource format (resx, json, i18next, android, ios, po, xliff)</param>
    /// <returns>Dictionary of relative file paths to content</returns>
    Task<Dictionary<string, string>> ExportProjectAsync(int projectId, string basePath, string format);

    /// <summary>
    /// Export translations for specific languages only.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="basePath">Base path for file generation</param>
    /// <param name="format">Resource format (resx, json, i18next, android, ios, po, xliff)</param>
    /// <param name="languages">List of language codes to export</param>
    /// <returns>Dictionary of relative file paths to content</returns>
    Task<Dictionary<string, string>> ExportProjectAsync(int projectId, string basePath, string format, IEnumerable<string>? languages);
}
