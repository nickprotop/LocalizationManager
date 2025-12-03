// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;
using LocalizationManager.Models.Api;
using System.Globalization;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public ImportController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// Import keys from CSV format
    /// </summary>
    [HttpPost("csv")]
    public ActionResult<ImportResult> ImportFromCsv([FromBody] ImportCsvRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CsvData))
            {
                return BadRequest(new ErrorResponse { Error = "CSV data is required" });
            }

            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            // Parse CSV
            var lines = request.CsvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return BadRequest(new ErrorResponse { Error = "CSV must have at least a header row and one data row" });
            }

            // Parse header
            var headers = ParseCsvLine(lines[0]);
            if (headers.Length < 2 || headers[0].ToLower() != "key")
            {
                return BadRequest(new ErrorResponse { Error = "First column must be 'Key'" });
            }

            var addedCount = 0;
            var updatedCount = 0;
            var errors = new List<string>();

            // Process each row
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length < 2 || string.IsNullOrWhiteSpace(values[0]))
                        continue;

                    var key = values[0].Trim();
                    var keyExists = resourceFiles.Any(rf => rf.Entries.Any(e => e.Key == key));

                    // Extract values for each language
                    for (int j = 1; j < Math.Min(values.Length, headers.Length); j++)
                    {
                        var langHeader = headers[j];
                        if (langHeader.ToLower() == "comment")
                            continue;

                        var resourceFile = resourceFiles.FirstOrDefault(rf => rf.Language.Name == langHeader);
                        if (resourceFile == null)
                            continue;

                        var value = values[j];
                        var entry = resourceFile.Entries.FirstOrDefault(e => e.Key == key);

                        if (entry != null)
                        {
                            entry.Value = value;
                        }
                        else
                        {
                            resourceFile.Entries.Add(new ResourceEntry
                            {
                                Key = key,
                                Value = value,
                                Comment = null
                            });
                        }
                    }

                    if (keyExists)
                        updatedCount++;
                    else
                        addedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            // Save all files
            foreach (var resourceFile in resourceFiles)
            {
                _backend.Writer.Write(resourceFile);
            }

            return Ok(new ImportResult
            {
                Success = true,
                AddedCount = addedCount,
                UpdatedCount = updatedCount,
                TotalProcessed = addedCount + updatedCount,
                Errors = errors.Any() ? errors : null
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var currentValue = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        result.Add(currentValue.ToString());
        return result.ToArray();
    }
}
