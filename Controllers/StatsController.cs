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
    /// Get translation coverage statistics. Aggregates across resource groups so
    /// that directories containing multiple base names produce one entry per
    /// culture (not per file).
    /// </summary>
    [HttpGet]
    public ActionResult<StatsResponse> GetStats()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var grouped = directory.Groups
                .SelectMany(g => g.Files.Select(f => new
                {
                    Group = g,
                    File = f,
                    Resource = _backend.Reader.Read(f)
                }))
                .ToList();

            var defaultFiles = grouped.Where(x => x.File.IsDefault).ToList();
            if (defaultFiles.Count == 0)
            {
                return StatusCode(500, new { error = "No default language file found" });
            }

            // Total keys: sum of Count across the default file of each group.
            var totalKeys = defaultFiles.Sum(x => x.Resource.Count);

            var cultureGroups = grouped
                .GroupBy(x => new { Code = x.File.Code ?? string.Empty, x.File.IsDefault })
                .OrderByDescending(g => g.Key.IsDefault)
                .ThenBy(g => g.Key.Code)
                .ToList();

            var languageStats = cultureGroups.Select(cultureGroup =>
            {
                var sample = cultureGroup.First();
                var translatedCount = cultureGroup.Sum(x => x.Resource.CompletedCount);
                var coverage = totalKeys > 0 ? (double)translatedCount / totalKeys * 100 : 0;

                return new LanguageStats
                {
                    LanguageCode = sample.File.Code ?? "default",
                    FilePath = string.Join(";", cultureGroup
                        .Select(x => x.File.FilePath)
                        .Where(p => !string.IsNullOrEmpty(p))),
                    IsDefault = sample.File.IsDefault,
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
