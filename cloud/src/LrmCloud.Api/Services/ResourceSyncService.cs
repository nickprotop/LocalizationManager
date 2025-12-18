using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for syncing resources between CLI and server using Core's backends.
/// </summary>
public class ResourceSyncService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ResourceSyncService> _logger;
    private readonly IStorageService _storageService;
    private readonly LimitsConfiguration _limits;

    /// <summary>
    /// Allowed file extensions for upload.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".resx", ".json"
    };

    public ResourceSyncService(
        AppDbContext db,
        ILogger<ResourceSyncService> logger,
        IStorageService storageService,
        CloudConfiguration config)
    {
        _db = db;
        _logger = logger;
        _storageService = storageService;
        _limits = config.Limits;
    }

    /// <summary>
    /// Validates a file for security and format compliance.
    /// </summary>
    /// <param name="file">File to validate.</param>
    /// <param name="plan">User's plan for size limits.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    private void ValidateFile(FileDto file, string plan)
    {
        // Check file path is not empty
        if (string.IsNullOrWhiteSpace(file.Path))
        {
            throw new ArgumentException("File path cannot be empty");
        }

        // Check file extension
        var extension = Path.GetExtension(file.Path);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                $"File type '{extension}' is not allowed. Only .resx and .json files are supported.");
        }

        // Check file size against plan-based limit
        var maxFileSizeBytes = _limits.GetMaxFileSizeBytes(plan);
        if (file.Content.Length > maxFileSizeBytes)
        {
            var maxMB = maxFileSizeBytes / 1024 / 1024;
            throw new ArgumentException(
                $"File '{file.Path}' exceeds maximum size of {maxMB}MB for your plan.");
        }

        // Validate content matches the file type
        if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
        {
            ValidateResxContent(file.Content, file.Path);
        }
        else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            ValidateJsonContent(file.Content, file.Path);
        }
    }

    /// <summary>
    /// Validates RESX content for security (prevents XXE attacks).
    /// </summary>
    private static void ValidateResxContent(string content, string filePath)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,  // Block DTD (prevents XXE)
                XmlResolver = null,                       // No external resources
                MaxCharactersFromEntities = 0,            // Block entity expansion
                MaxCharactersInDocument = 10_000_000      // 10MB limit
            };

            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            // Read through the entire document to validate
            while (xmlReader.Read()) { }
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"Invalid XML in file '{filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Validates JSON content.
    /// </summary>
    private static void ValidateJsonContent(string content, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON in file '{filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Stores uploaded files to S3/Minio for historical archive.
    /// Also maintains a "current/" folder with the latest version of all files for snapshots.
    /// </summary>
    /// <param name="projectId">Project ID.</param>
    /// <param name="files">Files to upload.</param>
    /// <param name="userId">User ID for plan-based limits.</param>
    public async Task StoreUploadedFilesAsync(int projectId, List<FileDto> files, int userId)
    {
        if (files.Count == 0)
            return;

        // Get user's plan for limit checks
        var user = await _db.Users.FindAsync(userId);
        var plan = user?.Plan ?? "free";

        // Calculate total size of new files
        var newFilesSize = files.Sum(f => (long)System.Text.Encoding.UTF8.GetByteCount(f.Content));

        // Get user's current storage usage
        var userProjectIds = await _db.Projects
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();
        var currentStorageUsed = await _storageService.GetTotalStorageSizeAsync(userProjectIds);

        // Check storage limit
        var maxStorageBytes = _limits.GetMaxStorageBytes(plan);
        if (currentStorageUsed + newFilesSize > maxStorageBytes)
        {
            var usedMB = currentStorageUsed / 1024.0 / 1024.0;
            var limitMB = maxStorageBytes / 1024.0 / 1024.0;
            throw new InvalidOperationException(
                $"Storage limit exceeded. Used: {usedMB:F1}MB, Limit: {limitMB:F0}MB. " +
                "Delete files/snapshots or upgrade your plan.");
        }

        // Validate all files before processing (including per-file size limit)
        foreach (var file in files)
        {
            ValidateFile(file, plan);
        }

        var uploadTimestamp = DateTime.UtcNow;
        var uploadPath = $"uploads/{uploadTimestamp:yyyy-MM-dd-HH-mm-ss}";

        foreach (var file in files)
        {
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(file.Content);

            // Store in timestamped upload folder (historical archive)
            var s3FilePath = $"{uploadPath}/{file.Path}";
            using (var stream = new MemoryStream(contentBytes))
            {
                await _storageService.UploadFileAsync(projectId, s3FilePath, stream, "text/plain");
            }

            // Also update the "current/" folder for snapshots
            var currentFilePath = $"current/{file.Path}";
            using (var stream = new MemoryStream(contentBytes))
            {
                await _storageService.UploadFileAsync(projectId, currentFilePath, stream, "text/plain");
            }
        }

        _logger.LogInformation("Stored {Count} files to {Path} and current/", files.Count, uploadPath);
    }

    /// <summary>
    /// Creates a resource backend from configuration.
    /// Falls back to project format from database if not specified in config.
    /// Maps "i18next" to "json" backend since i18next is a JSON variant.
    /// </summary>
    private IResourceBackend CreateBackendFromConfig(ConfigurationModel config, string projectFormat)
    {
        var factory = new ResourceBackendFactory();
        var backendName = config.ResourceFormat ?? projectFormat;  // Use project format as fallback

        // Map i18next to json backend (i18next is a JSON variant with different naming convention)
        if (backendName.Equals("i18next", StringComparison.OrdinalIgnoreCase))
        {
            backendName = "json";
        }

        return factory.GetBackend(backendName);
    }

    /// <summary>
    /// Extracts language code from file path using configuration and smart detection.
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

        // JSON: Explicit i18next mode via project format or config
        if (effectiveFormat == "i18next" || config.Json?.I18nextCompatible == true)
        {
            return fileName;
        }

        // JSON: Explicit standard format via config with baseName
        if (config.Json?.I18nextCompatible == false && !string.IsNullOrEmpty(config.Json?.BaseName))
        {
            var jsonParts = fileName.Split('.');
            if (jsonParts.Length > 1 && jsonParts[0] == config.Json.BaseName)
                return jsonParts[^1];
            if (jsonParts[0] == config.Json.BaseName)
                return config.DefaultLanguageCode ?? "en";
        }

        // JSON: Auto-detect from filename
        // 1. If filename IS a valid culture code → it's the language (en.json → "en")
        if (IsValidCultureCode(fileName))
        {
            return fileName;
        }

        // 2. If filename has parts and last part is culture code → standard format (strings.fr.json → "fr")
        var parts2 = fileName.Split('.');
        if (parts2.Length > 1 && IsValidCultureCode(parts2[^1]))
        {
            return parts2[^1];
        }

        // 3. Fallback to default
        return config.DefaultLanguageCode ?? "en";
    }

    /// <summary>
    /// Extracts language code from JSON file content's _meta.culture property.
    /// Returns null if not found or not applicable.
    /// </summary>
    private string? ExtractLanguageCodeFromContent(string content, string effectiveFormat)
    {
        // Only applicable to JSON formats
        if (effectiveFormat != "json" && effectiveFormat != "i18next")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("_meta", out var meta) &&
                meta.TryGetProperty("culture", out var culture) &&
                culture.ValueKind == JsonValueKind.String)
            {
                var cultureCode = culture.GetString();
                if (!string.IsNullOrWhiteSpace(cultureCode))
                {
                    return cultureCode;
                }
            }
        }
        catch
        {
            // Invalid JSON or parsing error - fall back to filename detection
        }

        return null;
    }

    /// <summary>
    /// Validates whether a string is a valid .NET culture code.
    /// Uses strict validation by checking ThreeLetterISOLanguageName to filter out
    /// custom/unknown cultures that .NET 9 accepts but aren't real language codes.
    /// </summary>
    private bool IsValidCultureCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
            return false;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            // .NET 9 is very permissive and accepts arbitrary strings as cultures.
            // Check ThreeLetterISOLanguageName to ensure it's a real, known culture.
            // Unknown/custom cultures will have empty ThreeLetterISOLanguageName.
            return culture != null &&
                   !string.IsNullOrEmpty(culture.Name) &&
                   !string.IsNullOrEmpty(culture.ThreeLetterISOLanguageName);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Detects the naming convention used by the provided files.
    /// Returns "i18next", "standard", "resx", or "unknown".
    /// </summary>
    public string DetectNamingConvention(List<FileDto> files)
    {
        if (files.Count == 0)
            return "unknown";

        // Check if all files are RESX
        if (files.All(f => f.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
            return "resx";

        // Check if all files are JSON
        if (!files.All(f => f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            return "unknown";

        // For JSON files, detect i18next vs standard naming
        var fileNames = files.Select(f => Path.GetFileNameWithoutExtension(f.Path)).ToList();

        // i18next: all filenames are valid culture codes (en.json, fr.json, zh-Hans.json)
        if (fileNames.All(IsValidCultureCode))
            return "i18next";

        // Standard: filenames have baseName.json or baseName.lang.json pattern
        // Check if there's a common base name
        var possibleBaseNames = fileNames
            .Select(f => f.Split('.')[0])
            .Distinct()
            .ToList();

        if (possibleBaseNames.Count == 1)
        {
            // All files share the same base name - this is standard format
            return "standard";
        }

        // Mixed or unrecognized
        return "unknown";
    }

    /// <summary>
    /// Validates that file naming is consistent with the project format.
    /// Returns validation result with error message if inconsistent.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateFileNamingConsistency(
        List<FileDto> files, string projectFormat, ConfigurationModel? config)
    {
        if (files.Count == 0)
            return (true, null);

        var detectedConvention = DetectNamingConvention(files);
        var fileNames = string.Join(", ", files.Select(f => Path.GetFileName(f.Path)));

        // RESX projects
        if (projectFormat == "resx")
        {
            if (detectedConvention != "resx")
            {
                return (false,
                    $"Project format is 'resx' but files don't use RESX format. " +
                    $"Files: {fileNames}");
            }

            // Check all RESX files share the same base name
            var baseNames = files
                .Select(f => GetResxBaseName(f.Path))
                .Distinct()
                .ToList();

            if (baseNames.Count > 1)
            {
                return (false,
                    $"RESX files must share the same base name. Found different base names: {string.Join(", ", baseNames)}");
            }

            return (true, null);
        }

        // JSON standard projects
        if (projectFormat == "json")
        {
            if (detectedConvention == "resx")
            {
                return (false,
                    $"Project format is 'json' but files are RESX format. Files: {fileNames}");
            }

            if (detectedConvention == "i18next")
            {
                return (false,
                    $"File naming inconsistency detected.\n" +
                    $"Your files use i18next naming: {fileNames}\n" +
                    $"But project format is 'json' (standard format expects: strings.json, strings.fr.json).\n\n" +
                    $"To fix this, either:\n" +
                    $"  • Rename your files to standard format (strings.json, strings.fr.json), or\n" +
                    $"  • Change project format to 'i18next' in project settings");
            }

            return (true, null);
        }

        // i18next projects
        if (projectFormat == "i18next")
        {
            if (detectedConvention == "resx")
            {
                return (false,
                    $"Project format is 'i18next' but files are RESX format. Files: {fileNames}");
            }

            if (detectedConvention == "standard")
            {
                return (false,
                    $"File naming inconsistency detected.\n" +
                    $"Your files use standard naming: {fileNames}\n" +
                    $"But project format is 'i18next' (expects: en.json, fr.json).\n\n" +
                    $"To fix this, either:\n" +
                    $"  • Rename your files to i18next format (en.json, fr.json), or\n" +
                    $"  • Change project format to 'json' in project settings");
            }

            return (true, null);
        }

        return (true, null);
    }

    /// <summary>
    /// Gets the base name from a RESX file path (e.g., "Resources" from "Resources.el.resx").
    /// </summary>
    private string GetResxBaseName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');
        return parts[0];
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
            // Try to extract language code from _meta.culture in JSON content first
            // Fall back to filename-based detection
            var langCode = ExtractLanguageCodeFromContent(fileDto.Content, effectiveFormat)
                ?? ExtractLanguageCodeFromPath(fileDto.Path, config, effectiveFormat);

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
    public async Task<List<FileDto>> GenerateFilesFromDatabaseAsync(
        int projectId, string? configJson, string projectFormat, string defaultLanguage)
    {
        // Deserialize configuration or use defaults
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = configJson != null
            ? JsonSerializer.Deserialize<ConfigurationModel>(configJson, jsonOptions) ?? new ConfigurationModel()
            : new ConfigurationModel();

        // Determine effective format: config takes priority, then project format from DB
        var effectiveFormat = config.ResourceFormat ?? projectFormat;

        // Determine effective default language: config takes priority, then project setting
        var effectiveDefaultLang = config.DefaultLanguageCode ?? defaultLanguage;

        // Get the base name from stored files (important for RESX which has project-specific base names)
        var baseName = await GetBaseNameFromStoredFilesAsync(projectId, effectiveFormat, config);

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
                        BaseName = baseName,
                        Code = langCode,
                        Name = langCode,  // Use language code as display name
                        IsDefault = langCode == effectiveDefaultLang,
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
    /// Gets the base name from config, stored files, or defaults.
    /// Priority: 1) Config setting, 2) Stored files in current/, 3) Default value
    /// For RESX: default is "SharedResource" per ASP.NET Core convention
    /// For JSON: default is "strings"
    /// </summary>
    private async Task<string> GetBaseNameFromStoredFilesAsync(int projectId, string effectiveFormat, ConfigurationModel config)
    {
        // Config takes priority for both formats
        if (effectiveFormat == "resx" && !string.IsNullOrEmpty(config.Resx?.BaseName))
        {
            return config.Resx.BaseName;
        }
        if (effectiveFormat != "resx" && !string.IsNullOrEmpty(config.Json?.BaseName))
        {
            return config.Json.BaseName;
        }

        try
        {
            // List files in current/ folder to get original filenames
            var currentFiles = await _storageService.ListFilesAsync(projectId, "current/");

            if (currentFiles.Count > 0)
            {
                // Get the first file and extract base name based on format
                foreach (var filePath in currentFiles)
                {
                    var fileName = Path.GetFileName(filePath);

                    if (effectiveFormat == "resx" && fileName.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
                    {
                        // RESX: SharedResource.resx or SharedResource.el.resx → "SharedResource"
                        return GetResxBaseName(fileName);
                    }
                    else if (effectiveFormat != "resx" && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // JSON: Check if i18next (filename is culture code) or standard
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                        // i18next mode: filename IS the culture code (en.json, fr.json)
                        if (effectiveFormat == "i18next" || config.Json?.I18nextCompatible == true)
                        {
                            return "strings"; // i18next doesn't use base name in filename
                        }

                        // Standard JSON: strings.json or strings.fr.json → "strings"
                        var parts = nameWithoutExt.Split('.');
                        return parts[0];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get base name from stored files for project {ProjectId}", projectId);
        }

        // Fallback to defaults per ASP.NET Core conventions
        // RESX: "SharedResource" is the standard name for shared/global resources
        // JSON: "strings" is a common convention
        return effectiveFormat == "resx" ? "SharedResource" : "strings";
    }

    /// <summary>
    /// Gets the filename for a language based on configuration and project format.
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

        // i18next format: en.json, fr.json (project format or config flag)
        if (effectiveFormat == "i18next" || config.Json?.I18nextCompatible == true)
        {
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
