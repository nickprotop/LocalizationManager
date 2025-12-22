using LrmCloud.Shared.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace LrmCloud.Api.Services;

/// <summary>
/// MinIO implementation of storage service for snapshot files.
/// Only stores dbstate.json for each snapshot - resource files are
/// regenerated from database on demand.
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

    public async Task UploadSnapshotFileAsync(int projectId, string snapshotId, string fileName, Stream content)
    {
        try
        {
            var objectName = GetSnapshotObjectName(projectId, snapshotId, fileName);

            var putArgs = new PutObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType("application/json");

            await _minioClient.PutObjectAsync(putArgs);

            _logger.LogDebug("Uploaded snapshot file {ObjectName}", objectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading snapshot file {FileName} for project {ProjectId} snapshot {SnapshotId}",
                fileName, projectId, snapshotId);
            throw;
        }
    }

    public async Task<Stream?> DownloadSnapshotFileAsync(int projectId, string snapshotId, string fileName)
    {
        try
        {
            var objectName = GetSnapshotObjectName(projectId, snapshotId, fileName);

            // Check if file exists first
            if (!await SnapshotFileExistsAsync(projectId, snapshotId, fileName))
                return null;

            var memoryStream = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getArgs);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading snapshot file {FileName} for project {ProjectId} snapshot {SnapshotId}",
                fileName, projectId, snapshotId);
            throw;
        }
    }

    public async Task<bool> SnapshotFileExistsAsync(int projectId, string snapshotId, string fileName)
    {
        try
        {
            var objectName = GetSnapshotObjectName(projectId, snapshotId, fileName);

            var statArgs = new StatObjectArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteSnapshotAsync(int projectId, string snapshotId)
    {
        try
        {
            var prefix = $"projects/{projectId}/snapshots/{snapshotId}/";
            var files = await ListFilesWithPrefixAsync(prefix);

            foreach (var file in files)
            {
                var removeArgs = new RemoveObjectArgs()
                    .WithBucket(_config.Storage.Bucket)
                    .WithObject(file);

                await _minioClient.RemoveObjectAsync(removeArgs);
            }

            _logger.LogInformation("Deleted snapshot {SnapshotId} for project {ProjectId} ({FileCount} files)",
                snapshotId, projectId, files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snapshot {SnapshotId} for project {ProjectId}",
                snapshotId, projectId);
            throw;
        }
    }

    public async Task DeleteProjectFilesAsync(int projectId)
    {
        try
        {
            var prefix = $"projects/{projectId}/";
            var files = await ListFilesWithPrefixAsync(prefix);

            foreach (var file in files)
            {
                var removeArgs = new RemoveObjectArgs()
                    .WithBucket(_config.Storage.Bucket)
                    .WithObject(file);

                await _minioClient.RemoveObjectAsync(removeArgs);
            }

            _logger.LogInformation("Deleted all files for project {ProjectId} ({FileCount} files)",
                projectId, files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all files for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<long> GetTotalStorageSizeAsync(IEnumerable<int> projectIds)
    {
        long totalSize = 0;

        foreach (var projectId in projectIds)
        {
            try
            {
                var prefix = $"projects/{projectId}/";
                var listArgs = new ListObjectsArgs()
                    .WithBucket(_config.Storage.Bucket)
                    .WithPrefix(prefix)
                    .WithRecursive(true);

                var observable = _minioClient.ListObjectsEnumAsync(listArgs);

                await foreach (var item in observable)
                {
                    totalSize += (long)item.Size;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting storage size for project {ProjectId}", projectId);
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Constructs the object name for a snapshot file.
    /// Format: projects/{projectId}/snapshots/{snapshotId}/{fileName}
    /// </summary>
    private static string GetSnapshotObjectName(int projectId, string snapshotId, string fileName)
    {
        ValidatePathComponent(snapshotId, nameof(snapshotId));
        ValidatePathComponent(fileName, nameof(fileName));

        return $"projects/{projectId}/snapshots/{snapshotId}/{fileName}";
    }

    /// <summary>
    /// Validates a path component to prevent path traversal attacks.
    /// </summary>
    private static void ValidatePathComponent(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty", paramName);

        if (value.Contains("..") || value.Contains('/') || value.Contains('\\'))
            throw new ArgumentException("Invalid path component", paramName);

        if (value.Contains('\0') || value.Contains('%'))
            throw new ArgumentException("Invalid characters in path", paramName);
    }

    /// <summary>
    /// Lists all files with a given prefix.
    /// </summary>
    private async Task<List<string>> ListFilesWithPrefixAsync(string prefix)
    {
        var files = new List<string>();

        var listArgs = new ListObjectsArgs()
            .WithBucket(_config.Storage.Bucket)
            .WithPrefix(prefix)
            .WithRecursive(true);

        var observable = _minioClient.ListObjectsEnumAsync(listArgs);

        await foreach (var item in observable)
        {
            files.Add(item.Key);
        }

        return files;
    }
}
