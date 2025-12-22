// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.Core.Cloud.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Result of loading sync state.
/// </summary>
public class SyncStateLoadResult
{
    /// <summary>
    /// The loaded sync state, or null if not found.
    /// </summary>
    public SyncState? State { get; init; }

    /// <summary>
    /// Whether the state file was corrupted.
    /// </summary>
    public bool WasCorrupted { get; init; }

    /// <summary>
    /// Whether the state needs migration from v1 to v2.
    /// </summary>
    public bool NeedsMigration { get; init; }
}

/// <summary>
/// Manages sync state for key-level synchronization.
/// State is stored in .lrm/sync-state.json (git-ignored).
/// </summary>
public static class SyncStateManager
{
    private const string StateDirectory = ".lrm";
    private const string StateFileName = "sync-state.json";
    private const string TempDirectory = ".lrm/temp";

    /// <summary>
    /// Loads the sync state from .lrm/sync-state.json.
    /// </summary>
    /// <param name="projectDirectory">Project directory containing .lrm folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SyncStateLoadResult with state and status flags</returns>
    public static async Task<SyncStateLoadResult> LoadAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var statePath = GetStatePath(projectDirectory);
        if (!File.Exists(statePath))
        {
            return new SyncStateLoadResult { State = null, WasCorrupted = false, NeedsMigration = false };
        }

        try
        {
            var json = await File.ReadAllTextAsync(statePath, cancellationToken);
            var state = JsonSerializer.Deserialize<SyncState>(json);

            if (state == null)
            {
                return new SyncStateLoadResult { State = null, WasCorrupted = true, NeedsMigration = false };
            }

            var needsMigration = state.NeedsMigration;

            return new SyncStateLoadResult
            {
                State = state,
                WasCorrupted = false,
                NeedsMigration = needsMigration
            };
        }
        catch (JsonException)
        {
            // Corrupted state file
            return new SyncStateLoadResult { State = null, WasCorrupted = true, NeedsMigration = false };
        }
        catch (IOException)
        {
            // File read error, treat as corrupted
            return new SyncStateLoadResult { State = null, WasCorrupted = true, NeedsMigration = false };
        }
    }

    /// <summary>
    /// Loads the sync state, returning null if not found (simple version).
    /// </summary>
    /// <param name="projectDirectory">Project directory containing .lrm folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SyncState if file exists and is valid, null otherwise</returns>
    public static async Task<SyncState?> LoadSimpleAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var result = await LoadAsync(projectDirectory, cancellationToken);
        return result.State;
    }

    /// <summary>
    /// Saves the sync state to .lrm/sync-state.json atomically.
    /// Uses temp file + rename for atomic write.
    /// </summary>
    /// <param name="projectDirectory">Project directory containing .lrm folder</param>
    /// <param name="syncState">Sync state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task SaveAsync(
        string projectDirectory,
        SyncState syncState,
        CancellationToken cancellationToken = default)
    {
        var statePath = GetStatePath(projectDirectory);
        var directory = Path.GetDirectoryName(statePath);

        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ensure version is set
        if (syncState.Version == 0)
        {
            syncState.Version = 2;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(syncState, options);

        // Write to temp file first, then rename for atomic operation
        var tempPath = statePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        // Atomic rename (works on both Linux and Windows)
        File.Move(tempPath, statePath, overwrite: true);
    }

    /// <summary>
    /// Clears the sync state by deleting .lrm/sync-state.json.
    /// </summary>
    /// <param name="projectDirectory">Project directory containing .lrm folder</param>
    public static void Clear(string projectDirectory)
    {
        var statePath = GetStatePath(projectDirectory);
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }
    }

    /// <summary>
    /// Creates or gets a new sync state for the project.
    /// </summary>
    /// <param name="projectDirectory">Project directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Existing or new sync state</returns>
    public static async Task<SyncState> GetOrCreateAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var result = await LoadAsync(projectDirectory, cancellationToken);

        if (result.State != null && !result.NeedsMigration)
        {
            return result.State;
        }

        // Create new v2 state
        return SyncState.CreateNew();
    }

    /// <summary>
    /// Updates entry hashes in the sync state from a dictionary.
    /// </summary>
    /// <param name="syncState">Sync state to update</param>
    /// <param name="entryHashes">Dictionary of key -> { lang -> hash }</param>
    public static void UpdateEntryHashes(SyncState syncState, Dictionary<string, Dictionary<string, string>> entryHashes)
    {
        foreach (var (key, langHashes) in entryHashes)
        {
            foreach (var (lang, hash) in langHashes)
            {
                syncState.SetEntryHash(key, lang, hash);
            }
        }
        syncState.Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates config property hashes in the sync state.
    /// </summary>
    /// <param name="syncState">Sync state to update</param>
    /// <param name="configHashes">Dictionary of property path -> hash</param>
    public static void UpdateConfigHashes(SyncState syncState, Dictionary<string, string> configHashes)
    {
        foreach (var (path, hash) in configHashes)
        {
            syncState.ConfigProperties[path] = hash;
        }
        syncState.Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if sync state exists for the project.
    /// </summary>
    /// <param name="projectDirectory">Project directory</param>
    /// <returns>True if sync state file exists</returns>
    public static bool Exists(string projectDirectory)
    {
        return File.Exists(GetStatePath(projectDirectory));
    }

    /// <summary>
    /// Gets the path to the temp directory for atomic file operations.
    /// </summary>
    /// <param name="projectDirectory">Project directory</param>
    /// <returns>Path to temp directory</returns>
    public static string GetTempDirectory(string projectDirectory)
    {
        var tempDir = Path.Combine(projectDirectory, TempDirectory);
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }
        return tempDir;
    }

    /// <summary>
    /// Cleans up the temp directory.
    /// </summary>
    /// <param name="projectDirectory">Project directory</param>
    public static void CleanupTempDirectory(string projectDirectory)
    {
        var tempDir = Path.Combine(projectDirectory, TempDirectory);
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
        }
    }

    private static string GetStatePath(string projectDirectory)
    {
        return Path.Combine(projectDirectory, StateDirectory, StateFileName);
    }
}
