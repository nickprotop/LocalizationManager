// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public ExportController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// Export all keys to JSON format. Includes a <c>resourceGroup</c> field per
    /// key so multi-base directories round-trip through export/import.
    /// </summary>
    [HttpGet("json")]
    public ActionResult<object> ExportToJson([FromQuery] bool includeComments = true)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);

            // Distinct cultures across all groups, default first.
            var allLanguages = directory.Groups
                .SelectMany(g => g.Files)
                .GroupBy(f => new { Code = f.Code ?? string.Empty, f.IsDefault })
                .Select(g => g.First())
                .OrderByDescending(f => f.IsDefault)
                .ThenBy(f => f.Code)
                .ToList();

            var exportData = new List<Dictionary<string, object>>();

            foreach (var group in directory.Groups)
            {
                var files = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
                var defaultFile = files.FirstOrDefault(f => f.Language.IsDefault);
                if (defaultFile == null) continue;

                var keys = defaultFile.Entries.Select(e => e.Key).Distinct().OrderBy(k => k).ToList();

                foreach (var key in keys)
                {
                    var values = new Dictionary<string, string?>();
                    string? comment = null;

                    foreach (var file in files)
                    {
                        var entry = file.Entries.FirstOrDefault(e => e.Key == key);
                        values[file.Language.Code ?? "default"] = entry?.Value;

                        if (includeComments && comment == null && entry?.Comment != null)
                        {
                            comment = entry.Comment;
                        }
                    }

                    var row = new Dictionary<string, object>
                    {
                        ["key"] = key,
                        ["resourceGroup"] = group.BaseName,
                        ["values"] = values
                    };

                    if (includeComments && comment != null)
                    {
                        row["comment"] = comment;
                    }

                    exportData.Add(row);
                }
            }

            return Ok(new
            {
                languages = allLanguages.Select(l => new { code = l.Code, name = l.Name, isDefault = l.IsDefault }),
                keys = exportData
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export all keys to CSV format (returns CSV text). Adds a <c>Group</c>
    /// column when multiple resource groups exist, so multi-base directories
    /// can round-trip through CSV import.
    /// </summary>
    [HttpGet("csv")]
    [Produces("text/csv")]
    public ActionResult ExportToCsv([FromQuery] bool includeComments = true)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var multiGroup = directory.Groups.Count > 1;

            // Determine column order: distinct cultures, default first.
            var allLanguages = directory.Groups
                .SelectMany(g => g.Files)
                .GroupBy(f => new { Code = f.Code ?? string.Empty, f.IsDefault })
                .Select(g => g.First())
                .OrderByDescending(f => f.IsDefault)
                .ThenBy(f => f.Code)
                .ToList();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.Append("Key");
            if (multiGroup) csv.Append(",Group");
            foreach (var lang in allLanguages)
            {
                csv.Append($",{lang.Name}");
            }
            if (includeComments)
            {
                csv.Append(",Comment");
            }
            csv.AppendLine();

            // Rows, ordered by group then key.
            foreach (var group in directory.Groups.OrderBy(g => g.BaseName, StringComparer.OrdinalIgnoreCase))
            {
                var files = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
                var defaultFile = files.FirstOrDefault(f => f.Language.IsDefault);
                if (defaultFile == null) continue;

                var keys = defaultFile.Entries.Select(e => e.Key).Distinct().OrderBy(k => k).ToList();

                foreach (var key in keys)
                {
                    csv.Append(EscapeCsvValue(key));
                    if (multiGroup) csv.Append($",{EscapeCsvValue(group.BaseName)}");

                    foreach (var lang in allLanguages)
                    {
                        var file = files.FirstOrDefault(f =>
                            (f.Language.Code ?? string.Empty) == (lang.Code ?? string.Empty) &&
                            f.Language.IsDefault == lang.IsDefault);
                        var entry = file?.Entries.FirstOrDefault(e => e.Key == key);
                        csv.Append($",{EscapeCsvValue(entry?.Value ?? string.Empty)}");
                    }

                    if (includeComments)
                    {
                        var defaultEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == key);
                        csv.Append($",{EscapeCsvValue(defaultEntry?.Comment ?? string.Empty)}");
                    }

                    csv.AppendLine();
                }
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
