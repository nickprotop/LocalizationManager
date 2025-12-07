namespace LrmCloud.Shared.Api;

using System.Text.Json.Serialization;

/// <summary>
/// Standard API response wrapper for successful responses with data.
/// Errors use ProblemDetails (RFC 7807) via ASP.NET Core built-in handling.
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public record ApiResponse<T>
{
    /// <summary>
    /// The response data payload
    /// </summary>
    public required T Data { get; init; }

    /// <summary>
    /// Optional metadata (pagination, timestamp, etc.)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiMeta? Meta { get; init; }
}

/// <summary>
/// Standard API response for operations without data (e.g., delete, update confirmation)
/// </summary>
public record ApiResponse
{
    /// <summary>
    /// A human-readable message describing the result
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional metadata
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiMeta? Meta { get; init; }
}

/// <summary>
/// Metadata for API responses (pagination, timing, etc.)
/// </summary>
public record ApiMeta
{
    /// <summary>
    /// Server timestamp when the response was generated
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Current page number (1-based) for paginated responses
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Page { get; init; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; init; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalPages { get; init; }
}

/// <summary>
/// Helper class for creating pagination metadata
/// </summary>
public static class ApiMetaExtensions
{
    public static ApiMeta ForPage(int page, int pageSize, int totalCount)
    {
        return new ApiMeta
        {
            Timestamp = DateTime.UtcNow,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public static ApiMeta Now() => new() { Timestamp = DateTime.UtcNow };
}
