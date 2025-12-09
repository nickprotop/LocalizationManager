// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Detects conflicts between local and remote resources and configuration.
/// </summary>
public class ConflictDetector
{
    /// <summary>
    /// Conflict resolution strategy.
    /// </summary>
    public enum ResolutionStrategy
    {
        /// <summary>Keep local version</summary>
        Local,
        /// <summary>Use remote version</summary>
        Remote,
        /// <summary>Prompt user to choose</summary>
        Prompt,
        /// <summary>Abort operation</summary>
        Abort
    }

    /// <summary>
    /// Represents a detected conflict.
    /// </summary>
    public class Conflict
    {
        public ConflictType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? LocalContent { get; set; }
        public string? RemoteContent { get; set; }
        public string? LocalHash { get; set; }
        public string? RemoteHash { get; set; }
        public ResolutionStrategy? Resolution { get; set; }
    }

    /// <summary>
    /// Type of conflict.
    /// </summary>
    public enum ConflictType
    {
        /// <summary>Both local and remote modified</summary>
        BothModified,
        /// <summary>File deleted locally but modified remotely</summary>
        DeletedLocallyModifiedRemotely,
        /// <summary>File deleted remotely but modified locally</summary>
        DeletedRemotelyModifiedLocally,
        /// <summary>Configuration conflict</summary>
        ConfigurationConflict
    }

    /// <summary>
    /// Detect conflicts between local and remote resources.
    /// </summary>
    public List<Conflict> DetectResourceConflicts(
        List<FileDto> localResources,
        List<FileDto> remoteResources)
    {
        var conflicts = new List<Conflict>();

        var localByPath = localResources.ToDictionary(r => r.Path, r => r);
        var remoteByPath = remoteResources.ToDictionary(r => r.Path, r => r);

        // Check for conflicts in existing files
        foreach (var local in localResources)
        {
            if (remoteByPath.TryGetValue(local.Path, out var remote))
            {
                // Both exist - check if different
                if (local.Hash != remote.Hash)
                {
                    conflicts.Add(new Conflict
                    {
                        Type = ConflictType.BothModified,
                        Path = local.Path,
                        LocalContent = local.Content,
                        RemoteContent = remote.Content,
                        LocalHash = local.Hash,
                        RemoteHash = remote.Hash
                    });
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Detect conflicts between local and remote configuration.
    /// </summary>
    public Conflict? DetectConfigurationConflict(
        string localConfigJson,
        string remoteConfigJson)
    {
        // Parse both configurations
        var localConfig = JsonSerializer.Deserialize<JsonElement>(localConfigJson);
        var remoteConfig = JsonSerializer.Deserialize<JsonElement>(remoteConfigJson);

        // Compare - if different, it's a conflict
        if (!JsonSerializer.Serialize(localConfig) .Equals(JsonSerializer.Serialize(remoteConfig)))
        {
            return new Conflict
            {
                Type = ConflictType.ConfigurationConflict,
                Path = "lrm.json",
                LocalContent = localConfigJson,
                RemoteContent = remoteConfigJson
            };
        }

        return null;
    }

    /// <summary>
    /// Get a summary of differences between local and remote.
    /// </summary>
    public DiffSummary GetDiffSummary(
        List<FileDto> localResources,
        List<FileDto> remoteResources)
    {
        var summary = new DiffSummary();

        var localByPath = localResources.ToDictionary(r => r.Path, r => r);
        var remoteByPath = remoteResources.ToDictionary(r => r.Path, r => r);

        // Files only in remote (new files to pull)
        summary.FilesToAdd = remoteResources
            .Where(r => !localByPath.ContainsKey(r.Path))
            .Select(r => r.Path)
            .ToList();

        // Files only in local (files that were deleted remotely)
        summary.FilesToDelete = localResources
            .Where(r => !remoteByPath.ContainsKey(r.Path))
            .Select(r => r.Path)
            .ToList();

        // Files in both but different (modified files)
        summary.FilesToUpdate = localResources
            .Where(r => remoteByPath.ContainsKey(r.Path) && r.Hash != remoteByPath[r.Path].Hash)
            .Select(r => r.Path)
            .ToList();

        return summary;
    }

    /// <summary>
    /// Summary of differences between local and remote.
    /// </summary>
    public class DiffSummary
    {
        public List<string> FilesToAdd { get; set; } = new();
        public List<string> FilesToUpdate { get; set; } = new();
        public List<string> FilesToDelete { get; set; } = new();

        public int TotalChanges => FilesToAdd.Count + FilesToUpdate.Count + FilesToDelete.Count;
        public bool HasChanges => TotalChanges > 0;
    }
}
