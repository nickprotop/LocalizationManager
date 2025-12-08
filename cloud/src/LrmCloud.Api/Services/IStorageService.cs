namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing file storage in MinIO (S3-compatible object storage).
/// Stores project file snapshots for sync history and exports.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="filePath">Path within project (e.g., "current/en.json")</param>
    /// <param name="content">File content</param>
    /// <param name="contentType">MIME type (default: application/octet-stream)</param>
    Task UploadFileAsync(int projectId, string filePath, Stream content, string contentType = "application/octet-stream");

    /// <summary>
    /// Downloads a file from storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="filePath">Path within project</param>
    /// <returns>File content stream, or null if not found</returns>
    Task<Stream?> DownloadFileAsync(int projectId, string filePath);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="filePath">Path within project</param>
    Task DeleteFileAsync(int projectId, string filePath);

    /// <summary>
    /// Lists all files in a project directory.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="prefix">Directory prefix (e.g., "current/" or "history/")</param>
    /// <returns>List of file paths</returns>
    Task<List<string>> ListFilesAsync(int projectId, string prefix = "");

    /// <summary>
    /// Deletes all files for a project (used when deleting project).
    /// </summary>
    /// <param name="projectId">Project ID</param>
    Task DeleteProjectFilesAsync(int projectId);

    /// <summary>
    /// Creates a snapshot of current files to history.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="syncId">Sync identifier (timestamp or commit hash)</param>
    Task CreateSnapshotAsync(int projectId, string syncId);

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="filePath">Path within project</param>
    Task<bool> FileExistsAsync(int projectId, string filePath);
}
