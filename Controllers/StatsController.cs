// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public StatsController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// Get translation coverage statistics
    /// </summary>
    [HttpGet]
    public ActionResult<StatsResponse> GetStats()
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new { error = "No default language file found" });
            }

            var totalKeys = defaultFile.Entries.Select(e => e.Key).Distinct().Count();

            var languageStats = resourceFiles.Select(file =>
            {
                var translatedCount = file.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => e.Key)
                    .Distinct()
                    .Count();

                var coverage = totalKeys > 0 ? (double)translatedCount / totalKeys * 100 : 0;

                return new LanguageStats
                {
                    LanguageCode = file.Language.Code ?? "default",
                    FilePath = file.Language.FilePath,
                    IsDefault = file.Language.IsDefault,
                    TranslatedCount = translatedCount,
                    TotalCount = totalKeys,
                    Coverage = Math.Round(coverage, 2)
                };
            }).ToList();

            return Ok(new StatsResponse
            {
                TotalKeys = totalKeys,
                Languages = languageStats,
                OverallCoverage = languageStats.Count > 0
                    ? Math.Round(languageStats.Average(s => s.Coverage), 2)
                    : 0
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
