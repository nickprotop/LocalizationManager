// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourcesController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public ResourcesController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
    }

    /// <summary>
    /// List all resource files
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<object>> GetResources()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var result = languages.Select(l => new
            {
                fileName = l.Name,
                filePath = l.FilePath,
                code = l.Code,
                isDefault = l.IsDefault
            });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all keys from all resource files
    /// </summary>
    [HttpGet("keys")]
    public ActionResult<object> GetAllKeys()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var allKeys = resourceFiles
                .SelectMany(f => f.Entries.Select(e => e.Key))
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            var keysWithValues = allKeys.Select(key => {
                var values = new Dictionary<string, string?>();
                foreach (var file in resourceFiles)
                {
                    var entry = file.Entries.FirstOrDefault(e => e.Key == key);
                    values[file.Language.Code ?? "default"] = entry?.Value;
                }
                return new { key, values };
            });

            return Ok(keysWithValues);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get details of a specific key
    /// </summary>
    [HttpGet("keys/{keyName}")]
    public ActionResult<object> GetKey(string keyName)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var values = new Dictionary<string, object>();
            foreach (var file in resourceFiles)
            {
                var entry = file.Entries.FirstOrDefault(e => e.Key == keyName);
                if (entry != null)
                {
                    values[file.Language.Code ?? "default"] = new
                    {
                        value = entry.Value,
                        comment = entry.Comment
                    };
                }
            }

            if (values.Count == 0)
            {
                return NotFound(new { error = $"Key '{keyName}' not found" });
            }

            return Ok(new { key = keyName, values });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add a new key to all resource files
    /// </summary>
    [HttpPost("keys")]
    public ActionResult<object> AddKey([FromBody] AddKeyRequest request)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            // Check if key already exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key.Equals(request.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new { error = $"Key '{request.Key}' already exists" });
            }

            // Add the key to all resource files
            foreach (var resourceFile in resourceFiles)
            {
                var value = request.Values?.ContainsKey(resourceFile.Language.Code ?? "default") == true
                    ? request.Values[resourceFile.Language.Code ?? "default"]
                    : string.Empty;

                resourceFile.Entries.Add(new ResourceEntry
                {
                    Key = request.Key,
                    Value = value,
                    Comment = request.Comment
                });

                _parser.Write(resourceFile);
            }

            return Ok(new
            {
                success = true,
                key = request.Key,
                message = "Key added successfully to all resource files"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing key in all resource files
    /// </summary>
    [HttpPut("keys/{keyName}")]
    public ActionResult<object> UpdateKey(string keyName, [FromBody] UpdateKeyRequest request)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var keyFound = false;

            // Update the key in all resource files
            foreach (var resourceFile in resourceFiles)
            {
                var entry = resourceFile.Entries.FirstOrDefault(e => e.Key == keyName);
                if (entry != null)
                {
                    keyFound = true;

                    // Update value if provided for this language
                    if (request.Values?.ContainsKey(resourceFile.Language.Code ?? "default") == true)
                    {
                        entry.Value = request.Values[resourceFile.Language.Code ?? "default"];
                    }

                    // Update comment if provided
                    if (request.Comment != null)
                    {
                        entry.Comment = request.Comment;
                    }

                    _parser.Write(resourceFile);
                }
            }

            if (!keyFound)
            {
                return NotFound(new { error = $"Key '{keyName}' not found" });
            }

            return Ok(new
            {
                success = true,
                key = keyName,
                message = "Key updated successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a key from all resource files
    /// </summary>
    [HttpDelete("keys/{keyName}")]
    public ActionResult<object> DeleteKey(string keyName, [FromQuery] int? occurrence)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var deletedCount = 0;

            // Delete the key from all resource files
            foreach (var resourceFile in resourceFiles)
            {
                if (occurrence.HasValue)
                {
                    // Delete specific occurrence
                    var entries = resourceFile.Entries.Where(e => e.Key == keyName).ToList();
                    if (occurrence.Value > 0 && occurrence.Value <= entries.Count)
                    {
                        resourceFile.Entries.Remove(entries[occurrence.Value - 1]);
                        deletedCount++;
                    }
                }
                else
                {
                    // Delete all occurrences
                    var removed = resourceFile.Entries.RemoveAll(e => e.Key == keyName);
                    deletedCount += removed;
                }

                _parser.Write(resourceFile);
            }

            if (deletedCount == 0)
            {
                return NotFound(new { error = $"Key '{keyName}' not found" });
            }

            return Ok(new
            {
                success = true,
                key = keyName,
                deletedCount,
                message = $"Deleted {deletedCount} occurrence(s) of key '{keyName}'"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class AddKeyRequest
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string?>? Values { get; set; }
    public string? Comment { get; set; }
}

public class UpdateKeyRequest
{
    public Dictionary<string, string?>? Values { get; set; }
    public string? Comment { get; set; }
}
