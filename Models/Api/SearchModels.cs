// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Models.Api;

/// <summary>
/// Request model for searching/filtering resource keys
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// The search pattern (text, wildcard, or regex depending on FilterMode)
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// The filtering mode: "substring" (default), "wildcard", or "regex"
    /// </summary>
    public string FilterMode { get; set; } = "substring";

    /// <summary>
    /// Whether the search should be case-sensitive (default: false)
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Search scope: "keys", "values", "keysAndValues" (default), "comments", or "all"
    /// </summary>
    public string SearchScope { get; set; } = "keysAndValues";

    /// <summary>
    /// Optional status filters: "missing", "extra", "duplicates", "unused"
    /// Multiple can be specified (OR logic)
    /// </summary>
    public List<string>? StatusFilters { get; set; }

    /// <summary>
    /// Maximum number of results to return (for pagination)
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Number of results to skip (for pagination)
    /// </summary>
    public int? Offset { get; set; }
}

/// <summary>
/// Response model for search results
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// The filtered resource keys matching the search criteria
    /// </summary>
    public List<ResourceKeyInfo> Results { get; set; } = new();

    /// <summary>
    /// Total number of keys before filtering
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of keys matching the filter (before pagination)
    /// </summary>
    public int FilteredCount { get; set; }

    /// <summary>
    /// The filter mode that was applied
    /// </summary>
    public string AppliedFilterMode { get; set; } = "substring";
}
