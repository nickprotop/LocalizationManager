// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;
using LocalizationManager.UI.Filters;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;
    private readonly ResourceFilterService _filterService;

    public SearchController(IConfiguration configuration, IResourceBackend backend, ResourceFilterService filterService)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
        _filterService = filterService;
    }

    /// <summary>
    /// Search and filter resource keys
    /// </summary>
    [HttpPost]
    public ActionResult<SearchResponse> Search([FromBody] SearchRequest request)
    {
        try
        {
            // Load all keys
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var allKeys = resourceFiles
                .SelectMany(f => f.Entries.Select(e => e.Key))
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);

            var keysWithValues = allKeys.Select(key =>
            {
                var values = new Dictionary<string, string?>();
                foreach (var file in resourceFiles)
                {
                    var entry = file.Entries.FirstOrDefault(e => e.Key == key);
                    values[file.Language.Code ?? "default"] = entry?.Value;
                }

                var occurrenceCount = defaultFile?.Entries.Count(e => e.Key == key) ?? 1;
                var hasDuplicates = occurrenceCount > 1;

                return new ResourceKeyInfo
                {
                    Key = key,
                    Values = values,
                    OccurrenceCount = occurrenceCount,
                    HasDuplicates = hasDuplicates
                };
            }).ToList();

            var totalCount = keysWithValues.Count;

            // Build FilterCriteria from request
            var criteria = new FilterCriteria
            {
                SearchText = request.Pattern ?? "",
                Mode = ParseFilterMode(request.FilterMode),
                CaseSensitive = request.CaseSensitive,
                Scope = ParseSearchScope(request.SearchScope)
            };

            // Apply text filter using ResourceFilterService
            var filtered = _filterService.FilterKeys(keysWithValues, criteria);

            // Apply status filters if specified
            if (request.StatusFilters?.Any() == true)
            {
                filtered = ApplyStatusFilters(filtered, request.StatusFilters, resourceFiles, defaultFile);
            }

            var filteredCount = filtered.Count;

            // Apply pagination
            if (request.Offset.HasValue && request.Offset.Value > 0)
            {
                filtered = filtered.Skip(request.Offset.Value).ToList();
            }
            if (request.Limit.HasValue && request.Limit.Value > 0)
            {
                filtered = filtered.Take(request.Limit.Value).ToList();
            }

            return Ok(new SearchResponse
            {
                Results = filtered,
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                AppliedFilterMode = request.FilterMode
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    private FilterMode ParseFilterMode(string mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "wildcard" => FilterMode.Wildcard,
            "regex" => FilterMode.Regex,
            _ => FilterMode.Substring
        };
    }

    private SearchScope ParseSearchScope(string scope)
    {
        return scope?.ToLowerInvariant() switch
        {
            "keys" => SearchScope.KeysOnly,
            "values" => SearchScope.KeysAndValues, // values only not directly supported, use keysAndValues
            "comments" => SearchScope.Comments,
            "all" => SearchScope.All,
            _ => SearchScope.KeysAndValues
        };
    }

    private List<ResourceKeyInfo> ApplyStatusFilters(
        List<ResourceKeyInfo> keys,
        List<string> statusFilters,
        List<ResourceFile> resourceFiles,
        ResourceFile? defaultFile)
    {
        var statusFiltersLower = statusFilters.Select(s => s.ToLowerInvariant()).ToHashSet();

        // Build sets for each status
        var missingKeys = new HashSet<string>();
        var extraKeys = new HashSet<string>();
        var duplicateKeys = new HashSet<string>();

        // Find missing translations (keys in default but missing values in other languages)
        if (statusFiltersLower.Contains("missing") && defaultFile != null)
        {
            var defaultKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();
            foreach (var file in resourceFiles.Where(f => !f.Language.IsDefault))
            {
                var fileKeys = file.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => e.Key)
                    .ToHashSet();

                foreach (var key in defaultKeys.Where(k => !fileKeys.Contains(k)))
                {
                    missingKeys.Add(key);
                }
            }
        }

        // Find extra keys (keys in non-default but not in default)
        if (statusFiltersLower.Contains("extra") && defaultFile != null)
        {
            var defaultKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();
            foreach (var file in resourceFiles.Where(f => !f.Language.IsDefault))
            {
                foreach (var entry in file.Entries.Where(e => !defaultKeys.Contains(e.Key)))
                {
                    extraKeys.Add(entry.Key);
                }
            }
        }

        // Find duplicate keys
        if (statusFiltersLower.Contains("duplicates") && defaultFile != null)
        {
            var duplicates = defaultFile.Entries
                .GroupBy(e => e.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var key in duplicates)
            {
                duplicateKeys.Add(key);
            }
        }

        // Filter keys based on status (OR logic)
        return keys.Where(k =>
            (statusFiltersLower.Contains("missing") && missingKeys.Contains(k.Key)) ||
            (statusFiltersLower.Contains("extra") && extraKeys.Contains(k.Key)) ||
            (statusFiltersLower.Contains("duplicates") && duplicateKeys.Contains(k.Key))
        ).ToList();
    }
}
