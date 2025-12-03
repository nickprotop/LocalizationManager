// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backup;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;
    private readonly BackupVersionManager _backupManager;

    public BackupController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
        _backupManager = new BackupVersionManager();
    }

    /// <summary>
    /// Validates a file name to prevent path traversal attacks
    /// </summary>
    private bool IsValidFileName(string? fileName, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "File name is required";
            return false;
        }

        // Block path traversal attempts
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(':'))
        {
            error = "Invalid file name";
            return false;
        }

        // Must end with a supported extension
        var supportedExtensions = _backend.SupportedExtensions;
        if (!supportedExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"File must have one of these extensions: {string.Join(", ", supportedExtensions)}";
            return false;
        }

        // Additional check: file name must only contain safe characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
        {
            error = "File name contains invalid characters";
            return false;
        }

        return true;
    }

    /// <summary>
    /// List all backups
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<BackupListResponse>> ListBackups([FromQuery] string? fileName)
    {
        try
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                if (!IsValidFileName(fileName, out var error))
                {
                    return BadRequest(new ErrorResponse { Error = error! });
                }
                var backups = await _backupManager.ListBackupsAsync(fileName, _resourcePath);
                return Ok(new BackupListResponse
                {
                    FileName = fileName,
                    BackupCount = backups.Count,
                    Backups = backups.Select(b => new BackupInfo
                    {
                        FileName = fileName,
                        Version = b.Version,
                        Timestamp = b.Timestamp,
                        Operation = b.Operation,
                        KeyCount = b.KeyCount,
                        ChangedKeys = b.ChangedKeys,
                        ChangedKeyNames = b.ChangedKeyNames
                    }).ToList()
                });
            }
            else
            {
                var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
                var allBackupsTasks = languages.Select(async lang =>
                {
                    var backups = await _backupManager.ListBackupsAsync(lang.Name, _resourcePath);
                    return backups.Select(b => new BackupInfo
                    {
                        FileName = lang.Name,
                        Version = b.Version,
                        Timestamp = b.Timestamp,
                        Operation = b.Operation,
                        KeyCount = b.KeyCount,
                        ChangedKeys = b.ChangedKeys,
                        ChangedKeyNames = b.ChangedKeyNames
                    });
                });

                var allBackups = (await Task.WhenAll(allBackupsTasks))
                    .SelectMany(x => x)
                    .OrderByDescending(b => b.Timestamp)
                    .ToList();

                return Ok(new BackupListResponse { Backups = allBackups });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a backup
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateBackupResponse>> CreateBackup([FromBody] CreateBackupRequest request)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.FileName))
            {
                if (!IsValidFileName(request.FileName, out var error))
                {
                    return BadRequest(new ErrorResponse { Error = error! });
                }

                var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
                var language = languages.FirstOrDefault(l => l.Name == request.FileName);
                if (language == null)
                {
                    return NotFound(new ErrorResponse { Error = $"File '{request.FileName}' not found" });
                }

                var resourceFile = _backend.Reader.Read(language);
                var metadata = await _backupManager.CreateBackupAsync(
                    language.FilePath,
                    request.Operation ?? "manual",
                    _resourcePath);

                return Ok(new CreateBackupResponse
                {
                    Success = true,
                    FileName = request.FileName,
                    Version = metadata.Version,
                    Operation = request.Operation ?? "manual"
                });
            }
            else
            {
                // Backup all files
                var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
                var results = new List<BackupCreatedInfo>();

                foreach (var lang in languages)
                {
                    var resourceFile = _backend.Reader.Read(lang);
                    var metadata = await _backupManager.CreateBackupAsync(
                        lang.FilePath,
                        request.Operation ?? "manual",
                        _resourcePath);
                    results.Add(new BackupCreatedInfo
                    {
                        FileName = lang.Name,
                        Version = metadata.Version
                    });
                }

                return Ok(new CreateBackupResponse
                {
                    Success = true,
                    Operation = request.Operation ?? "manual",
                    Backups = results
                });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get backup info
    /// </summary>
    [HttpGet("{fileName}/{version}")]
    public async Task<ActionResult<BackupInfo>> GetBackupInfo(string fileName, int version)
    {
        try
        {
            if (!IsValidFileName(fileName, out var error))
            {
                return BadRequest(new ErrorResponse { Error = error! });
            }

            if (version < 1)
            {
                return BadRequest(new ErrorResponse { Error = "Version must be a positive integer" });
            }

            var backup = await _backupManager.GetBackupAsync(fileName, version, _resourcePath);

            if (backup == null)
            {
                return NotFound(new ErrorResponse { Error = $"Backup version {version} not found for '{fileName}'" });
            }

            return Ok(new BackupInfo
            {
                FileName = fileName,
                Version = backup.Version,
                Timestamp = backup.Timestamp,
                Operation = backup.Operation,
                KeyCount = backup.KeyCount,
                ChangedKeys = backup.ChangedKeys,
                ChangedKeyNames = backup.ChangedKeyNames
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Restore from backup
    /// </summary>
    [HttpPost("{fileName}/{version}/restore")]
    public async Task<ActionResult<RestoreBackupResponse>> RestoreBackup(string fileName, int version, [FromBody] RestoreBackupRequest? request)
    {
        try
        {
            if (!IsValidFileName(fileName, out var error))
            {
                return BadRequest(new ErrorResponse { Error = error! });
            }

            if (version < 1)
            {
                return BadRequest(new ErrorResponse { Error = "Version must be a positive integer" });
            }

            var restoreService = new BackupRestoreService(_backupManager);
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var language = languages.FirstOrDefault(l => l.Name == fileName);

            if (language == null)
            {
                return NotFound(new ErrorResponse { Error = $"File '{fileName}' not found" });
            }

            var currentFile = _backend.Reader.Read(language);

            if (request?.Preview == true)
            {
                var diff = await restoreService.PreviewRestoreAsync(
                    fileName,
                    version,
                    language.FilePath,
                    _resourcePath);

                var added = diff.Changes.Where(c => c.Type == LocalizationManager.Shared.Enums.ChangeType.Added).ToList();
                var modified = diff.Changes.Where(c => c.Type == LocalizationManager.Shared.Enums.ChangeType.Modified).ToList();
                var removed = diff.Changes.Where(c => c.Type == LocalizationManager.Shared.Enums.ChangeType.Deleted).ToList();

                return Ok(new RestoreBackupResponse
                {
                    Preview = true,
                    AddedCount = added.Count,
                    ModifiedCount = modified.Count,
                    RemovedCount = removed.Count,
                    Changes = new RestoreChanges
                    {
                        Added = added.Select(c => c.Key).ToList(),
                        Modified = modified.Select(m => new ModifiedKeyInfo
                        {
                            Key = m.Key,
                            CurrentValue = m.OldValue,
                            BackupValue = m.NewValue
                        }).ToList(),
                        Removed = removed.Select(c => c.Key).ToList()
                    }
                });
            }

            // Perform restore
            if (request?.Keys?.Any() == true)
            {
                var restoredCount = await restoreService.RestoreKeysAsync(
                    fileName,
                    version,
                    request.Keys,
                    language.FilePath,
                    _resourcePath);

                return Ok(new RestoreBackupResponse
                {
                    Success = restoredCount > 0,
                    FileName = fileName,
                    Version = version,
                    RestoredKeys = restoredCount
                });
            }
            else
            {
                var success = await restoreService.RestoreAsync(
                    fileName,
                    version,
                    language.FilePath,
                    _resourcePath);

                return Ok(new RestoreBackupResponse
                {
                    Success = success,
                    FileName = fileName,
                    Version = version,
                    Message = success ? "Restore completed successfully" : "Restore failed"
                });
            }
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a backup
    /// </summary>
    [HttpDelete("{fileName}/{version}")]
    public async Task<ActionResult<DeleteBackupResponse>> DeleteBackup(string fileName, int version)
    {
        try
        {
            if (!IsValidFileName(fileName, out var error))
            {
                return BadRequest(new ErrorResponse { Error = error! });
            }

            if (version < 1)
            {
                return BadRequest(new ErrorResponse { Error = "Version must be a positive integer" });
            }

            var deleted = await _backupManager.DeleteBackupAsync(fileName, version, _resourcePath);

            if (!deleted)
            {
                return NotFound(new ErrorResponse { Error = $"Backup version {version} not found for '{fileName}'" });
            }

            return Ok(new DeleteBackupResponse
            {
                Success = true,
                FileName = fileName,
                Version = version,
                Message = "Backup deleted successfully"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete all backups for a file
    /// </summary>
    [HttpDelete("{fileName}")]
    public async Task<ActionResult<OperationResponse>> DeleteAllBackups(string fileName)
    {
        try
        {
            if (!IsValidFileName(fileName, out var error))
            {
                return BadRequest(new ErrorResponse { Error = error! });
            }

            await _backupManager.DeleteAllBackupsAsync(fileName, _resourcePath);

            return Ok(new OperationResponse
            {
                Success = true,
                Message = "All backups deleted successfully"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }
}
