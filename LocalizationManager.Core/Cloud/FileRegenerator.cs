// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Cloud.Models;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Regenerates local resource files from merged entries.
/// Uses backend writers for format-specific file generation.
/// </summary>
public class FileRegenerator
{
    private readonly IResourceBackend _backend;
    private readonly string _projectDirectory;

    public FileRegenerator(IResourceBackend backend, string projectDirectory)
    {
        _backend = backend;
        _projectDirectory = projectDirectory;
    }

    /// <summary>
    /// Regenerates all resource files from merged entries.
    /// Preserves existing file structure and entry order where possible.
    /// </summary>
    /// <param name="mergedEntries">Entries to write, grouped by language</param>
    /// <param name="existingLanguages">Existing language files for preserving order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of regeneration</returns>
    public async Task<RegenerationResult> RegenerateFilesAsync(
        IEnumerable<MergedEntry> mergedEntries,
        IEnumerable<LanguageInfo> existingLanguages,
        CancellationToken cancellationToken = default)
    {
        var result = new RegenerationResult();

        // Group entries by language
        var entriesByLang = mergedEntries
            .GroupBy(e => e.Lang)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build lookup for existing languages
        var existingLangFiles = existingLanguages.ToDictionary(
            l => l.Code,
            l => l);

        // Use temp directory for atomic writes
        var tempDir = SyncStateManager.GetTempDirectory(_projectDirectory);

        try
        {
            foreach (var (lang, entries) in entriesByLang)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (existingLangFiles.TryGetValue(lang, out var existingLang))
                {
                    // Update existing file
                    await UpdateExistingFileAsync(
                        existingLang,
                        entries,
                        tempDir,
                        result,
                        cancellationToken);
                }
                else
                {
                    // Create new language file
                    await CreateNewLanguageFileAsync(
                        lang,
                        entries,
                        tempDir,
                        result,
                        cancellationToken);
                }
            }

            // Atomic move from temp to final location
            await MoveFilesFromTempAsync(tempDir, result, cancellationToken);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        finally
        {
            // Cleanup temp directory
            SyncStateManager.CleanupTempDirectory(_projectDirectory);
        }

        return result;
    }

    /// <summary>
    /// Updates an existing resource file with merged entries.
    /// Preserves entry order for existing keys, appends new keys.
    /// </summary>
    private async Task UpdateExistingFileAsync(
        LanguageInfo existingLang,
        List<MergedEntry> entries,
        string tempDir,
        RegenerationResult result,
        CancellationToken cancellationToken)
    {
        // Read existing file
        var resourceFile = await _backend.Reader.ReadAsync(existingLang, cancellationToken);

        // Build lookup of merged entries
        var mergedByKey = entries.ToDictionary(e => e.Key, e => e);

        // Track which keys we've processed
        var processedKeys = new HashSet<string>();

        // Update existing entries (preserves order)
        foreach (var existing in resourceFile.Entries.ToList())
        {
            if (mergedByKey.TryGetValue(existing.Key, out var merged))
            {
                // Update existing entry
                if (merged.IsPlural && merged.PluralForms != null)
                {
                    existing.Value = merged.Value;
                    existing.PluralForms = merged.PluralForms;
                    existing.IsPlural = true;
                }
                else
                {
                    existing.Value = merged.Value;
                    existing.PluralForms = null;
                    existing.IsPlural = false;
                }

                if (merged.Comment != null)
                {
                    existing.Comment = merged.Comment;
                }

                processedKeys.Add(existing.Key);
            }
            // If key not in merged, keep existing (not overwritten by remote)
        }

        // Add new entries (alphabetically sorted among new)
        var newEntries = entries
            .Where(e => !processedKeys.Contains(e.Key))
            .OrderBy(e => e.Key)
            .ToList();

        foreach (var newEntry in newEntries)
        {
            var entry = new ResourceEntry
            {
                Key = newEntry.Key,
                Value = newEntry.Value,
                Comment = newEntry.Comment
            };

            if (newEntry.IsPlural && newEntry.PluralForms != null)
            {
                entry.IsPlural = true;
                entry.PluralForms = newEntry.PluralForms;
            }

            resourceFile.Entries.Add(entry);
        }

        // Write to temp file - update the language file path temporarily
        var originalPath = resourceFile.Language.FilePath;

        // Preserve relative path structure for formats like Android (values/strings.xml, values-fr/strings.xml)
        // and iOS (en.lproj/Localizable.strings, fr.lproj/Localizable.strings)
        var relativePath = GetRelativeFilePath(existingLang.FilePath, _projectDirectory);
        var tempPath = Path.Combine(tempDir, relativePath);

        // Ensure temp subdirectory exists
        var tempSubDir = Path.GetDirectoryName(tempPath);
        if (tempSubDir != null && !Directory.Exists(tempSubDir))
        {
            Directory.CreateDirectory(tempSubDir);
        }

        resourceFile.Language.FilePath = tempPath;

        await _backend.Writer.WriteAsync(resourceFile, cancellationToken);

        // Restore original path for tracking
        resourceFile.Language.FilePath = originalPath;

        result.FilesToMove.Add((tempPath, existingLang.FilePath!));
        result.UpdatedFiles++;
    }

