// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MergeDuplicatesController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public MergeDuplicatesController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// Get list of all duplicate keys across every resource group.
    /// </summary>
    [HttpGet("list")]
    public ActionResult<DuplicateKeysResponse> ListDuplicates()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var duplicateKeys = new List<DuplicateKeyInfo>();

            foreach (var group in directory.Groups)
            {
                var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
                var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
                if (defaultFile == null) continue;

                var groupDuplicates = defaultFile.Entries
                    .GroupBy(e => e.Key)
                    .Where(g => g.Count() > 1)
                    .Select(g =>
                    {
                        var valuesByLanguage = new Dictionary<string, List<string>>();
                        foreach (var file in resourceFiles)
                        {
                            var entries = file.Entries.Where(e => e.Key == g.Key).ToList();
                            valuesByLanguage[file.Language.Code ?? "default"] = entries.Select(e => e.Value ?? "").ToList();
                        }
                        return new DuplicateKeyInfo
                        {
                            Key = g.Key,
                            OccurrenceCount = g.Count(),
                            ValuesByLanguage = valuesByLanguage
                        };
                    });
                duplicateKeys.AddRange(groupDuplicates);
            }

            return Ok(new DuplicateKeysResponse
            {
                DuplicateKeys = duplicateKeys,
                TotalDuplicateKeys = duplicateKeys.Count
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Merge duplicate keys (auto-first strategy). Operates within a single
    /// resource group at a time; when <c>MergeAll</c> is true, every group is
    /// processed independently so a key duplicated in two groups doesn't get
    /// collapsed across the group boundary.
    /// </summary>
    [HttpPost("merge")]
    public ActionResult<MergeDuplicatesResponse> MergeDuplicates([FromBody] MergeDuplicatesRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Key) && !request.MergeAll)
            {
                return BadRequest(new ErrorResponse { Error = "You must specify a key or set mergeAll to true" });
            }

            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var mergedKeys = new List<string>();

            foreach (var group in directory.Groups)
            {
                var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
                var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
                if (defaultFile == null) continue;

                List<string> keysToMerge;

                if (request.MergeAll)
                {
                    keysToMerge = defaultFile.Entries
                        .GroupBy(e => e.Key)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
                }
                else
                {
                    var occurrenceCount = defaultFile.Entries.Count(e => e.Key == request.Key);
                    if (occurrenceCount <= 1)
                    {
                        // Key not in this group, or not duplicated here — move on.
                        continue;
                    }
                    keysToMerge = new List<string> { request.Key! };
                }

                foreach (var key in keysToMerge)
                {
                    foreach (var resourceFile in resourceFiles)
                    {
                        var entries = resourceFile.Entries.Where(e => e.Key == key).ToList();
                        if (entries.Count > 1)
                        {
                            for (int i = entries.Count - 1; i >= 1; i--)
                            {
                                resourceFile.Entries.Remove(entries[i]);
                            }
                        }
                    }
                    mergedKeys.Add(key);
                }

                foreach (var file in resourceFiles)
                {
                    _backend.Writer.Write(file);
                }
            }

            if (!request.MergeAll && mergedKeys.Count == 0)
            {
                // Single-key merge requested but no group had it as a duplicate.
                var anyOccurrence = directory.Groups
                    .SelectMany(g => g.Files)
                    .Select(f => _backend.Reader.Read(f))
                    .Any(rf => rf.Entries.Any(e => e.Key == request.Key));
                if (!anyOccurrence)
                {
                    return NotFound(new ErrorResponse { Error = $"Key '{request.Key}' not found" });
                }
                return BadRequest(new ErrorResponse { Error = $"Key '{request.Key}' has no duplicates" });
            }

            var message = request.MergeAll
                ? $"Merged {mergedKeys.Count} duplicate key(s) across {directory.Groups.Count} resource group(s) (kept first occurrence from each language)"
                : $"Merged '{request.Key}' (kept first occurrence from each language)";

            return Ok(new MergeDuplicatesResponse
            {
                Success = true,
                MergedCount = mergedKeys.Count,
                MergedKeys = mergedKeys,
                Message = message
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }
}
