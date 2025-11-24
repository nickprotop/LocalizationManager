// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Backup;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceDiscovery _discovery;
    private readonly BackupVersionManager _backupManager;

    public BackupController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _discovery = new ResourceDiscovery();
        _backupManager = new BackupVersionManager();
    }

    /// <summary>
    /// List all backups
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> ListBackups([FromQuery] string? fileName)
    {
        try
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                var backups = await _backupManager.ListBackupsAsync(fileName, _resourcePath);
                return Ok(new
                {
                    fileName,
                    backupCount = backups.Count,
                    backups = backups.Select(b => new
                    {
                        version = b.Version,
                        timestamp = b.Timestamp,
                        operation = b.Operation,
                        keyCount = b.KeyCount,
                        changedKeys = b.ChangedKeys,
                        changedKeyNames = b.ChangedKeyNames
                    })
                });
            }
            else
            {
                var languages = _discovery.DiscoverLanguages(_resourcePath);
                var allBackupsTasks = languages.Select(async lang =>
                {
                    var backups = await _backupManager.ListBackupsAsync(lang.Name, _resourcePath);
                    return backups.Select(b => new
                    {
                        fileName = lang.Name,
                        version = b.Version,
                        timestamp = b.Timestamp,
                        operation = b.Operation,
                        keyCount = b.KeyCount,
                        changedKeys = b.ChangedKeys
                    });
                });

                var allBackups = (await Task.WhenAll(allBackupsTasks))
                    .SelectMany(x => x)
                    .OrderByDescending(b => b.timestamp);

                return Ok(new { backups = allBackups });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a backup
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateBackup([FromBody] CreateBackupRequest request)
    {
        try
        {
            var parser = new ResourceFileParser();

            if (!string.IsNullOrEmpty(request.FileName))
            {
                var languages = _discovery.DiscoverLanguages(_resourcePath);
                var language = languages.FirstOrDefault(l => l.Name == request.FileName);
                if (language == null)
                {
                    return NotFound(new { error = $"File '{request.FileName}' not found" });
                }

                var resourceFile = parser.Parse(language);
                var metadata = await _backupManager.CreateBackupAsync(
                    language.FilePath,
                    request.Operation ?? "manual",
                    _resourcePath);

                return Ok(new
                {
                    success = true,
                    fileName = request.FileName,
                    version = metadata.Version,
                    operation = request.Operation ?? "manual"
                });
            }
            else
            {
                // Backup all files
                var languages = _discovery.DiscoverLanguages(_resourcePath);
                var results = new List<object>();

                foreach (var lang in languages)
                {
                    var resourceFile = parser.Parse(lang);
                    var metadata = await _backupManager.CreateBackupAsync(
                        lang.FilePath,
                        request.Operation ?? "manual",
                        _resourcePath);
                    results.Add(new
                    {
                        fileName = lang.Name,
                        version = metadata.Version
                    });
                }

                return Ok(new
                {
                    success = true,
                    operation = request.Operation ?? "manual",
                    backups = results
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get backup info
    /// </summary>
    [HttpGet("{fileName}/{version}")]
    public async Task<ActionResult<object>> GetBackupInfo(string fileName, int version)
    {
        try
        {
            var backup = await _backupManager.GetBackupAsync(fileName, version, _resourcePath);

            if (backup == null)
            {
                return NotFound(new { error = $"Backup version {version} not found for '{fileName}'" });
            }

            return Ok(new
            {
                fileName,
                version = backup.Version,
                timestamp = backup.Timestamp,
                operation = backup.Operation,
                keyCount = backup.KeyCount,
                changedKeys = backup.ChangedKeys,
                changedKeyNames = backup.ChangedKeyNames
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Restore from backup
    /// </summary>
    [HttpPost("{fileName}/{version}/restore")]
    public async Task<ActionResult<object>> RestoreBackup(string fileName, int version, [FromBody] RestoreBackupRequest? request)
    {
        try
        {
            var parser = new ResourceFileParser();
            var restoreService = new BackupRestoreService(_backupManager);
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var language = languages.FirstOrDefault(l => l.Name == fileName);

            if (language == null)
            {
                return NotFound(new { error = $"File '{fileName}' not found" });
            }

            var currentFile = parser.Parse(language);

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

                return Ok(new
                {
                    preview = true,
                    added = added.Count,
                    modified = modified.Count,
                    removed = removed.Count,
                    changes = new
                    {
                        added = added.Select(c => c.Key),
                        modified = modified.Select(m => new
                        {
                            key = m.Key,
                            currentValue = m.OldValue,
                            backupValue = m.NewValue
                        }),
                        removed = removed.Select(c => c.Key)
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

                return Ok(new
                {
                    success = restoredCount > 0,
                    fileName,
                    version,
                    restoredKeys = restoredCount
                });
            }
            else
            {
                var success = await restoreService.RestoreAsync(
                    fileName,
                    version,
                    language.FilePath,
                    _resourcePath);

                return Ok(new
                {
                    success,
                    fileName,
                    version,
                    message = success ? "Restore completed successfully" : "Restore failed"
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a backup
    /// </summary>
    [HttpDelete("{fileName}/{version}")]
    public async Task<ActionResult<object>> DeleteBackup(string fileName, int version)
    {
        try
        {
            var deleted = await _backupManager.DeleteBackupAsync(fileName, version, _resourcePath);

            if (!deleted)
            {
                return NotFound(new { error = $"Backup version {version} not found for '{fileName}'" });
            }

            return Ok(new
            {
                success = true,
                fileName,
                version,
                message = "Backup deleted successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete all backups for a file
    /// </summary>
    [HttpDelete("{fileName}")]
    public async Task<ActionResult<object>> DeleteAllBackups(string fileName)
    {
        try
        {
            await _backupManager.DeleteAllBackupsAsync(fileName, _resourcePath);

            return Ok(new
            {
                success = true,
                fileName,
                message = "All backups deleted successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class CreateBackupRequest
{
    public string? FileName { get; set; }
    public string? Operation { get; set; }
}

public class RestoreBackupRequest
{
    public bool Preview { get; set; }
    public List<string>? Keys { get; set; }
}
