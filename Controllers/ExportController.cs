// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public ExportController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
    }

    /// <summary>
    /// Export all keys to JSON format
    /// </summary>
    [HttpGet("json")]
    public ActionResult<object> ExportToJson([FromQuery] bool includeComments = true)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new { error = "No default language found" });
            }

            var allKeys = defaultFile.Entries.Select(e => e.Key).Distinct().OrderBy(k => k).ToList();

            var exportData = allKeys.Select(key =>
            {
                var values = new Dictionary<string, string?>();
                string? comment = null;

                foreach (var resourceFile in resourceFiles)
                {
                    var entry = resourceFile.Entries.FirstOrDefault(e => e.Key == key);
                    values[resourceFile.Language.Code ?? "default"] = entry?.Value;

                    if (includeComments && comment == null && entry?.Comment != null)
                    {
                        comment = entry.Comment;
                    }
                }

                var result = new Dictionary<string, object>
                {
                    ["key"] = key,
                    ["values"] = values
                };

                if (includeComments && comment != null)
                {
                    result["comment"] = comment;
                }

                return result;
            });

            return Ok(new
            {
                languages = languages.Select(l => new { code = l.Code, name = l.Name, isDefault = l.IsDefault }),
                keys = exportData
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export all keys to CSV format (returns CSV text)
    /// </summary>
    [HttpGet("csv")]
    [Produces("text/csv")]
    public ActionResult ExportToCsv([FromQuery] bool includeComments = true)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, "No default language found");
            }

            var csv = new System.Text.StringBuilder();

            // Header
            csv.Append("Key");
            foreach (var lang in languages)
            {
                csv.Append($",{lang.Name}");
            }
            if (includeComments)
            {
                csv.Append(",Comment");
            }
            csv.AppendLine();

            // Rows
            var allKeys = defaultFile.Entries.Select(e => e.Key).Distinct().OrderBy(k => k).ToList();
            foreach (var key in allKeys)
            {
                csv.Append(EscapeCsvValue(key));

                foreach (var resourceFile in resourceFiles)
                {
                    var entry = resourceFile.Entries.FirstOrDefault(e => e.Key == key);
                    csv.Append($",{EscapeCsvValue(entry?.Value ?? string.Empty)}");
                }

                if (includeComments)
                {
                    var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == key);
                    csv.Append($",{EscapeCsvValue(defaultEntry?.Comment ?? string.Empty)}");
                }

                csv.AppendLine();
            }

            return Content(csv.ToString(), "text/csv", System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape quotes and wrap in quotes if needed
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
