// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Models;

/// <summary>
/// The output of <c>IResourceDiscovery.DiscoverResourceGroups</c>: zero or
/// more groups in a directory, plus the distinct culture codes present
/// across all groups (the empty string represents the invariant/default culture).
/// </summary>
public class ResourceDirectory
{
    public required IReadOnlyList<ResourceGroup> Groups { get; init; }

    /// <summary>Distinct culture codes across all groups. Includes "" for the default/invariant culture if any group has a default file.</summary>
    public required IReadOnlyList<string> CultureCodes { get; init; }
}
