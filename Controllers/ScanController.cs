// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Scanning;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly string _sourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;
    private readonly CodeScanner _scanner;

    public ScanController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _sourcePath = configuration["SourcePath"] ?? Directory.GetParent(_resourcePath)?.FullName ?? _resourcePath;
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
        _scanner = new CodeScanner();
    }

    /// <summary>
    /// Scan source code for localization key references
    /// </summary>
    [HttpPost("scan")]
    public ActionResult<object> Scan([FromBody] ScanRequest? request)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new { error = "No default language file found" });
            }

            // Get all keys from resource files
            var allResourceKeys = defaultFile.Entries.Select(e => e.Key).Distinct().ToHashSet();

            // Scan source code
            var excludePatterns = request?.ExcludePatterns ?? new List<string>
            {
                "**/bin/**",
                "**/obj/**",
                "**/node_modules/**",
                "**/.git/**"
            };

            var result = _scanner.Scan(_sourcePath, resourceFiles, false, excludePatterns, null, null);

            return Ok(new
            {
                scannedFiles = result.FilesScanned,
                totalReferences = result.TotalReferences,
                uniqueKeysFound = result.UniqueKeysFound,
                unusedKeys = result.UnusedKeys.Count,
                missingKeys = result.MissingKeys.Count,
                unused = result.UnusedKeys,
                missing = result.MissingKeys.Select(k => k.Key),
                references = result.AllKeyUsages.Select(k => new
                {
                    key = k.Key,
                    referenceCount = k.References.Count,
                    references = k.References.Select(r => new
                    {
                        file = r.FilePath,
                        line = r.Line,
                        pattern = r.Pattern,
                        confidence = r.Confidence.ToString()
                    })
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get unused keys
    /// </summary>
    [HttpGet("unused")]
    public ActionResult<object> GetUnused()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new { error = "No default language file found" });
            }

            var excludePatterns = new List<string>
            {
                "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**"
            };

            var result = _scanner.Scan(_sourcePath, resourceFiles, false, excludePatterns, null, null);

            return Ok(new { unusedKeys = result.UnusedKeys });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get missing keys (in code but not in resources)
    /// </summary>
    [HttpGet("missing")]
    public ActionResult<object> GetMissing()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new { error = "No default language file found" });
            }

            var excludePatterns = new List<string>
            {
                "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**"
            };

            var result = _scanner.Scan(_sourcePath, resourceFiles, false, excludePatterns, null, null);

            return Ok(new { missingKeys = result.MissingKeys.Select(k => k.Key).ToList() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get code references for a specific key
    /// </summary>
    [HttpGet("references/{keyName}")]
    public ActionResult<object> GetReferences(string keyName)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var excludePatterns = new List<string>
            {
                "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**"
            };

            var result = _scanner.Scan(_sourcePath, resourceFiles, false, excludePatterns, null, null);
            var keyUsage = result.AllKeyUsages.FirstOrDefault(k => k.Key == keyName);

            if (keyUsage == null)
            {
                return NotFound(new { error = $"No references found for key '{keyName}'" });
            }

            return Ok(new
            {
                key = keyName,
                referenceCount = keyUsage.References.Count,
                references = keyUsage.References.Select(r => new
                {
                    file = r.FilePath,
                    line = r.Line,
                    pattern = r.Pattern,
                    confidence = r.Confidence.ToString()
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ScanRequest
{
    public List<string>? ExcludePatterns { get; set; }
}
