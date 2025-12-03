// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Loads JSON localization resources from the file system.
/// </summary>
public class FileSystemResourceLoader : IResourceLoader
{
    private readonly string _resourcesPath;

    /// <summary>
    /// Creates a file system resource loader for the specified path.
    /// </summary>
    /// <param name="resourcesPath">Path to the directory containing JSON resource files.</param>
    public FileSystemResourceLoader(string resourcesPath)
    {
        _resourcesPath = Path.GetFullPath(resourcesPath);
    }

    /// <inheritdoc />
    public Stream? GetResourceStream(string baseName, string culture)
    {
        var fileName = GetFileName(baseName, culture);
        var path = Path.Combine(_resourcesPath, fileName);

        if (!File.Exists(path))
            return null;

        return File.OpenRead(path);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures(string baseName)
    {
        if (!Directory.Exists(_resourcesPath))
            yield break;

        var pattern = $"{baseName}*.json";
        var files = Directory.GetFiles(_resourcesPath, pattern);

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            if (fileName.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                // Default culture (no suffix)
                yield return "";
            }
            else if (fileName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase))
            {
                // Extract culture code from filename
                var culture = fileName.Substring(baseName.Length + 1);
                yield return culture;
            }
        }
    }

    /// <summary>
    /// Gets the filename for a resource with the specified culture.
    /// </summary>
    private static string GetFileName(string baseName, string culture)
    {
        return string.IsNullOrEmpty(culture)
            ? $"{baseName}.json"
            : $"{baseName}.{culture}.json";
    }
}
