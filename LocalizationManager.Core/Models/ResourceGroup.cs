// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Models;

/// <summary>
/// A resource group bundles one base resource name (e.g. "SharedResource") with
/// its per-culture files. Each <see cref="LanguageInfo"/> in <see cref="Files"/>
/// shares the same <see cref="BaseName"/>.
/// </summary>
public class ResourceGroup
{
    /// <summary>Base resource name shared by all files in this group.</summary>
    public required string BaseName { get; init; }

    /// <summary>One entry per culture for this base name. At least one entry; one will be marked IsDefault.</summary>
    public required IReadOnlyList<LanguageInfo> Files { get; init; }
}
