// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LanguageController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;
    private readonly LanguageFileManager _languageManager;

    public LanguageController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
        _languageManager = new LanguageFileManager();
    }

    /// <summary>
    /// List all languages
    /// </summary>
    [HttpGet]
    public ActionResult<object> GetLanguages()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            var totalKeys = defaultFile?.Entries.Select(e => e.Key).Distinct().Count() ?? 0;

            var result = languages.Select(lang =>
            {
                var file = resourceFiles.First(f => f.Language.Code == lang.Code && f.Language.IsDefault == lang.IsDefault);
                var translatedCount = file.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => e.Key)
                    .Distinct()
                    .Count();

                var coverage = totalKeys > 0 ? (double)translatedCount / totalKeys * 100 : 0;

                return new
                {
                    code = lang.Code,
                    isDefault = lang.IsDefault,
                    name = lang.Name,
                    filePath = lang.FilePath,
                    displayName = _languageManager.GetCultureDisplayName(lang.Code ?? "default"),
                    totalKeys = file.Entries.Select(e => e.Key).Distinct().Count(),
                    translatedKeys = translatedCount,
                    coverage = Math.Round(coverage, 2)
                };
            });

            return Ok(new { languages = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add a new language
    /// </summary>
    [HttpPost]
    public ActionResult<object> AddLanguage([FromBody] AddLanguageRequest request)
    {
        try
        {
            if (!_languageManager.IsValidCultureCode(request.CultureCode, out var culture))
            {
                return BadRequest(new { error = $"Invalid culture code: {request.CultureCode}" });
            }

            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var baseName = languages.First().Name.Replace(".resx", "");

            if (_languageManager.LanguageFileExists(baseName, request.CultureCode, _resourcePath))
            {
                return Conflict(new { error = $"Language file for '{request.CultureCode}' already exists" });
            }

            ResourceFile? sourceFile = null;
            if (!string.IsNullOrEmpty(request.CopyFrom))
            {
                var sourceLanguage = languages.FirstOrDefault(l => l.Code == request.CopyFrom);
                if (sourceLanguage != null)
                {
                    sourceFile = _parser.Parse(sourceLanguage);
                }
            }

            var newFile = _languageManager.CreateLanguageFile(
                baseName,
                request.CultureCode,
                _resourcePath,
                sourceFile
            );

            if (!request.Empty)
            {
                _parser.Write(newFile);
            }

            return Ok(new
            {
                success = true,
                cultureCode = request.CultureCode,
                fileName = newFile.Language.Name,
                filePath = newFile.Language.FilePath,
                displayName = _languageManager.GetCultureDisplayName(request.CultureCode)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a language
    /// </summary>
    [HttpDelete("{cultureCode}")]
    public ActionResult<object> RemoveLanguage(string cultureCode)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var language = languages.FirstOrDefault(l => l.Code == cultureCode);

            if (language == null)
            {
                return NotFound(new { error = $"Language '{cultureCode}' not found" });
            }

            if (language.IsDefault)
            {
                return BadRequest(new { error = "Cannot delete the default language file" });
            }

            _languageManager.DeleteLanguageFile(language);

            return Ok(new
            {
                success = true,
                cultureCode,
                fileName = language.Name,
                message = "Language file deleted successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class AddLanguageRequest
{
    public string CultureCode { get; set; } = string.Empty;
    public string? CopyFrom { get; set; }
    public bool Empty { get; set; }
}
