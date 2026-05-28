// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LanguageController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;
    private readonly LanguageFileManager _languageManager;

    public LanguageController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
        _languageManager = new LanguageFileManager();
        _languageManager.SetBackend(backend);
    }

    /// <summary>
    /// List all languages. Aggregates across resource groups so that directories
    /// containing multiple base names (e.g. CustomerResources.resx +
    /// GlassResources.resx) report one entry per distinct culture, not per file.
    /// </summary>
    [HttpGet]
    public ActionResult<LanguagesResponse> GetLanguages()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var groupedFiles = directory.Groups
                .SelectMany(g => g.Files.Select(f => new
                {
                    Group = g,
                    File = f,
                    Resource = _backend.Reader.Read(f)
                }))
                .ToList();

            // Total keys: distinct (BaseName::Key) across all default files.
            var totalKeys = groupedFiles
                .Where(x => x.File.IsDefault)
                .SelectMany(x => x.Resource.Entries.Select(e => $"{x.Group.BaseName}::{e.Key}"))
                .Distinct()
                .Count();

            // One returned entry per distinct culture (keyed by Code + IsDefault).
            var cultureGroups = groupedFiles
                .GroupBy(x => new { Code = x.File.Code ?? string.Empty, x.File.IsDefault })
                .OrderByDescending(g => g.Key.IsDefault)
                .ThenBy(g => g.Key.Code)
                .ToList();

            var result = cultureGroups.Select(cultureGroup =>
            {
                var sample = cultureGroup.First();
                var translatedCount = cultureGroup
                    .SelectMany(x => x.Resource.Entries
                        .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                        .Select(e => $"{x.Group.BaseName}::{e.Key}"))
                    .Distinct()
                    .Count();

                var coverage = totalKeys > 0 ? (double)translatedCount / totalKeys * 100 : 0;

                return new Models.Api.LanguageInfo
                {
                    Code = sample.File.Code,
                    IsDefault = sample.File.IsDefault,
                    Name = sample.File.Name,
                    FilePath = string.Join(";", cultureGroup
                        .Select(x => x.File.FilePath)
                        .Where(p => !string.IsNullOrEmpty(p))),
                    DisplayName = _languageManager.GetCultureDisplayName(sample.File.Code ?? "default"),
                    TotalKeys = totalKeys,
                    TranslatedKeys = translatedCount,
                    Coverage = Math.Round(coverage, 2)
                };
            }).ToList();

            return Ok(new LanguagesResponse { Languages = result });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Add a new language. When the directory contains multiple resource groups
    /// (e.g. CustomerResources.resx and GlassResources.resx), a new
    /// <c>{group}.{culture}.resx</c> is created for EACH group, so the new
    /// culture appears as a single column across the editor.
    /// </summary>
    [HttpPost]
    public ActionResult<AddLanguageResponse> AddLanguage([FromBody] AddLanguageRequest request)
    {
        try
        {
            if (!_languageManager.IsValidCultureCode(request.CultureCode, out var culture))
            {
                return BadRequest(new ErrorResponse { Error = $"Invalid culture code: {request.CultureCode}" });
            }

            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            if (directory.Groups.Count == 0)
            {
                return NotFound(new ErrorResponse { Error = "No resource groups found in resource path" });
            }

            // Reject if any group already has this culture.
            var existingGroup = directory.Groups.FirstOrDefault(g =>
                _languageManager.LanguageFileExists(g.BaseName, request.CultureCode, _resourcePath));
            if (existingGroup != null)
            {
                return Conflict(new ErrorResponse { Error = $"Language file for '{request.CultureCode}' already exists (group '{existingGroup.BaseName}')" });
            }

            var createdFiles = new List<string>();
            foreach (var group in directory.Groups)
            {
                ResourceFile? sourceFile = null;
                if (!string.IsNullOrEmpty(request.CopyFrom))
                {
                    var sourceLanguage = group.Files.FirstOrDefault(l => (l.Code ?? string.Empty) == request.CopyFrom);
                    if (sourceLanguage != null)
                    {
                        sourceFile = _backend.Reader.Read(sourceLanguage);
                    }
                }

                var newFile = _languageManager.CreateLanguageFile(
                    group.BaseName,
                    request.CultureCode,
                    _resourcePath,
                    sourceFile
                );

                if (!request.Empty)
                {
                    _backend.Writer.Write(newFile);
                }

                if (!string.IsNullOrEmpty(newFile.Language.FilePath))
                {
                    createdFiles.Add(newFile.Language.FilePath);
                }
            }

            // Pick the first created file as the representative; FilePath in the
            // response carries a joined list of all created paths.
            return Ok(new AddLanguageResponse
            {
                Success = true,
                CultureCode = request.CultureCode,
                FileName = Path.GetFileName(createdFiles.FirstOrDefault() ?? string.Empty),
                FilePath = string.Join(";", createdFiles),
                DisplayName = _languageManager.GetCultureDisplayName(request.CultureCode)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Remove a language. When the directory contains multiple resource groups,
    /// the <c>{group}.{culture}.resx</c> file for every group is deleted, so the
    /// culture disappears as a column.
    /// </summary>
    [HttpDelete("{cultureCode}")]
    public ActionResult<RemoveLanguageResponse> RemoveLanguage(string cultureCode)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);

            var matchingFiles = directory.Groups
                .SelectMany(g => g.Files)
                .Where(l => (l.Code ?? string.Empty) == cultureCode)
                .ToList();

            if (matchingFiles.Count == 0)
            {
                return NotFound(new ErrorResponse { Error = $"Language '{cultureCode}' not found" });
            }

            if (matchingFiles.Any(l => l.IsDefault))
            {
                return BadRequest(new ErrorResponse { Error = "Cannot delete the default language file" });
            }

            foreach (var file in matchingFiles)
            {
                _languageManager.DeleteLanguageFile(file);
            }

            return Ok(new RemoveLanguageResponse
            {
                Success = true,
                CultureCode = cultureCode,
                FileName = string.Join(";", matchingFiles.Select(f => f.Name)),
                Message = $"Removed {matchingFiles.Count} language file(s) for '{cultureCode}'"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }
}
