// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourcesController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public ResourcesController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// List all resource files
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<ResourceFileInfo>> GetResources()
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var result = languages.Select(l => new ResourceFileInfo
            {
                FileName = l.Name,
                FilePath = l.FilePath,
                Code = l.Code,
                IsDefault = l.IsDefault
            });
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get all keys from all resource files (includes duplicate information)
    /// </summary>
    [HttpGet("keys")]
    public ActionResult<IEnumerable<ResourceKeyInfo>> GetAllKeys()
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var allKeys = resourceFiles
                .SelectMany(f => f.Entries.Select(e => e.Key))
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            // Get default file to check for duplicates
            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);

            var keysWithValues = allKeys.Select(key => {
                var values = new Dictionary<string, string?>();
                var isPlural = false;
                foreach (var file in resourceFiles)
                {
                    var entry = file.Entries.FirstOrDefault(e => e.Key == key);
                    values[file.Language.Code ?? "default"] = entry?.Value;
                    // Check if any entry for this key is plural
                    if (entry?.IsPlural == true)
                    {
                        isPlural = true;
                    }
                }

                // Check for duplicates in default file
                var occurrenceCount = defaultFile?.Entries.Count(e => e.Key == key) ?? 1;
                var hasDuplicates = occurrenceCount > 1;

                return new ResourceKeyInfo
                {
                    Key = key,
                    Values = values,
                    OccurrenceCount = occurrenceCount,
                    HasDuplicates = hasDuplicates,
                    IsPlural = isPlural
                };
            });

            return Ok(keysWithValues);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get details of a specific key (supports duplicates)
    /// </summary>
    [HttpGet("keys/{keyName}")]
    public ActionResult<ResourceKeyDetails> GetKey(string keyName)
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            // Check for key existence and duplicates
            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new ErrorResponse { Error = "No default language file found" });
            }

            var occurrences = defaultFile.Entries.Where(e => e.Key == keyName).ToList();
            if (occurrences.Count == 0)
            {
                return NotFound(new ErrorResponse { Error = $"Key '{keyName}' not found" });
            }

            var hasDuplicates = occurrences.Count > 1;

            // If no duplicates, return simple response
            if (!hasDuplicates)
            {
                var values = new Dictionary<string, ResourceValue>();
                foreach (var file in resourceFiles)
                {
                    var entry = file.Entries.FirstOrDefault(e => e.Key == keyName);
                    if (entry != null)
                    {
                        values[file.Language.Code ?? "default"] = new ResourceValue
                        {
                            Value = entry.Value,
                            Comment = entry.Comment,
                            IsPlural = entry.IsPlural,
                            PluralForms = entry.PluralForms
                        };
                    }
                }

                return Ok(new ResourceKeyDetails
                {
                    Key = keyName,
                    Values = values,
                    OccurrenceCount = 1,
                    HasDuplicates = false
                });
            }

            // Handle duplicates - return all occurrences
            var duplicateOccurrences = new List<DuplicateOccurrence>();
            for (int i = 0; i < occurrences.Count; i++)
            {
                var occurrenceValues = new Dictionary<string, ResourceValue>();
                foreach (var file in resourceFiles)
                {
                    var entries = file.Entries.Where(e => e.Key == keyName).ToList();
                    if (i < entries.Count)
                    {
                        occurrenceValues[file.Language.Code ?? "default"] = new ResourceValue
                        {
                            Value = entries[i].Value,
                            Comment = entries[i].Comment,
                            IsPlural = entries[i].IsPlural,
                            PluralForms = entries[i].PluralForms
                        };
                    }
                }

                duplicateOccurrences.Add(new DuplicateOccurrence
                {
                    OccurrenceNumber = i + 1,
                    Values = occurrenceValues
                });
            }

            // Return first occurrence in Values for backward compatibility
            var firstValues = new Dictionary<string, ResourceValue>();
            foreach (var file in resourceFiles)
            {
                var entry = file.Entries.FirstOrDefault(e => e.Key == keyName);
                if (entry != null)
                {
                    firstValues[file.Language.Code ?? "default"] = new ResourceValue
                    {
                        Value = entry.Value,
                        Comment = entry.Comment,
                        IsPlural = entry.IsPlural,
                        PluralForms = entry.PluralForms
                    };
                }
            }

            return Ok(new ResourceKeyDetails
            {
                Key = keyName,
                Values = firstValues,
                OccurrenceCount = occurrences.Count,
                HasDuplicates = true,
                Occurrences = duplicateOccurrences
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Add a new key to all resource files
    /// </summary>
    [HttpPost("keys")]
    public ActionResult<OperationResponse> AddKey([FromBody] AddKeyRequest request)
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            // Check if key already exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key.Equals(request.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ErrorResponse { Error = $"Key '{request.Key}' already exists" });
            }

            // Add the key to all resource files
            foreach (var resourceFile in resourceFiles)
            {
                var langCode = resourceFile.Language.Code ?? "default";

                if (request.IsPlural && request.PluralValues != null)
                {
                    // Add plural key
                    var pluralForms = request.PluralValues.GetValueOrDefault(langCode)
                        ?? new Dictionary<string, string> { ["other"] = "" };

                    resourceFile.Entries.Add(new ResourceEntry
                    {
                        Key = request.Key,
                        Value = pluralForms.GetValueOrDefault("other") ?? pluralForms.Values.FirstOrDefault(),
                        Comment = request.Comment,
                        IsPlural = true,
                        PluralForms = pluralForms
                    });
                }
                else
                {
                    // Add simple key
                    var value = request.Values?.GetValueOrDefault(langCode) ?? string.Empty;

                    resourceFile.Entries.Add(new ResourceEntry
                    {
                        Key = request.Key,
                        Value = value,
                        Comment = request.Comment
                    });
                }

                _backend.Writer.Write(resourceFile);
            }

            return Ok(new OperationResponse
            {
                Success = true,
                Message = "Key added successfully to all resource files"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing key in all resource files (supports occurrence parameter for duplicates)
    /// </summary>
    [HttpPut("keys/{keyName}")]
    public ActionResult<OperationResponse> UpdateKey(string keyName, [FromBody] UpdateKeyRequest request)
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var keyFound = false;

            // Update the key in all resource files
            foreach (var resourceFile in resourceFiles)
            {
                var langCode = string.IsNullOrEmpty(resourceFile.Language.Code) ? "default" : resourceFile.Language.Code;

                if (request.Occurrence.HasValue)
                {
                    // Update specific occurrence
                    var entries = resourceFile.Entries.Where(e => e.Key == keyName).ToList();
                    if (request.Occurrence.Value > 0 && request.Occurrence.Value <= entries.Count)
                    {
                        var entry = entries[request.Occurrence.Value - 1];
                        keyFound = true;

                        // Update value and comment if provided for this language
                        if (request.Values?.TryGetValue(langCode, out var resourceValue) == true)
                        {
                            UpdateEntryFromResourceValue(entry, resourceValue, request.Comment);
                        }
                        else if (request.Comment != null)
                        {
                            // No value for this language, but global comment provided
                            entry.Comment = request.Comment;
                        }
                    }
                }
                else
                {
                    // Update first occurrence (or all if only one exists)
                    var entry = resourceFile.Entries.FirstOrDefault(e => e.Key == keyName);
                    if (entry != null)
                    {
                        keyFound = true;

                        // Update value and comment if provided for this language
                        if (request.Values?.TryGetValue(langCode, out var resourceValue) == true)
                        {
                            UpdateEntryFromResourceValue(entry, resourceValue, request.Comment);
                        }
                        else if (request.Comment != null)
                        {
                            // No value for this language, but global comment provided
                            entry.Comment = request.Comment;
                        }
                    }
                }

                if (keyFound)
                {
                    _backend.Writer.Write(resourceFile);
                }
            }

            if (!keyFound)
            {
                return NotFound(new ErrorResponse { Error = $"Key '{keyName}' not found" });
            }

            var message = request.Occurrence.HasValue
                ? $"Key '{keyName}' occurrence {request.Occurrence.Value} updated successfully"
                : "Key updated successfully";

            return Ok(new OperationResponse
            {
                Success = true,
                Message = message
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a key from all resource files
    /// </summary>
    [HttpDelete("keys/{keyName}")]
    public ActionResult<DeleteKeyResponse> DeleteKey(string keyName, [FromQuery] int? occurrence)
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

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

                _backend.Writer.Write(resourceFile);
            }

            if (deletedCount == 0)
            {
                return NotFound(new ErrorResponse { Error = $"Key '{keyName}' not found" });
            }

            return Ok(new DeleteKeyResponse
            {
                Success = true,
                Key = keyName,
                DeletedCount = deletedCount,
                Message = $"Deleted {deletedCount} occurrence(s) of key '{keyName}'"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Helper method to update a ResourceEntry from an API ResourceValue
    /// </summary>
    private static void UpdateEntryFromResourceValue(ResourceEntry entry, ResourceValue resourceValue, string? globalComment)
    {
        // Handle plural form updates
        if (resourceValue.IsPlural || resourceValue.PluralForms != null)
        {
            entry.IsPlural = true;
            if (resourceValue.PluralForms != null)
            {
                entry.PluralForms ??= new Dictionary<string, string>();
                foreach (var kvp in resourceValue.PluralForms)
                {
                    entry.PluralForms[kvp.Key] = kvp.Value;
                }
                // Keep Value in sync with 'other' form
                entry.Value = entry.PluralForms.GetValueOrDefault("other") ?? entry.PluralForms.Values.FirstOrDefault();
            }
        }
        else if (resourceValue.Value != null)
        {
            // Simple value update
            entry.Value = resourceValue.Value;
        }

        // Per-language comment takes priority over global comment
        if (resourceValue.Comment != null)
        {
            entry.Comment = resourceValue.Comment;
        }
        else if (globalComment != null)
        {
            entry.Comment = globalComment;
        }
    }
}
