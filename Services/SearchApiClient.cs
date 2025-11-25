// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Net.Http.Json;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Services;

/// <summary>
/// API client for search operations
/// </summary>
public class SearchApiClient
{
    private readonly HttpClient _httpClient;

    public SearchApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LrmApi");
    }

    /// <summary>
    /// Search and filter resource keys
    /// </summary>
    public async Task<SearchResponse?> SearchAsync(SearchRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/search", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SearchResponse>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Search with simple parameters (convenience method)
    /// </summary>
    public async Task<SearchResponse?> SearchAsync(
        string? pattern,
        string filterMode = "substring",
        bool caseSensitive = false,
        List<string>? statusFilters = null,
        int? limit = null)
    {
        var request = new SearchRequest
        {
            Pattern = pattern,
            FilterMode = filterMode,
            CaseSensitive = caseSensitive,
            StatusFilters = statusFilters,
            Limit = limit
        };
        return await SearchAsync(request);
    }
}
