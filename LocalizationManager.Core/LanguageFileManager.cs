// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core;

/// <summary>
/// Manages CRUD operations for language resource files.
/// </summary>
public class LanguageFileManager
{
    private IResourceBackend? _backend;

    public LanguageFileManager()
    {
    }

    /// <summary>
    /// Sets the backend to use for file operations.
    /// </summary>
    public void SetBackend(IResourceBackend backend)
    {
        _backend = backend;
    }

    private IResourceBackend GetBackend()
    {
        if (_backend == null)
        {
            // Default to RESX backend for backwards compatibility
            _backend = new Backends.Resx.ResxResourceBackend();
        }
        return _backend;
    }

    private string GetFileExtension()
    {
        return GetBackend().SupportedExtensions.FirstOrDefault() ?? ".resx";
    }

    /// <summary>
    /// Creates a new language resource file.
    /// </summary>
    /// <param name="baseName">Base name of the resource file (e.g., "Resources")</param>
    /// <param name="cultureCode">Culture code (e.g., "fr", "fr-FR")</param>
    /// <param name="targetPath">Directory path where file will be created</param>
    /// <param name="sourceFile">Source file to copy entries from (optional)</param>
    /// <param name="copyEntries">Whether to copy entries from source file</param>
    /// <returns>The created ResourceFile</returns>
    public ResourceFile CreateLanguageFile(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true)
    {
        // Validate culture code
        if (!IsValidCultureCode(cultureCode, out var culture))
        {
            throw new ArgumentException($"Invalid culture code: {cultureCode}", nameof(cultureCode));
        }

        // Build target file path
        var extension = GetFileExtension();
        var fileName = $"{baseName}.{cultureCode}{extension}";
        var filePath = Path.Combine(targetPath, fileName);

        // Check if file already exists
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"Language file already exists: {fileName}");
        }

        // Create resource file with proper structure
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = culture!.DisplayName,
                IsDefault = false,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>()
        };

        // Copy entries from source if specified
        if (copyEntries && sourceFile != null)
        {
            foreach (var entry in sourceFile.Entries)
            {
                resourceFile.Entries.Add(new ResourceEntry
                {
                    Key = entry.Key,
                    Value = entry.Value,
                    Comment = entry.Comment
                });
            }
        }

        // Write to disk
        GetBackend().Writer.Write(resourceFile);

        return resourceFile;
    }

    /// <summary>
    /// Deletes a language resource file.
    /// </summary>
    /// <param name="languageInfo">Language information for the file to delete</param>
    public void DeleteLanguageFile(LanguageInfo languageInfo)
    {
        if (!File.Exists(languageInfo.FilePath))
        {
            throw new FileNotFoundException($"Language file not found: {Path.GetFileName(languageInfo.FilePath)}");
        }

        // Prevent deletion of default language
        if (languageInfo.IsDefault)
        {
            throw new InvalidOperationException(
                "Cannot delete default language file. Default language files serve as fallback for all languages.");
        }

        File.Delete(languageInfo.FilePath);
    }

    /// <summary>
    /// Validates a culture code.
    /// </summary>
    /// <param name="code">Culture code to validate</param>
    /// <param name="culture">Output parameter for the CultureInfo if valid</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValidCultureCode(string code, out CultureInfo? culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(code);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the display name for a culture code.
    /// </summary>
    /// <param name="code">Culture code</param>
    /// <returns>Display name or the code itself if invalid</returns>
    public string GetCultureDisplayName(string code)
    {
        if (IsValidCultureCode(code, out var culture))
        {
            return culture!.DisplayName;
        }
        return code;
    }

    /// <summary>
    /// Checks if a language file already exists.
    /// </summary>
    /// <param name="baseName">Base name of the resource file</param>
    /// <param name="cultureCode">Culture code</param>
    /// <param name="path">Directory path</param>
    /// <returns>True if file exists, false otherwise</returns>
    public bool LanguageFileExists(string baseName, string cultureCode, string path)
    {
        var extension = GetFileExtension();
        var fileName = $"{baseName}.{cultureCode}{extension}";
        var filePath = Path.Combine(path, fileName);
        return File.Exists(filePath);
    }
}
