namespace LrmCloud.Shared.Api;

/// <summary>
/// Response for sync conflicts (matches ROADMAP specification)
/// </summary>
public record ConflictResponse
{
    public string Status { get; init; } = "conflict";
    public required List<ResourceConflict> Conflicts { get; init; }
}

/// <summary>
/// Individual resource conflict details
/// </summary>
public record ResourceConflict
{
    public required string Key { get; init; }
    public required string Language { get; init; }
    public required ConflictVersion Local { get; init; }
    public required ConflictVersion Remote { get; init; }
}

/// <summary>
/// Version details for conflict comparison
/// </summary>
public record ConflictVersion
{
    public required string Value { get; init; }
    public required int Version { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}
