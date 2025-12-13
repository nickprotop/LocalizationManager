using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for syncing resources between CLI and server using Core's backends.
/// </summary>
public class ResourceSyncService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ResourceSyncService> _logger;
    private readonly IStorageService _storageService;

    public ResourceSyncService(
        AppDbContext db,
        ILogger<ResourceSyncService> logger,
        IStorageService storageService)
    {
        _db = db;
        _logger = logger;
        _storageService = storageService;
    }

    /// <summary>
    /// Stores uploaded files to S3/Minio for historical archive.
    /// </summary>
    public async Task StoreUploadedFilesAsync(int projectId, List<FileDto> files)
    {
        if (files.Count == 0)
            return;

        var uploadTimestamp = DateTime.UtcNow;
        var uploadPath = $"uploads/{uploadTimestamp:yyyy-MM-dd-HH-mm-ss}";

        foreach (var file in files)
        {
            var s3FilePath = $"{uploadPath}/{file.Path}";
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(file.Content);

            using var stream = new MemoryStream(contentBytes);
            await _storageService.UploadFileAsync(projectId, s3FilePath, stream, "text/plain");
        }

        _logger.LogInformation("Stored {Count} files to {Path}", files.Count, uploadPath);
    }

    /// <summary>
    /// Creates a resource backend from configuration.
    /// Falls back to project format from database if not specified in config.
    /// </summary>
    private IResourceBackend CreateBackendFromConfig(ConfigurationModel config, string projectFormat)
    {
        var factory = new ResourceBackendFactory();
        var backendName = config.ResourceFormat ?? projectFormat;  // Use project format as fallback

        return factory.GetBackend(backendName);
    }

    /// <summary>
    /// Extracts language code from file path using configuration.
    /// </summary>
    private string ExtractLanguageCodeFromPath(string filePath, ConfigurationModel config, string effectiveFormat)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // RESX format: Resources.el.resx → "el", Resources.resx → default language
        if (effectiveFormat == "resx")
        {
            var parts = fileName.Split('.');
            if (parts.Length > 1)
            {
                // Has language code: Resources.el
                return parts[^1];
            }
            // No language code: Resources → default language
            return config.DefaultLanguageCode ?? "en";
        }

        // JSON i18next format: el.json → "el"
        if (config.Json?.I18nextCompatible == true)
        {
            return fileName;
        }

        // JSON standard format: strings.el.json → "el", strings.json → default language
        var jsonParts = fileName.Split('.');
        if (jsonParts.Length > 1)
        {
            return jsonParts[^1];
        }

        return config.DefaultLanguageCode ?? "en";
    }

    /// <summary>
    /// Parses files using Core's backends and saves to database.
    /// Uses stream-based reading to avoid temp folder security risks.
    /// </summary>
    public async Task ParseFilesToDatabaseAsync(
        int projectId,
        List<FileDto> files,
        string? configJson,
        string projectFormat)
    {
        if (files.Count == 0)
            return;

        // Deserialize configuration or use defaults
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = configJson != null
            ? JsonSerializer.Deserialize<ConfigurationModel>(configJson, jsonOptions) ?? new ConfigurationModel()
            : new ConfigurationModel();

        // Determine effective format: config takes priority, then project format from DB
        var effectiveFormat = config.ResourceFormat ?? projectFormat;

        // Create resource backend
        var backend = CreateBackendFromConfig(config, projectFormat);

        foreach (var fileDto in files)
        {
            // Extract language code from filename
            var langCode = ExtractLanguageCodeFromPath(fileDto.Path, config, effectiveFormat);

            // Create LanguageInfo metadata (no FilePath needed for stream-based reading)
            var langInfo = new LanguageInfo
            {
                BaseName = config.Json?.BaseName ?? "strings",
                Code = langCode,
                Name = langCode,  // Use language code as display name
                IsDefault = langCode == config.DefaultLanguageCode
            };

            // Use Core's backend to parse content directly from string (no temp file)
            using var reader = new StringReader(fileDto.Content);
            var resourceFile = backend.Reader.Read(reader, langInfo);

            // Save to database
            await SaveResourceFileToDatabaseAsync(projectId, langCode, resourceFile.Entries);
        }
    }

    /// <summary>
    /// Saves resource entries to database for a specific language.
    /// </summary>
    private async Task SaveResourceFileToDatabaseAsync(
        int projectId,
        string languageCode,
        List<ResourceEntry> entries)
    {
        foreach (var entry in entries)
        {
            // Find or create resource key
            var resourceKey = await _db.ResourceKeys
                .FirstOrDefaultAsync(k => k.ProjectId == projectId && k.KeyName == entry.Key);

            if (resourceKey == null)
            {
                resourceKey = new ResourceKey
                {
                    ProjectId = projectId,
                    KeyName = entry.Key,
                    IsPlural = entry.IsPlural,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ResourceKeys.Add(resourceKey);
                await _db.SaveChangesAsync(); // Save to get ID
            }

            if (entry.IsPlural && entry.PluralForms != null)
            {
                // Handle plural forms
                foreach (var (pluralForm, value) in entry.PluralForms)
                {
                    await UpsertTranslationAsync(resourceKey.Id, languageCode, value, entry.Comment, pluralForm);
                }
            }
            else
            {
                // Handle regular translation
                await UpsertTranslationAsync(resourceKey.Id, languageCode, entry.Value, entry.Comment, "");
            }
        }
    }

    /// <summary>
    /// Inserts or updates a translation.
    /// </summary>
    private async Task UpsertTranslationAsync(
        int resourceKeyId,
        string languageCode,
        string? value,
        string? comment,
        string pluralForm)
    {
        var translation = await _db.Translations
            .FirstOrDefaultAsync(t =>
                t.ResourceKeyId == resourceKeyId &&
                t.LanguageCode == languageCode &&
                t.PluralForm == pluralForm);

        if (translation == null)
        {
            translation = new Shared.Entities.Translation
            {
                ResourceKeyId = resourceKeyId,
                LanguageCode = languageCode,
                Value = value,
                Comment = comment,
                PluralForm = pluralForm,
                Status = string.IsNullOrWhiteSpace(value) ? "pending" : "translated",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Translations.Add(translation);
        }
        else
        {
            translation.Value = value;
            translation.Comment = comment;
            translation.Status = string.IsNullOrWhiteSpace(value) ? "pending" : "translated";
            translation.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Generates files from database using Core's backends.
    /// </summary>
    public async Task<List<FileDto>> GenerateFilesFromDatabaseAsync(int projectId, string? configJson, string projectFormat)
    {
        // Deserialize configuration or use defaults
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = configJson != null
            ? JsonSerializer.Deserialize<ConfigurationModel>(configJson, jsonOptions) ?? new ConfigurationModel()
            : new ConfigurationModel();

        // Determine effective format: config takes priority, then project format from DB
        var effectiveFormat = config.ResourceFormat ?? projectFormat;

        // Create backend
        var backend = CreateBackendFromConfig(config, projectFormat);

        // Load all resource keys with translations
        var keys = await _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId)
            .ToListAsync();

        // Group translations by language
        var languageGroups = keys
            .SelectMany(k => k.Translations)
            .GroupBy(t => t.LanguageCode);

        var files = new List<FileDto>();

        foreach (var langGroup in languageGroups)
        {
            var langCode = langGroup.Key;

            // Build ResourceFile from database
            var entries = new List<ResourceEntry>();

            foreach (var key in keys)
            {
                var translations = key.Translations
                    .Where(t => t.LanguageCode == langCode)
                    .ToList();

                if (translations.Count == 0)
                    continue;

                if (key.IsPlural)
                {
                    // Build plural entry
                    var pluralForms = translations.ToDictionary(
                        t => t.PluralForm,
                        t => t.Value ?? ""
                    );

                    entries.Add(new ResourceEntry
                    {
                        Key = key.KeyName,
                        IsPlural = true,
                        PluralForms = pluralForms,
                        Comment = translations.FirstOrDefault()?.Comment
                    });
                }
                else
                {
                    // Build regular entry
                    var translation = translations.First();
                    entries.Add(new ResourceEntry
                    {
                        Key = key.KeyName,
                        Value = translation.Value,
                        Comment = translation.Comment,
                        IsPlural = false
                    });
                }
            }

            // Use Core's backend to write file
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create ResourceFile with FilePath set for Writer
                var resourceFile = new ResourceFile
                {
                    Language = new LanguageInfo
                    {
                        BaseName = config.Json?.BaseName ?? "strings",
                        Code = langCode,
                        Name = langCode,  // Use language code as display name
                        IsDefault = langCode == config.DefaultLanguageCode,
                        FilePath = tempFile  // Writer uses this path
                    },
                    Entries = entries
                };

                // Write to temp file
                backend.Writer.Write(resourceFile);

                // Read generated file content
                var content = await File.ReadAllTextAsync(tempFile);

                // Get proper filename from backend
                var filename = GetFileNameForLanguage(resourceFile.Language, config, effectiveFormat);

                files.Add(new FileDto
                {
                    Path = filename,
                    Content = content
                });
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Gets the filename for a language based on configuration.
    /// </summary>
    private string GetFileNameForLanguage(LanguageInfo language, ConfigurationModel config, string effectiveFormat)
    {
        if (effectiveFormat == "resx")
        {
            // RESX: Resources.el.resx or Resources.resx (default)
            if (language.IsDefault)
            {
                return $"{language.BaseName}.resx";
            }
            return $"{language.BaseName}.{language.Code}.resx";
        }

        // JSON
        if (config.Json?.I18nextCompatible == true)
        {
            // i18next: el.json
            return $"{language.Code}.json";
        }

        // JSON standard: strings.el.json or strings.json (default)
        if (language.IsDefault)
        {
            return $"{language.BaseName}.json";
        }
        return $"{language.BaseName}.{language.Code}.json";
    }

    /// <summary>
    /// Deletes all translations for a specific language.
    /// </summary>
    public async Task DeleteLanguageTranslationsAsync(int projectId, string languageCode)
    {
        var translations = await _db.Translations
            .Include(t => t.ResourceKey)
            .Where(t => t.ResourceKey != null && t.ResourceKey.ProjectId == projectId && t.LanguageCode == languageCode)
            .ToListAsync();

        _db.Translations.RemoveRange(translations);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted {Count} translations for language {Language} in project {ProjectId}",
            translations.Count, languageCode, projectId);
    }
}
