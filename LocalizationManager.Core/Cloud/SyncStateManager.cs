// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.Json;
using LocalizationManager.Core.Cloud.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Manages sync state for tracking file changes between pushes.
/// State is stored in .lrm/sync-state.json (git-ignored).
/// </summary>
public static class SyncStateManager
{
    private const string StateDirectory = ".lrm";
    private const string StateFileName = "sync-state.json";

    /// <summary>
    /// Loads the sync state from .lrm/sync-state.json.
    /// </summary>
    /// <param name="projectDirectory">Project directory containing .lrm folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SyncState if file exists, null otherwise</returns>
    public static async Task<SyncState?> LoadAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var statePath = GetStatePath(projectDirectory);
        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(statePath, cancellationToken);
            return JsonSerializer.Deserialize<SyncState>(json);
        }
        catch (JsonException)
        {
            // Invalid state file, return null
            return null;
        }
    }

    /// <summary>
    /// Saves the sync state to .lrm/sync-state.json.
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

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(syncState, options);
        await File.WriteAllTextAsync(statePath, json, cancellationToken);
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

    private static string GetStatePath(string projectDirectory)
    {
        return Path.Combine(projectDirectory, StateDirectory, StateFileName);
    }
}
