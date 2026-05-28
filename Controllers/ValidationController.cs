// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Validation;
using LocalizationManager.Shared.Enums;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;
    private readonly ResourceValidator _validator;

    public ValidationController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
        _validator = new ResourceValidator();
    }

    /// <summary>
    /// Validate all resource files. Runs the validator independently per
    /// resource group so that keys from CustomerResources aren't flagged as
    /// "missing" from GlassResources's translation, then merges per-culture
    /// results across groups.
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<ValidationResponse> Validate([FromBody] ValidateRequest? request)
    {
        try
        {
            var placeholderTypes = request?.EnabledPlaceholderTypes ?? PlaceholderType.All;
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var perGroup = directory.Groups
                .Select(g =>
                {
                    var files = g.Files.Select(f => _backend.Reader.Read(f)).ToList();
                    return _validator.Validate(files, placeholderTypes);
                })
                .ToList();

            var merged = MergeResults(perGroup);

            return Ok(new ValidationResponse
            {
                IsValid = merged.IsValid,
                MissingKeys = merged.MissingKeys,
                DuplicateKeys = merged.DuplicateKeys,
                EmptyValues = merged.EmptyValues,
                ExtraKeys = merged.ExtraKeys,
                PlaceholderMismatches = merged.PlaceholderMismatches.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Select(v => v.Key).ToList()
                ),
                Summary = new ValidationSummary
                {
                    TotalIssues = merged.TotalIssues,
                    MissingCount = merged.MissingKeys.Sum(kv => kv.Value.Count),
                    DuplicatesCount = merged.DuplicateKeys.Sum(kv => kv.Value.Count),
                    EmptyCount = merged.EmptyValues.Sum(kv => kv.Value.Count),
                    ExtraCount = merged.ExtraKeys.Sum(kv => kv.Value.Count),
                    PlaceholderCount = merged.PlaceholderMismatches.Sum(kv => kv.Value.Count)
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get validation issues summary
    /// </summary>
    [HttpGet("issues")]
    public ActionResult<ValidationSummary> GetIssues()
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);
            var perGroup = directory.Groups
                .Select(g =>
                {
                    var files = g.Files.Select(f => _backend.Reader.Read(f)).ToList();
                    return _validator.Validate(files);
                })
                .ToList();
            var merged = MergeResults(perGroup);

            return Ok(new ValidationSummary
            {
                HasIssues = !merged.IsValid,
                MissingCount = merged.MissingKeys.Sum(kv => kv.Value.Count),
                DuplicatesCount = merged.DuplicateKeys.Sum(kv => kv.Value.Count),
                EmptyCount = merged.EmptyValues.Sum(kv => kv.Value.Count),
                ExtraCount = merged.ExtraKeys.Sum(kv => kv.Value.Count),
                PlaceholderCount = merged.PlaceholderMismatches.Sum(kv => kv.Value.Count),
                TotalIssues = merged.TotalIssues
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }

    private static LocalizationManager.Core.Models.ValidationResult MergeResults(
        IEnumerable<LocalizationManager.Core.Models.ValidationResult> results)
    {
        var merged = new LocalizationManager.Core.Models.ValidationResult();
        foreach (var r in results)
        {
            foreach (var (lang, keys) in r.MissingKeys)
            {
                if (!merged.MissingKeys.TryGetValue(lang, out var list)) merged.MissingKeys[lang] = list = new();
                list.AddRange(keys);
            }
            foreach (var (lang, keys) in r.ExtraKeys)
            {
                if (!merged.ExtraKeys.TryGetValue(lang, out var list)) merged.ExtraKeys[lang] = list = new();
                list.AddRange(keys);
            }
            foreach (var (lang, keys) in r.EmptyValues)
            {
                if (!merged.EmptyValues.TryGetValue(lang, out var list)) merged.EmptyValues[lang] = list = new();
                list.AddRange(keys);
            }
            foreach (var (lang, keys) in r.DuplicateKeys)
            {
                if (!merged.DuplicateKeys.TryGetValue(lang, out var list)) merged.DuplicateKeys[lang] = list = new();
                list.AddRange(keys);
            }
            foreach (var (lang, mismatches) in r.PlaceholderMismatches)
            {
                if (!merged.PlaceholderMismatches.TryGetValue(lang, out var dict)) merged.PlaceholderMismatches[lang] = dict = new();
                foreach (var (k, v) in mismatches) dict[k] = v;
            }
        }
        return merged;
    }
}
