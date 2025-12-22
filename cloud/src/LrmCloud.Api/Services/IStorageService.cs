namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing snapshot storage in MinIO (S3-compatible object storage).
/// Used exclusively for snapshot dbstate.json storage.
///
/// Note: With key-level sync, the database is the source of truth.
/// MinIO only stores snapshot state for backup/restore functionality.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a snapshot file to storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="snapshotId">Snapshot identifier</param>
    /// <param name="fileName">File name (e.g., "dbstate.json")</param>
    /// <param name="content">File content</param>
    Task UploadSnapshotFileAsync(int projectId, string snapshotId, string fileName, Stream content);

    /// <summary>
    /// Downloads a snapshot file from storage.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="snapshotId">Snapshot identifier</param>
    /// <param name="fileName">File name (e.g., "dbstate.json")</param>
    /// <returns>File content stream, or null if not found</returns>
    Task<Stream?> DownloadSnapshotFileAsync(int projectId, string snapshotId, string fileName);

    /// <summary>
    /// Checks if a snapshot file exists.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="snapshotId">Snapshot identifier</param>
    /// <param name="fileName">File name</param>
    Task<bool> SnapshotFileExistsAsync(int projectId, string snapshotId, string fileName);

    /// <summary>
    /// Deletes all files for a snapshot.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="snapshotId">Snapshot identifier</param>
    Task DeleteSnapshotAsync(int projectId, string snapshotId);

    /// <summary>
    /// Deletes all files for a project (used when deleting project).
    /// </summary>
    /// <param name="projectId">Project ID</param>
    Task DeleteProjectFilesAsync(int projectId);

    /// <summary>
    /// Gets the total storage size for multiple projects in bytes.
    /// Used for usage tracking/billing.
    /// </summary>
    /// <param name="projectIds">List of project IDs</param>
    /// <returns>Total size in bytes across all projects</returns>
    Task<long> GetTotalStorageSizeAsync(IEnumerable<int> projectIds);
}
