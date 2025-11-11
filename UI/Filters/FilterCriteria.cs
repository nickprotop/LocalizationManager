// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.UI.Filters;

/// <summary>
/// Represents the criteria for filtering resource entries in the TUI
/// </summary>
public class FilterCriteria
{
    /// <summary>
    /// The search text/pattern to filter by
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// The filtering mode (substring, wildcard, regex)
    /// </summary>
    public FilterMode Mode { get; set; } = FilterMode.Substring;

    /// <summary>
    /// Whether the search should be case-sensitive
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Search scope: keys only or keys + values
    /// </summary>
    public SearchScope Scope { get; set; } = SearchScope.KeysAndValues;

    /// <summary>
    /// Specific column to search in (null = all columns based on scope)
    /// </summary>
    public string? TargetColumn { get; set; } = null;

    /// <summary>
    /// List of language codes that are currently visible
    /// </summary>
    public List<string> VisibleLanguageCodes { get; set; } = new();
}

/// <summary>
/// The type of pattern matching to use for filtering
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// Simple substring matching (case-insensitive LIKE)
    /// </summary>
    Substring,

    /// <summary>
    /// Wildcard pattern matching (* and ?)
    /// </summary>
    Wildcard,

    /// <summary>
    /// Regular expression pattern matching
    /// </summary>
    Regex
}

/// <summary>
/// The scope of the search: keys only or keys + translation values
/// </summary>
public enum SearchScope
{
    /// <summary>
    /// Search in both key names and translation values
    /// </summary>
    KeysAndValues,

    /// <summary>
    /// Search only in key names (useful for patterns like "Error.*")
    /// </summary>
    KeysOnly
}
