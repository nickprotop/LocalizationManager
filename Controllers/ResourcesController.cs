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
    /// List the language columns shown in the editor. Returns one entry per
    /// EFFECTIVE language code across all resource groups (default file and an
    /// explicit culture file sharing the configured DefaultLanguageCode collapse
    /// into a single column), so the headers agree with the merged cells from
    /// <see cref="GetAllKeys"/>. A within-group default-vs-culture collision is
    /// surfaced via <see cref="ResourceFileInfo.HasLanguageConflict"/>; the
    /// legitimate cross-group case (two groups each having an "it" file) is NOT
    /// treated as a conflict.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<ResourceFileInfo>> GetResources()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var allFiles = directory.Groups.SelectMany(g => g.Files).ToList();
            var columns = MergedLanguageColumns.Build(allFiles);

            var result = columns.Select(col => new ResourceFileInfo
            {
                FileName = col.Name,
                FilePath = col.WinningFilePath,
                Code = col.Code,
                IsDefault = col.IsDefault,
                // Cross-group merge would falsely flag two legit same-code files
                // (e.g. CustomerResources.it + GlassResources.it). Only flag a
                // conflict when a SINGLE group has >1 file for this effective code.
                HasLanguageConflict = directory.Groups.Any(g =>
                    g.Files.Count(f => MergedLanguageColumns.EffectiveCode(f) == col.Code) > 1),
                ConflictingFilePaths = col.ConflictingFilePaths.ToList()
            });
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get all keys from all resource files. Returns one row per (Key, ResourceGroup)
    /// tuple so that multiple resource files in the same directory (e.g.
    /// CustomerResources.resx + GlassResources.resx) are not collapsed against each
    /// other.
    /// </summary>
    [HttpGet("keys")]
    public ActionResult<IEnumerable<ResourceKeyInfo>> GetAllKeys()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var rows = new List<ResourceKeyInfo>();

            foreach (var group in directory.Groups)
            {
                var resources = group.Files.ToDictionary(f => f, f => _backend.Reader.Read(f));
                var defaultFile = group.Files.FirstOrDefault(f => f.IsDefault);
                var defaultResource = defaultFile != null ? resources[defaultFile] : null;

                var keys = resources.Values
                    .SelectMany(r => r.Entries.Select(e => e.Key))
                    .Distinct()
                    .OrderBy(k => k)
                    .ToList();

                var fileByPath = group.Files.ToDictionary(f => f.FilePath ?? string.Empty);
                var columns = MergedLanguageColumns.Build(group.Files);

                foreach (var key in keys)
                {
                    var values = new Dictionary<string, string?>();
                    var isPlural = false;
                    var conflictCodes = new List<string>();

                    foreach (var col in columns)
                    {
                        var winnerEntry = resources[fileByPath[col.WinningFilePath]].Entries.FirstOrDefault(e => e.Key == key);
                        var resolvedEntry = winnerEntry;

                        // default wins; culture files fill the gap only when the winner has no value.
                        if (string.IsNullOrEmpty(resolvedEntry?.Value))
                        {
                            foreach (var lp in col.ConflictingFilePaths)
                            {
                                var le = resources[fileByPath[lp]].Entries.FirstOrDefault(e => e.Key == key);
                                if (!string.IsNullOrEmpty(le?.Value)) { resolvedEntry = le; break; }
                            }
                        }

                        values[col.Code] = resolvedEntry?.Value;
                        if (resolvedEntry?.IsPlural == true) isPlural = true;
                        if (col.HasConflict) conflictCodes.Add(col.Code);
                    }

                    var occurrenceCount = defaultResource?.Entries.Count(e => e.Key == key) ?? 1;

                    rows.Add(new ResourceKeyInfo
                    {
                        Key = key,
                        ResourceGroup = group.BaseName,
                        Values = values,
                        OccurrenceCount = occurrenceCount,
                        HasDuplicates = occurrenceCount > 1,
                        IsPlural = isPlural,
                        HasLanguageConflict = conflictCodes.Count > 0,
                        ConflictingLanguages = conflictCodes
                    });
                }
            }

            return Ok(rows);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get details of a specific key (supports duplicates). When the directory
    /// contains multiple resource groups, the caller must specify <c>resourceGroup</c>
    /// to disambiguate; otherwise the controller searches every group and uses
    /// the first match (preserving single-group backwards compatibility).
    /// </summary>
    [HttpGet("keys/{keyName}")]
    public ActionResult<ResourceKeyDetails> GetKey(string keyName, [FromQuery] string? resourceGroup = null)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            ResourceGroup? group;
            if (!string.IsNullOrEmpty(resourceGroup))
            {
                group = directory.Groups.FirstOrDefault(g => g.BaseName.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase));
                if (group is null)
                {
                    return NotFound(new ErrorResponse { Error = $"Resource group '{resourceGroup}' not found" });
                }
            }
            else
            {
                // No group specified: search every group; pick the first one that has this key.
                group = directory.Groups.FirstOrDefault(g =>
                    g.Files.Any(f =>
                    {
                        var rf = _backend.Reader.Read(f);
                        return rf.Entries.Any(e => e.Key == keyName);
                    }));
                if (group is null)
                {
                    return NotFound(new ErrorResponse { Error = $"Key '{keyName}' not found" });
                }
            }

            var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();

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
                    ResourceGroup = group.BaseName,
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
                ResourceGroup = group.BaseName,
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
    /// Add a new key to a specific resource group. When the directory has exactly
    /// one group, <c>ResourceGroup</c> may be omitted; when there are multiple
    /// groups, it is required.
    /// </summary>
    [HttpPost("keys")]
    public ActionResult<OperationResponse> AddKey([FromBody] AddKeyRequest request)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var groupResult = ResolveGroup(directory, request.ResourceGroup);
            if (groupResult.ErrorResult != null) return groupResult.ErrorResult;
            var group = groupResult.Group!;

            var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();

            // Check if key already exists
            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key.Equals(request.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ErrorResponse { Error = $"Key '{request.Key}' already exists" });
            }

            // Add the key to all resource files in this group
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
                Message = $"Key added successfully to all resource files in group '{group.BaseName}'"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing key within a specific resource group (supports occurrence
    /// parameter for duplicates). When the directory has exactly one group,
    /// <c>ResourceGroup</c> may be omitted; when there are multiple groups, it
    /// is required.
    /// </summary>
    [HttpPut("keys/{keyName}")]
    public ActionResult<OperationResponse> UpdateKey(string keyName, [FromBody] UpdateKeyRequest request)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var groupResult = ResolveGroup(directory, request.ResourceGroup);
            if (groupResult.ErrorResult != null) return groupResult.ErrorResult;
            var group = groupResult.Group!;

            var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();

            var keyFound = false;

            // Update the key in all resource files in this group
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
    /// Delete a key from a specific resource group's files. When the directory
    /// has exactly one group, <c>resourceGroup</c> may be omitted; when there
    /// are multiple groups, it is required.
    /// </summary>
    [HttpDelete("keys/{keyName}")]
    public ActionResult<DeleteKeyResponse> DeleteKey(string keyName, [FromQuery] int? occurrence, [FromQuery] string? resourceGroup = null)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var groupResult = ResolveGroup(directory, resourceGroup);
            if (groupResult.ErrorResult != null) return groupResult.ErrorResult;
            var group = groupResult.Group!;

            var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();

            var deletedCount = 0;

            // Delete the key from all resource files in this group
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
    /// Resolves a request's optional <c>ResourceGroup</c> to a concrete group.
    /// Returns the only group if the directory contains exactly one (preserving
    /// single-group behavior); otherwise requires the caller to specify and
    /// returns BadRequest/NotFound if not provided/found.
    /// </summary>
    private (ResourceGroup? Group, ActionResult? ErrorResult) ResolveGroup(ResourceDirectory directory, string? requestedGroup)
    {
        if (directory.Groups.Count == 0)
        {
            return (null, NotFound(new ErrorResponse { Error = "No resource groups found in resource path" }));
        }

        if (!string.IsNullOrEmpty(requestedGroup))
        {
            var match = directory.Groups.FirstOrDefault(g => g.BaseName.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return (null, NotFound(new ErrorResponse { Error = $"Resource group '{requestedGroup}' not found" }));
            }
            return (match, null);
        }

        if (directory.Groups.Count == 1)
        {
            return (directory.Groups[0], null);
        }

        return (null, BadRequest(new ErrorResponse { Error = "ResourceGroup is required when multiple resource groups exist in the directory" }));
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
