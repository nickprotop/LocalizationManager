using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends.Android;
using LocalizationManager.Core.Backends.iOS;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Backends.Resx;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using LrmCloud.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for exporting project translations to file content.
/// Uses LocalizationManager.Core backends for serialization.
/// </summary>
public class FileExportService : IFileExportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FileExportService> _logger;

    public FileExportService(AppDbContext db, ILogger<FileExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> ExportProjectAsync(int projectId, string basePath)
    {
        return await ExportProjectAsync(projectId, basePath, null);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> ExportProjectAsync(
        int projectId, string basePath, IEnumerable<string>? languages)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null)
            throw new InvalidOperationException($"Project {projectId} not found");

        // Get all resource keys with their translations
        var keysQuery = _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .Include(k => k.Translations)
            .AsNoTracking();

        var keys = await keysQuery.ToListAsync();

        if (!keys.Any())
        {
            _logger.LogWarning("No resource keys found for project {ProjectId}", projectId);
            return new Dictionary<string, string>();
        }

        // Get all unique languages in this project
        var allLanguages = keys
            .SelectMany(k => k.Translations)
            .Select(t => t.LanguageCode)
            .Distinct()
            .ToList();

        // Filter to requested languages if specified
        var targetLanguages = languages?.ToList() ?? allLanguages;

        // Always include default language
        if (!targetLanguages.Contains(project.DefaultLanguage))
        {
            targetLanguages.Insert(0, project.DefaultLanguage);
        }

        var result = new Dictionary<string, string>();
        var writer = GetWriter(project.Format);

        foreach (var languageCode in targetLanguages)
        {
            var isDefault = languageCode == project.DefaultLanguage;
            var resourceFile = BuildResourceFile(keys, languageCode, isDefault, project.Format);

            if (!resourceFile.Entries.Any())
            {
                continue; // Skip empty language files
            }

            var filePath = GetFilePath(basePath, project.Format, languageCode, isDefault);
            var content = writer.SerializeToString(resourceFile);

            result[filePath] = content;

            // For iOS, also generate .stringsdict if there are plurals
            if (project.Format.Equals("ios", StringComparison.OrdinalIgnoreCase))
            {
                var iosWriter = (IosResourceWriter)writer;
                var stringsdictContent = iosWriter.SerializeToStringdict(resourceFile);
                if (!string.IsNullOrEmpty(stringsdictContent))
                {
                    var stringsdictPath = filePath.Replace(".strings", ".stringsdict");
                    result[stringsdictPath] = stringsdictContent;
                }
            }
        }

        _logger.LogInformation("Exported {FileCount} files for project {ProjectId}", result.Count, projectId);
        return result;
    }

    /// <summary>
    /// Builds a ResourceFile from database keys and translations.
    /// </summary>
    private static ResourceFile BuildResourceFile(
        List<Shared.Entities.ResourceKey> keys,
        string languageCode,
        bool isDefault,
        string format)
    {
        var entries = new List<ResourceEntry>();

        foreach (var key in keys)
        {
            // Get translations for this language
            var translations = key.Translations
                .Where(t => t.LanguageCode == languageCode)
                .ToList();

            if (!translations.Any())
            {
                // For default language, include key even without translation
                if (isDefault)
                {
                    entries.Add(new ResourceEntry
                    {
                        Key = key.KeyName,
                        Value = "",
                        Comment = key.Comment,
                        IsPlural = key.IsPlural
                    });
                }
                continue;
            }

            if (key.IsPlural)
            {
                // Build plural entry
                var pluralForms = translations
                    .Where(t => !string.IsNullOrEmpty(t.PluralForm))
                    .ToDictionary(t => t.PluralForm, t => t.Value ?? "");

                entries.Add(new ResourceEntry
                {
                    Key = key.KeyName,
                    Value = pluralForms.GetValueOrDefault("other", ""),
                    Comment = key.Comment,
                    IsPlural = true,
                    PluralForms = pluralForms
                });
            }
            else
            {
                // Simple string entry
                var translation = translations.FirstOrDefault(t => string.IsNullOrEmpty(t.PluralForm));
                entries.Add(new ResourceEntry
                {
                    Key = key.KeyName,
                    Value = translation?.Value ?? "",
                    Comment = key.Comment,
                    IsPlural = false
                });
            }
        }

        return new ResourceFile
        {
            Language = new LanguageInfo
            {
                Code = languageCode,
                IsDefault = isDefault,
                Name = languageCode
            },
            Entries = entries
        };
    }

    /// <summary>
    /// Gets the appropriate writer for the format.
    /// </summary>
    private static IResourceWriter GetWriter(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "resx" => new ResxResourceWriter(),
            "json" => new JsonResourceWriter(new JsonFormatConfiguration { UseNestedKeys = false }),
            "i18next" => new JsonResourceWriter(new JsonFormatConfiguration { I18nextCompatible = true }),
            "android" => new AndroidResourceWriter(),
            "ios" => new IosResourceWriter(),
            _ => throw new NotSupportedException($"Format '{format}' is not supported for export")
        };
    }

    /// <summary>
    /// Gets the file path for a language based on format conventions.
    /// </summary>
    private static string GetFilePath(string basePath, string format, string languageCode, bool isDefault)
    {
        // Normalize base path (remove trailing slashes)
        basePath = basePath.TrimEnd('/', '\\');
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = ".";
        }

        // Normalize language code - empty or null should be treated as default
        var hasLanguageCode = !string.IsNullOrEmpty(languageCode);
        var useDefaultPath = isDefault || !hasLanguageCode;

        return format.ToLowerInvariant() switch
        {
            // RESX: {basePath}/Resources.resx, {basePath}/Resources.{lang}.resx
            "resx" => useDefaultPath
                ? $"{basePath}/Resources.resx"
                : $"{basePath}/Resources.{languageCode}.resx",

            // JSON: {basePath}/strings.json, {basePath}/strings.{lang}.json
            "json" => useDefaultPath
                ? $"{basePath}/strings.json"
                : $"{basePath}/strings.{languageCode}.json",

            // i18next: {basePath}/{lang}.json (use default language code if empty)
            "i18next" => $"{basePath}/{(hasLanguageCode ? languageCode : "en")}.json",

            // Android: {basePath}/values/strings.xml, {basePath}/values-{lang}/strings.xml
            "android" => useDefaultPath
                ? $"{basePath}/values/strings.xml"
                : $"{basePath}/values-{AndroidCultureMapper.CodeToFolder(languageCode).Replace("values-", "")}/strings.xml",

            // iOS: {basePath}/{lang}.lproj/Localizable.strings
            "ios" => $"{basePath}/{IosCultureMapper.CodeToLproj(hasLanguageCode ? languageCode : "Base")}/Localizable.strings",

            _ => throw new NotSupportedException($"Format '{format}' is not supported")
        };
    }
}