    /// <summary>
    /// Creates a new language file from merged entries.
    /// </summary>
    private async Task CreateNewLanguageFileAsync(
        string lang,
        List<MergedEntry> entries,
        string tempDir,
        RegenerationResult result,
        CancellationToken cancellationToken)
    {
        // Determine file path for new language
        var filePath = GetNewLanguageFilePath(lang);

        // Create language info for new file
        var languageInfo = new LanguageInfo
        {
            Code = lang,
            Name = lang,
            FilePath = filePath,
            IsDefault = false
        };

        // Create new resource file
        var resourceFile = new ResourceFile
        {
            Language = languageInfo
        };

        // Add entries alphabetically
        foreach (var entry in entries.OrderBy(e => e.Key))
        {
            var resourceEntry = new ResourceEntry
            {
                Key = entry.Key,
                Value = entry.Value,
                Comment = entry.Comment
            };

            if (entry.IsPlural && entry.PluralForms != null)
            {
                resourceEntry.IsPlural = true;
                resourceEntry.PluralForms = entry.PluralForms;
            }

            resourceFile.Entries.Add(resourceEntry);
        }

        // Write to temp file - preserve relative path structure
        var relativePath = GetRelativeFilePath(filePath, _projectDirectory);
        var tempPath = Path.Combine(tempDir, relativePath);

        // Ensure temp subdirectory exists
        var tempSubDir = Path.GetDirectoryName(tempPath);
        if (tempSubDir != null && !Directory.Exists(tempSubDir))
        {
            Directory.CreateDirectory(tempSubDir);
        }

        resourceFile.Language.FilePath = tempPath;

        await _backend.Writer.WriteAsync(resourceFile, cancellationToken);

        // Update to final path for tracking
        resourceFile.Language.FilePath = filePath;

        result.FilesToMove.Add((tempPath, filePath));
        result.CreatedFiles++;
    }

    /// <summary>
    /// Moves all temp files to their final locations atomically.
    /// Also moves any companion files that backends may have created (e.g., .stringsdict for iOS).
    /// </summary>
    private async Task MoveFilesFromTempAsync(
        string tempDir,
        RegenerationResult result,
        CancellationToken cancellationToken)
    {
        // Move all files from temp directory to project directory
        // This handles companion files like .stringsdict that backends create
        if (Directory.Exists(tempDir))
        {
            foreach (var tempFilePath in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate the final path by replacing the temp directory with the project directory
                var relativePath = Path.GetRelativePath(tempDir, tempFilePath);
                var finalPath = Path.Combine(_projectDirectory, relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(finalPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Atomic move (rename)
                File.Move(tempFilePath, finalPath, overwrite: true);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the relative path of a file from the project directory.
    /// Handles null paths by returning a fallback filename.
    /// </summary>
    private string GetRelativeFilePath(string? filePath, string baseDirectory)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return "unknown.txt";
        }

        // If the file path is under the base directory, extract the relative part
        if (filePath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = filePath.Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relativePath;
        }

        // Otherwise just use the filename
        return Path.GetFileName(filePath);
    }

    /// <summary>
    /// Gets the file path for a new language file based on backend conventions.
    /// </summary>
    private string GetNewLanguageFilePath(string lang)
    {
        // Get path convention from backend
        var backendName = _backend.Name.ToLowerInvariant();
        var isDefaultLang = string.IsNullOrEmpty(lang);

        return backendName switch
        {
            "resx" => isDefaultLang
                ? Path.Combine(_projectDirectory, "Resources.resx")
                : Path.Combine(_projectDirectory, $"Resources.{lang}.resx"),
            "json" or "jsonlocalization" => isDefaultLang
                ? Path.Combine(_projectDirectory, "strings.json")
                : Path.Combine(_projectDirectory, $"strings.{lang}.json"),
            "android" => isDefaultLang
                ? Path.Combine(_projectDirectory, "values", "strings.xml")
                : Path.Combine(_projectDirectory, $"values-{lang}", "strings.xml"),
            "ios" or "strings" => isDefaultLang
                ? Path.Combine(_projectDirectory, "en.lproj", "Localizable.strings")
                : Path.Combine(_projectDirectory, $"{lang}.lproj", "Localizable.strings"),
            "i18next" => isDefaultLang
                ? Path.Combine(_projectDirectory, "en.json")
                : Path.Combine(_projectDirectory, $"{lang}.json"),
            _ => isDefaultLang
                ? Path.Combine(_projectDirectory, $"strings.{backendName}")
                : Path.Combine(_projectDirectory, $"strings.{lang}.{backendName}")
        };
    }

    /// <summary>
    /// Deletes entries from local files that were deleted on remote.
    /// </summary>
    /// <param name="deletedKeys">Keys to delete, grouped by language</param>
    /// <param name="existingLanguages">Existing language files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries deleted</returns>
    public async Task<int> DeleteEntriesAsync(
        IEnumerable<(string Key, string Lang)> deletedKeys,
        IEnumerable<LanguageInfo> existingLanguages,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;

        // Group deletions by language
        var deletionsByLang = deletedKeys
            .GroupBy(d => d.Lang)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Key).ToHashSet());

        var langFiles = existingLanguages.ToDictionary(l => l.Code, l => l);

        foreach (var (lang, keysToDelete) in deletionsByLang)
        {
            if (!langFiles.TryGetValue(lang, out var langFile))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var resourceFile = await _backend.Reader.ReadAsync(langFile, cancellationToken);
            var originalCount = resourceFile.Entries.Count;

            resourceFile.Entries.RemoveAll(e => keysToDelete.Contains(e.Key));

            if (resourceFile.Entries.Count < originalCount)
            {
                if (langFile.FilePath != null)
                {
                    await _backend.Writer.WriteAsync(resourceFile, cancellationToken);
                }
                deletedCount += originalCount - resourceFile.Entries.Count;
            }
        }

        return deletedCount;
    }
}

/// <summary>
/// Result of file regeneration.
/// </summary>
public class RegenerationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int UpdatedFiles { get; set; }
    public int CreatedFiles { get; set; }
    public List<(string TempPath, string FinalPath)> FilesToMove { get; } = new();
}
