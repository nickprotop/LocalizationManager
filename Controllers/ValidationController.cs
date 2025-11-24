// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Validation;
using LocalizationManager.Shared.Enums;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;
    private readonly ResourceValidator _validator;

    public ValidationController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
        _validator = new ResourceValidator();
    }

    /// <summary>
    /// Validate all resource files
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<object> Validate([FromBody] ValidateRequest? request)
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var placeholderTypes = request?.EnabledPlaceholderTypes ?? PlaceholderType.All;
            var result = _validator.Validate(resourceFiles, placeholderTypes);

            return Ok(new
            {
                isValid = result.IsValid,
                missingKeys = result.MissingKeys,
                duplicateKeys = result.DuplicateKeys,
                emptyValues = result.EmptyValues,
                extraKeys = result.ExtraKeys,
                placeholderMismatches = result.PlaceholderMismatches,
                summary = new
                {
                    totalIssues = result.TotalIssues,
                    missingCount = result.MissingKeys.Sum(kv => kv.Value.Count),
                    duplicatesCount = result.DuplicateKeys.Sum(kv => kv.Value.Count),
                    emptyCount = result.EmptyValues.Sum(kv => kv.Value.Count),
                    extraCount = result.ExtraKeys.Sum(kv => kv.Value.Count),
                    placeholderCount = result.PlaceholderMismatches.Sum(kv => kv.Value.Count)
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get validation issues summary
    /// </summary>
    [HttpGet("issues")]
    public ActionResult<object> GetIssues()
    {
        try
        {
            var languages = _discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _parser.Parse(l)).ToList();

            var result = _validator.Validate(resourceFiles);

            return Ok(new
            {
                hasIssues = !result.IsValid,
                missingCount = result.MissingKeys.Sum(kv => kv.Value.Count),
                duplicatesCount = result.DuplicateKeys.Sum(kv => kv.Value.Count),
                emptyCount = result.EmptyValues.Sum(kv => kv.Value.Count),
                extraCount = result.ExtraKeys.Sum(kv => kv.Value.Count),
                placeholderCount = result.PlaceholderMismatches.Sum(kv => kv.Value.Count),
                totalIssues = result.TotalIssues
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ValidateRequest
{
    public PlaceholderType EnabledPlaceholderTypes { get; set; } = PlaceholderType.All;
}
