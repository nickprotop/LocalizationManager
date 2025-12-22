// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.Core.Cloud.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Performs three-way merge for lrm.json configuration properties.
/// </summary>
public class ConfigMerger
{
    /// <summary>
    /// Computes changes between local config and last sync state (for push).
    /// </summary>
    /// <param name="localProperties">Current local config properties with hashes</param>
    /// <param name="syncState">Last sync state (null for first push)</param>
    /// <returns>Configuration changes to push</returns>
    public ConfigChanges ComputePushChanges(
        Dictionary<string, (string Value, string Hash)> localProperties,
        SyncState? syncState)
    {
        var result = new ConfigChanges();
        var seenPaths = new HashSet<string>();

        foreach (var (path, (value, hash)) in localProperties)
        {
            seenPaths.Add(path);
            var baseHash = syncState?.ConfigProperties.GetValueOrDefault(path);

            if (baseHash == null)
            {
                // New property
                result.Changes.Add(new ConfigPropertyChange
                {
                    Path = path,
                    Value = value,
                    BaseHash = null
                });
            }
            else if (baseHash != hash)
            {
                // Modified property
                result.Changes.Add(new ConfigPropertyChange
                {
                    Path = path,
                    Value = value,
                    BaseHash = baseHash
                });
            }
        }

        // Find deleted properties
        if (syncState != null)
        {
            foreach (var path in syncState.ConfigProperties.Keys)
            {
                if (!seenPaths.Contains(path))
                {
                    result.Deletions.Add(new ConfigPropertyDeletion
                    {
                        Path = path,
                        BaseHash = syncState.ConfigProperties[path]
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Performs three-way merge of remote config with local state (for pull).
    /// </summary>
    /// <param name="localProperties">Current local config properties with hashes</param>
    /// <param name="remoteConfig">Configuration from server</param>
    /// <param name="syncState">Last sync state (BASE for three-way merge)</param>
    /// <returns>Config merge result with properties to write and conflicts</returns>
    public ConfigMergeResult MergeForPull(
        Dictionary<string, (string Value, string Hash)> localProperties,
        ConfigData? remoteConfig,
        SyncState? syncState)
    {
        var result = new ConfigMergeResult();

        if (remoteConfig == null)
        {
            // No remote config - keep local unchanged
            foreach (var (path, (_, hash)) in localProperties)
            {
                result.NewHashes[path] = hash;
            }
            return result;
        }

        // Build remote lookup
        var remoteProperties = remoteConfig.Properties.ToDictionary(
            p => p.Key,
            p => (p.Value.Value, p.Value.Hash));

        // Track all property paths
        var allPaths = new HashSet<string>(localProperties.Keys);
        foreach (var path in remoteProperties.Keys) allPaths.Add(path);
        if (syncState != null)
        {
            foreach (var path in syncState.ConfigProperties.Keys) allPaths.Add(path);
        }

        foreach (var path in allPaths)
        {
            var baseHash = syncState?.ConfigProperties.GetValueOrDefault(path);
            var hasLocal = localProperties.TryGetValue(path, out var localProp);
            var hasRemote = remoteProperties.TryGetValue(path, out var remoteProp);

            var localHash = hasLocal ? localProp.Hash : null;
            var remoteHash = hasRemote ? remoteProp.Hash : null;

            // Three-way merge logic (same as entry merge)
            if (!hasLocal && !hasRemote)
            {
                // Both deleted - nothing to do
                continue;
            }

            if (!hasLocal && hasRemote)
            {
                // Only remote exists
                if (baseHash == null)
                {
                    // New from remote - accept
                    result.ToWrite[path] = remoteProp.Value;
                    result.NewHashes[path] = remoteProp.Hash;
                }
                else if (baseHash == remoteHash)
                {
                    // Remote unchanged, local deleted - keep deleted
                    // Don't add to ToWrite or NewHashes
                }
                else
                {
                    // CONFLICT: Deleted locally, modified remotely
                    result.Conflicts.Add(new ConfigConflict
                    {
                        Path = path,
                        Type = ConflictType.DeletedLocallyModifiedRemotely,
                        LocalValue = null,
                        RemoteValue = remoteProp.Value,
                        RemoteHash = remoteProp.Hash
                    });
                }
            }
            else if (hasLocal && !hasRemote)
            {
                // Only local exists
                if (baseHash == null)
                {
                    // New locally - keep
                    result.NewHashes[path] = localProp.Hash;
                }
                else if (baseHash == localHash)
                {
                    // Local unchanged, remote deleted - delete locally
                    // Don't add to NewHashes
                }
                else
                {
                    // CONFLICT: Deleted remotely, modified locally
                    result.Conflicts.Add(new ConfigConflict
                    {
                        Path = path,
                        Type = ConflictType.DeletedRemotelyModifiedLocally,
                        LocalValue = localProp.Value,
                        RemoteValue = null,
                        RemoteHash = null
                    });
                }
            }
            else
            {
                // Both exist
                if (localHash == remoteHash)
                {
                    // Same value
                    result.Unchanged++;
                    result.NewHashes[path] = localHash!;
                }
                else if (baseHash == localHash)
                {
                    // Only remote changed - accept
                    result.ToWrite[path] = remoteProp.Value;
                    result.NewHashes[path] = remoteHash!;
                    result.AutoMerged++;
                }
                else if (baseHash == remoteHash)
                {
                    // Only local changed - keep
                    result.NewHashes[path] = localHash!;
                }
                else
                {
                    // Both changed differently - conflict
                    result.Conflicts.Add(new ConfigConflict
                    {
                        Path = path,
                        Type = ConflictType.BothModified,
                        LocalValue = localProp.Value,
                        RemoteValue = remoteProp.Value,
                        RemoteHash = remoteHash
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts flattened properties from a JSON configuration with their hashes.
    /// </summary>
    /// <param name="configJson">lrm.json content</param>
    /// <returns>Dictionary of property path to (value JSON, hash)</returns>
    public Dictionary<string, (string Value, string Hash)> ExtractConfigProperties(string configJson)
    {
        var result = new Dictionary<string, (string Value, string Hash)>();

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            FlattenJsonElement(doc.RootElement, "", result);
        }
        catch (JsonException)
        {
            // Invalid JSON - return empty
        }

        return result;
    }

    /// <summary>
    /// Builds a complete lrm.json from remote ConfigData (for clone/first-pull).
    /// </summary>
    /// <param name="remoteConfig">Configuration data from server</param>
    /// <returns>Complete lrm.json content</returns>
    public string BuildConfigJson(ConfigData remoteConfig)
    {
        var config = new Dictionary<string, object>();

        foreach (var (path, prop) in remoteConfig.Properties)
        {
            // Use SetNestedProperty to handle both top-level and nested paths
            SetNestedProperty(config, path, prop.Value);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(config, options);
    }

    /// <summary>
    /// Applies config merge results to create updated lrm.json content.
    /// </summary>
    /// <param name="originalJson">Original lrm.json content</param>
    /// <param name="propertiesToUpdate">Properties to update (path -> new value JSON)</param>
    /// <returns>Updated lrm.json content</returns>
    public string ApplyConfigChanges(string originalJson, Dictionary<string, string> propertiesToUpdate)
    {
        if (propertiesToUpdate.Count == 0)
        {
            return originalJson;
        }

        try
        {
            using var doc = JsonDocument.Parse(originalJson);
            var options = new JsonSerializerOptions { WriteIndented = true };

            // Convert to mutable structure
            var root = JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson, options);
            if (root == null)
            {
                return originalJson;
            }

            // Apply updates - supports nested property paths like "Json.I18nextCompatible"
            foreach (var (path, valueJson) in propertiesToUpdate)
            {
                SetNestedProperty(root, path, valueJson);
            }

            return JsonSerializer.Serialize(root, options);
        }
        catch
        {
            return originalJson;
        }
    }

    /// <summary>
    /// Sets a property value at a nested path (e.g., "Json.I18nextCompatible").
    /// Creates intermediate objects if they don't exist.
    /// </summary>
    private static void SetNestedProperty(Dictionary<string, object> root, string path, string valueJson)
    {
        var parts = path.Split('.');
        var current = root;

        // Navigate/create intermediate objects
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (!current.TryGetValue(part, out var existing))
            {
                // Create new nested dictionary
                var nested = new Dictionary<string, object>();
                current[part] = nested;
                current = nested;
            }
            else if (existing is Dictionary<string, object> dict)
            {
                current = dict;
            }
            else if (existing is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                // Convert JsonElement to mutable dictionary
                var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText())
                    ?? new Dictionary<string, object>();
                current[part] = nested;
                current = nested;
            }
            else
            {
                // Can't navigate further - overwrite with new object
                var nested = new Dictionary<string, object>();
                current[part] = nested;
                current = nested;
            }
        }

        // Set the leaf value
        var leafKey = parts[^1];
        try
        {
            var value = JsonSerializer.Deserialize<object>(valueJson);
            current[leafKey] = value!;
        }
        catch
        {
            // Invalid JSON value, use as string
            current[leafKey] = valueJson;
        }
    }

    private void FlattenJsonElement(
        JsonElement element,
        string prefix,
        Dictionary<string, (string Value, string Hash)> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var path = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";

                    // For all values, store the serialized value (raw JSON text)
                    var valueJson = property.Value.GetRawText();
                    var hash = EntryHasher.ComputeConfigHash(valueJson);
                    result[path] = (valueJson, hash);
                }
                break;

            default:
                // Root is not an object - just store as-is
                if (!string.IsNullOrEmpty(prefix))
                {
                    var valueJson = element.GetRawText();
                    var hash = EntryHasher.ComputeConfigHash(valueJson);
                    result[prefix] = (valueJson, hash);
                }
                break;
        }
    }
}

/// <summary>
/// Result of config merge for pull.
/// </summary>
public class ConfigMergeResult
{
    /// <summary>
    /// Properties to write/update in local config.
    /// Key: property path, Value: JSON value
    /// </summary>
    public Dictionary<string, string> ToWrite { get; } = new();

    /// <summary>
    /// Configuration conflicts.
    /// </summary>
    public List<ConfigConflict> Conflicts { get; } = new();

    /// <summary>
    /// New hashes for sync state.
    /// </summary>
    public Dictionary<string, string> NewHashes { get; } = new();

    /// <summary>
    /// Number of properties auto-merged.
    /// </summary>
    public int AutoMerged { get; set; }

    /// <summary>
    /// Number of properties unchanged.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// Whether there are unresolved conflicts.
    /// </summary>
    public bool HasConflicts => Conflicts.Count > 0;
}
