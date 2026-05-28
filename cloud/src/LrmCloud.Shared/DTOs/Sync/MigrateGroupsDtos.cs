// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LrmCloud.Shared.DTOs.Sync;

/// <summary>
/// Request to bulk-rekey resource keys from one BaseName to another.
/// Used when a project that previously had a single resource group grows a
/// second group: existing rows live under <see cref="FromBaseName"/> (usually
/// <c>""</c>) and need to move to <see cref="ToBaseName"/>.
/// </summary>
public class MigrateGroupsRequest
{
    /// <summary>
    /// BaseName to migrate FROM. Empty string targets legacy single-group rows.
    /// </summary>
    public string FromBaseName { get; set; } = string.Empty;

    /// <summary>
    /// BaseName to migrate TO. Must be non-empty.
    /// </summary>
    public required string ToBaseName { get; set; }
}

/// <summary>
/// Response from a migrate-groups operation.
/// </summary>
public class MigrateGroupsResponse
{
    /// <summary>Number of resource key rows that were rekeyed.</summary>
    public int RowsUpdated { get; set; }

    /// <summary>
    /// Keys that could not be migrated because a row with the target
    /// <c>(ProjectId, ToBaseName, KeyName)</c> already exists. The caller
    /// must resolve these (delete one side, edit the target, etc.) before
    /// retrying.
    /// </summary>
    public List<string> ConflictingKeys { get; set; } = new();
}
