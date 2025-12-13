using LrmCloud.Shared.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace LrmCloud.Api.Services;

/// <summary>
/// MinIO implementation of storage service for project files.
/// </summary>
public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly CloudConfiguration _config;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(
        IMinioClient minioClient,
        CloudConfiguration config,
        ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
        _config = config;
        _logger = logger;
    }

    public async Task UploadFileAsync(int projectId, string filePath, Stream content, string contentType = "application/octet-stream")
    {
        try
        {
            var objectName = GetObjectName(projectId, filePath);

            var putArgs = new PutObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putArgs);

            _logger.LogInformation("Uploaded file {ObjectName} to bucket {Bucket}",
                objectName, _config.Storage.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FilePath} for project {ProjectId}",
                filePath, projectId);
            throw;
        }
    }

    public async Task<Stream?> DownloadFileAsync(int projectId, string filePath)
    {
        try
        {
            var objectName = GetObjectName(projectId, filePath);

            // Check if file exists first
            if (!await FileExistsAsync(projectId, filePath))
                return null;

            var memoryStream = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getArgs);

            memoryStream.Position = 0; // Reset stream position for reading
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FilePath} for project {ProjectId}",
                filePath, projectId);
            throw;
        }
    }

    public async Task DeleteFileAsync(int projectId, string filePath)
    {
        try
        {
            var objectName = GetObjectName(projectId, filePath);

            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs);

            _logger.LogInformation("Deleted file {ObjectName} from bucket {Bucket}",
                objectName, _config.Storage.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FilePath} for project {ProjectId}",
                filePath, projectId);
            throw;
        }
    }

    public async Task<List<string>> ListFilesAsync(int projectId, string prefix = "")
    {
        try
        {
            var objectPrefix = GetObjectName(projectId, prefix);
            var files = new List<string>();

            var listArgs = new ListObjectsArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithPrefix(objectPrefix)
                .WithRecursive(true);

            var observable = _minioClient.ListObjectsEnumAsync(listArgs);

            await foreach (var item in observable)
            {
                // Remove project prefix from object name
                var relativePath = item.Key.Substring($"projects/{projectId}/".Length);
                files.Add(relativePath);
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files for project {ProjectId} with prefix {Prefix}",
                projectId, prefix);
            throw;
        }
    }

    public async Task DeleteProjectFilesAsync(int projectId)
    {
        try
        {
            var files = await ListFilesAsync(projectId);

            foreach (var file in files)
            {
                await DeleteFileAsync(projectId, file);
            }

            _logger.LogInformation("Deleted all files for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all files for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task CreateSnapshotAsync(int projectId, string syncId)
    {
        try
        {
            // List all files in current/
            var currentFiles = await ListFilesAsync(projectId, "current/");

            // Copy each file to snapshots/{syncId}/
            foreach (var file in currentFiles)
            {
                var sourceObjectName = GetObjectName(projectId, file);
                var destFileName = file.Replace("current/", $"snapshots/{syncId}/");
                var destObjectName = GetObjectName(projectId, destFileName);

                var copyArgs = new CopyObjectArgs()
                    .WithBucket(_config.Storage.Bucket)
                    .WithObject(destObjectName)
                    .WithCopyObjectSource(new CopySourceObjectArgs()
                        .WithBucket(_config.Storage.Bucket)
                        .WithObject(sourceObjectName));

                await _minioClient.CopyObjectAsync(copyArgs);
            }

            _logger.LogInformation("Created snapshot {SyncId} for project {ProjectId} with {FileCount} files",
                syncId, projectId, currentFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot {SyncId} for project {ProjectId}",
                syncId, projectId);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(int projectId, string filePath)
    {
        try
        {
            var objectName = GetObjectName(projectId, filePath);

            var statArgs = new StatObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statArgs);
            return true;
        }
        catch (Exception)
        {
            // StatObjectAsync throws exception if file doesn't exist
            return false;
        }
    }

    /// <summary>
    /// Constructs the full object name for MinIO storage.
    /// Format: projects/{projectId}/{filePath}
    /// </summary>
    private static string GetObjectName(int projectId, string filePath)
    {
        // Security: Prevent path traversal attacks
        if (filePath.Contains("..") || filePath.Contains("./"))
        {
            throw new ArgumentException("Invalid file path: path traversal not allowed", nameof(filePath));
        }

        // Normalize path separators
        filePath = filePath.Replace('\\', '/');

        // Remove leading slash if present
        if (filePath.StartsWith('/'))
        {
            filePath = filePath.Substring(1);
        }

        // Additional validation: ensure no path traversal after normalization
        if (filePath.Contains(".."))
        {
            throw new ArgumentException("Invalid file path: path traversal not allowed", nameof(filePath));
        }

        return $"projects/{projectId}/{filePath}";
    }

    public async Task<List<string>> ListSnapshotFilesAsync(int projectId, string snapshotId)
    {
        return await ListFilesAsync(projectId, $"snapshots/{snapshotId}/");
    }

    public async Task<Stream?> DownloadSnapshotFileAsync(int projectId, string snapshotId, string fileName)
    {
        return await DownloadFileAsync(projectId, $"snapshots/{snapshotId}/{fileName}");
    }

    public async Task RestoreFromSnapshotAsync(int projectId, string snapshotId)
    {
        try
        {
            // First, delete all current files
            var currentFiles = await ListFilesAsync(projectId, "current/");
            foreach (var file in currentFiles)
            {
                await DeleteFileAsync(projectId, file);
            }

            // Then copy all files from snapshot to current
            var snapshotFiles = await ListSnapshotFilesAsync(projectId, snapshotId);
            foreach (var file in snapshotFiles)
            {
                var sourceObjectName = GetObjectName(projectId, file);
                var destFileName = file.Replace($"snapshots/{snapshotId}/", "current/");
                var destObjectName = GetObjectName(projectId, destFileName);

                var copyArgs = new CopyObjectArgs()
                    .WithBucket(_config.Storage.Bucket)
                    .WithObject(destObjectName)
                    .WithCopyObjectSource(new CopySourceObjectArgs()
                        .WithBucket(_config.Storage.Bucket)
                        .WithObject(sourceObjectName));

                await _minioClient.CopyObjectAsync(copyArgs);
            }

            _logger.LogInformation("Restored snapshot {SnapshotId} for project {ProjectId} with {FileCount} files",
                snapshotId, projectId, snapshotFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring snapshot {SnapshotId} for project {ProjectId}",
                snapshotId, projectId);
            throw;
        }
    }

    public async Task DeleteSnapshotAsync(int projectId, string snapshotId)
    {
        try
        {
            var snapshotFiles = await ListSnapshotFilesAsync(projectId, snapshotId);
            foreach (var file in snapshotFiles)
            {
                await DeleteFileAsync(projectId, file);
            }

            _logger.LogInformation("Deleted snapshot {SnapshotId} for project {ProjectId} with {FileCount} files",
                snapshotId, projectId, snapshotFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snapshot {SnapshotId} for project {ProjectId}",
                snapshotId, projectId);
            throw;
        }
    }
}
