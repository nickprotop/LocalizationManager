namespace LrmCloud.Api.Services;

/// <summary>
/// Service for exporting project translations to file content.
/// Uses LocalizationManager.Core backends for serialization.
/// </summary>
public interface IFileExportService
{
    /// <summary>
    /// Export all translations for a project to file content.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="basePath">Base path for file generation (e.g., "src/locales")</param>
    /// <returns>Dictionary of relative file paths to content</returns>
    Task<Dictionary<string, string>> ExportProjectAsync(int projectId, string basePath);

    /// <summary>
    /// Export translations for specific languages only.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="basePath">Base path for file generation</param>
    /// <param name="languages">List of language codes to export</param>
    /// <returns>Dictionary of relative file paths to content</returns>
    Task<Dictionary<string, string>> ExportProjectAsync(int projectId, string basePath, IEnumerable<string> languages);
}
