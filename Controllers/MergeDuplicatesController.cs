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
    /// Get list of all duplicate keys
    /// </summary>
    [HttpGet("list")]
    public ActionResult<DuplicateKeysResponse> ListDuplicates()
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new ErrorResponse { Error = "No default language file found" });
            }

            // Find all keys with duplicates
            var duplicateKeys = defaultFile.Entries
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
                })
                .ToList();

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
    /// Merge duplicate keys (auto-first strategy)
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

            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new ErrorResponse { Error = "No default language file found" });
            }

            var mergedKeys = new List<string>();
            var keysToMerge = new List<string>();

            if (request.MergeAll)
            {
                // Find all keys with duplicates
                keysToMerge = defaultFile.Entries
                    .GroupBy(e => e.Key)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (keysToMerge.Count == 0)
                {
                    return Ok(new MergeDuplicatesResponse
                    {
                        Success = true,
                        MergedCount = 0,
                        Message = "No duplicate keys found"
                    });
                }
            }
            else
            {
                // Check if key exists and has duplicates
                var occurrenceCount = defaultFile.Entries.Count(e => e.Key == request.Key);
                if (occurrenceCount == 0)
                {
                    return NotFound(new ErrorResponse { Error = $"Key '{request.Key}' not found" });
                }
                if (occurrenceCount == 1)
                {
                    return BadRequest(new ErrorResponse { Error = $"Key '{request.Key}' has no duplicates" });
                }

                keysToMerge.Add(request.Key!);
            }

            // Merge each key (keeping first occurrence, removing others)
            foreach (var key in keysToMerge)
            {
                foreach (var resourceFile in resourceFiles)
                {
                    var entries = resourceFile.Entries.Where(e => e.Key == key).ToList();
                    if (entries.Count > 1)
                    {
                        // Keep first occurrence, remove others
                        for (int i = entries.Count - 1; i >= 1; i--)
                        {
                            resourceFile.Entries.Remove(entries[i]);
                        }
                    }
                }

                mergedKeys.Add(key);
            }

            // Write all updated files
            foreach (var file in resourceFiles)
            {
                _backend.Writer.Write(file);
            }

            var message = request.MergeAll
                ? $"Merged {mergedKeys.Count} duplicate key(s) (kept first occurrence from each language)"
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
