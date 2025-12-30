using System.IO.Compression;
using System.Security.Claims;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Files;
using LrmCloud.Shared.DTOs.Sync;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Interface for file-level import/export operations.
/// </summary>
public interface IFileOperationsService
{
    /// <summary>
    /// Import files into a project.
    /// </summary>
    Task<FileImportResponse> ImportFilesAsync(int projectId, int userId, FileImportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Export project to files as ZIP.
    /// </summary>
    Task<byte[]> ExportFilesAsync(int projectId, int userId, string format, string[]? languages, CancellationToken ct = default);

    /// <summary>
    /// Get preview of what export would produce.
    /// </summary>
    Task<FileExportPreviewResponse> GetExportPreviewAsync(int projectId, int userId, string format, string[]? languages, CancellationToken ct = default);
}

/// <summary>
/// Service for file-level import/export operations.
/// Bridges file parsing/generation to key-level sync.
/// </summary>
public class FileOperationsService : IFileOperationsService
{
    private readonly AppDbContext _db;
    private readonly IFileImportService _fileImportService;
    private readonly IFileExportService _fileExportService;
    private readonly IKeySyncService _keySyncService;
    private readonly IProjectService _projectService;
    private readonly ILogger<FileOperationsService> _logger;

    public FileOperationsService(
        AppDbContext db,
        IFileImportService fileImportService,
        IFileExportService fileExportService,
        IKeySyncService keySyncService,
        IProjectService projectService,
        ILogger<FileOperationsService> logger)
    {
        _db = db;
        _fileImportService = fileImportService;
        _fileExportService = fileExportService;
        _keySyncService = keySyncService;
        _projectService = projectService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileImportResponse> ImportFilesAsync(
        int projectId,
        int userId,
        FileImportRequest request,
        CancellationToken ct = default)
    {
        var response = new FileImportResponse();

        try
        {
            // 1. Validate access
            if (!await _projectService.CanManageResourcesAsync(projectId, userId))
            {
                response.Errors.Add("You don't have permission to import to this project");
                return response;
            }

            // 2. Get project for default language
            var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
            if (project == null)
            {
                response.Errors.Add("Project not found");
                return response;
            }

            if (!request.Files.Any())
            {
                response.Errors.Add("No files to import");
                return response;
            }

            // 3. Detect format if not specified
            var format = request.Format ?? DetectFormat(request.Files);
            if (string.IsNullOrEmpty(format))
            {
                response.Errors.Add("Could not detect file format. Please specify format explicitly.");
                return response;
            }

            _logger.LogInformation(
                "Importing {FileCount} files to project {ProjectId} with format {Format}",
                request.Files.Count, projectId, format);

            // 4. Parse files → GitHubEntry[]
            var fileDict = request.Files.ToDictionary(f => f.Path, f => f.Content);
            Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> entries;

            try
            {
                entries = _fileImportService.ParseFiles(format, fileDict, project.DefaultLanguage);
            }
            catch (Exception ex)
            {
                response.Errors.Add($"Failed to parse files: {ex.Message}");
                _logger.LogWarning(ex, "Failed to parse files for project {ProjectId}", projectId);
                return response;
            }

            if (!entries.Any())
            {
                response.Errors.Add("No entries found in files");
                return response;
            }

            // 5. Convert GitHubEntry → EntryChangeDto
            // Group by key to handle plurals correctly
            var entryChanges = new List<EntryChangeDto>();
            var processedKeys = new HashSet<(string Key, string Lang)>();

            foreach (var entry in entries.Values)
            {
                var keyLang = (entry.Key, entry.LanguageCode);

                // Skip if we've already processed this key+lang (for plurals, we process all forms at once)
                if (processedKeys.Contains(keyLang))
                    continue;

                if (entry.IsPlural && entry.PluralForms != null && entry.PluralForms.Count > 0)
                {
                    // Plural entry - collect all forms for this key+lang
                    entryChanges.Add(new EntryChangeDto
                    {
                        Key = entry.Key,
                        Lang = entry.LanguageCode,
                        Value = entry.PluralForms.GetValueOrDefault("other", ""),
                        Comment = entry.Comment,
                        IsPlural = true,
                        PluralForms = entry.PluralForms,
                        SourcePluralText = entry.SourcePluralText,
                        BaseHash = null // No conflict detection for import
                    });
                    processedKeys.Add(keyLang);
                }
                else
                {
                    // Simple entry
                    entryChanges.Add(new EntryChangeDto
                    {
                        Key = entry.Key,
                        Lang = entry.LanguageCode,
                        Value = entry.Value ?? "",
                        Comment = entry.Comment,
                        IsPlural = false,
                        PluralForms = null,
                        SourcePluralText = null,
                        BaseHash = null // No conflict detection for import
                    });
                    processedKeys.Add(keyLang);
                }
            }

            // 6. Build KeySyncPushRequest and call KeySyncService
            var pushRequest = new KeySyncPushRequest
            {
                Entries = entryChanges,
                Message = request.Message ?? "Import from web UI"
            };

            var pushResult = await _keySyncService.PushAsync(projectId, userId, pushRequest, ct);

            // 7. Build response
            response.Success = pushResult.Conflicts.Count == 0;
            response.Applied = pushResult.Applied;

            if (pushResult.Conflicts.Count > 0)
            {
                foreach (var conflict in pushResult.Conflicts)
                {
                    response.Errors.Add($"Conflict on key '{conflict.Key}' ({conflict.Lang}): {conflict.Type}");
                }
            }

            _logger.LogInformation(
                "Imported {Applied} entries to project {ProjectId}",
                response.Applied, projectId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import files to project {ProjectId}", projectId);
            response.Errors.Add($"Import failed: {ex.Message}");
            return response;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportFilesAsync(
        int projectId,
        int userId,
        string format,
        string[]? languages,
        CancellationToken ct = default)
    {
        // 1. Validate access
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to export this project");
        }

        _logger.LogInformation(
            "Exporting project {ProjectId} with format {Format}, languages: {Languages}",
            projectId, format, languages != null ? string.Join(",", languages) : "all");

        // 2. Generate files using FileExportService
        var files = await _fileExportService.ExportProjectAsync(
            projectId,
            ".", // base path
            format,
            languages);

        if (!files.Any())
        {
            throw new InvalidOperationException("No files generated for export");
        }

        // 3. Package into ZIP
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var (path, content) in files)
            {
                // Clean path (remove leading ./ if present)
                var cleanPath = path.StartsWith("./") ? path[2..] : path;

                var entry = archive.CreateEntry(cleanPath, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync(content);
            }
        }

        memoryStream.Position = 0;
        return memoryStream.ToArray();
    }

    /// <inheritdoc />
    public async Task<FileExportPreviewResponse> GetExportPreviewAsync(
        int projectId,
        int userId,
        string format,
        string[]? languages,
        CancellationToken ct = default)
    {
        // 1. Validate access
        if (!await _projectService.CanViewProjectAsync(projectId, userId))
        {
            throw new UnauthorizedAccessException("You don't have permission to view this project");
        }

        // 2. Get project info
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            throw new InvalidOperationException("Project not found");
        }

        // 3. Get language stats
        var languageStats = await _db.Translations
            .Where(t => t.ResourceKey != null && t.ResourceKey.ProjectId == projectId)
            .GroupBy(t => t.LanguageCode)
            .Select(g => new { Language = g.Key ?? project.DefaultLanguage, KeyCount = g.Count() })
            .ToListAsync(ct);

        // 4. Get total key count
        var totalKeys = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .CountAsync(ct);

        // 5. Filter to requested languages
        var targetLanguages = languages?.ToList() ?? languageStats.Select(s => s.Language).ToList();

        // Ensure default language is included
        if (!targetLanguages.Contains(project.DefaultLanguage))
        {
            targetLanguages.Insert(0, project.DefaultLanguage);
        }

        // 6. Build preview
        var files = new List<ExportFileInfo>();
        foreach (var lang in targetLanguages)
        {
            var stat = languageStats.FirstOrDefault(s => s.Language == lang);
            files.Add(new ExportFileInfo
            {
                Path = GetExportPath(format, lang, lang == project.DefaultLanguage),
                Language = lang,
                KeyCount = stat?.KeyCount ?? 0,
                IsDefault = lang == project.DefaultLanguage
            });
        }

        return new FileExportPreviewResponse
        {
            Files = files,
            TotalKeys = totalKeys
        };
    }

    /// <summary>
    /// Detect format from file extensions.
    /// </summary>
    private static string? DetectFormat(List<FileDto> files)
    {
        var extensions = files
            .Select(f => Path.GetExtension(f.Path).ToLowerInvariant())
            .Distinct()
            .ToList();

        // Check for specific formats
        if (extensions.Any(e => e == ".resx"))
            return "resx";

        if (extensions.Any(e => e == ".strings" || e == ".stringsdict"))
            return "ios";

        if (extensions.Any(e => e == ".po" || e == ".pot"))
            return "po";

        if (extensions.Any(e => e == ".xliff" || e == ".xlf"))
            return "xliff";

        if (extensions.Any(e => e == ".xml"))
        {
            // Check if it looks like Android (values folder in path)
            if (files.Any(f => f.Path.Contains("values")))
                return "android";
        }

        if (extensions.Any(e => e == ".json"))
        {
            // Check if it looks like i18next (filename is language code like en.json)
            var jsonFiles = files.Where(f => f.Path.EndsWith(".json")).ToList();
            if (jsonFiles.Any())
            {
                var firstFile = jsonFiles.First();
                var fileName = Path.GetFileNameWithoutExtension(firstFile.Path);
                // If filename is short (likely language code), assume i18next
                if (fileName.Length <= 5 && !fileName.Contains("."))
                    return "i18next";
            }
            return "json";
        }

        return null;
    }

    /// <summary>
    /// Get the expected export path for a language.
    /// </summary>
    private static string GetExportPath(string format, string language, bool isDefault)
    {
        return format.ToLowerInvariant() switch
        {
            "resx" => isDefault ? "Resources.resx" : $"Resources.{language}.resx",
            "json" => isDefault ? "strings.json" : $"strings.{language}.json",
            "i18next" => $"{language}.json",
            "android" => isDefault ? "values/strings.xml" : $"values-{language}/strings.xml",
            "ios" => $"{language}.lproj/Localizable.strings",
            "po" => isDefault ? "messages.pot" : $"{language}.po",
            "xliff" => isDefault ? "messages.xliff" : $"messages.{language}.xliff",
            _ => $"{language}.txt"
        };
    }
}
