// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Translation;
using LocalizationManager.Models.Api;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly IResourceBackend _backend;

    public TranslationController(IConfiguration configuration, IResourceBackend backend)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _backend = backend;
    }

    /// <summary>
    /// Get list of available translation providers
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<TranslationProvidersResponse> GetProviders()
    {
        var providers = TranslationProviderFactory.GetProviderInfos()
            .Select(p => new TranslationProviderInfo
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                RequiresApiKey = p.RequiresApiKey
            })
            .ToList();

        return Ok(new TranslationProvidersResponse { Providers = providers });
    }

    /// <summary>
    /// Translate keys using specified provider. Runs the translation
    /// independently per resource group so that keys never cross group
    /// boundaries (a key in CustomerResources stays in CustomerResources;
    /// translations never leak into GlassResources's files).
    /// </summary>
    [HttpPost("translate")]
    public async Task<ActionResult<Models.Api.TranslationResponse>> Translate([FromBody] TranslateRequest request)
    {
        try
        {
            var directory = _backend.Discovery.DiscoverResourceGroups(_resourcePath);

            // Load configuration once.
            var (config, _) = Core.Configuration.ConfigurationManager.LoadConfiguration(null, _resourcePath);

            // Get provider once.
            var provider = TranslationProviderFactory.Create(
                request.Provider ?? "google",
                config
            );

            var results = new List<TranslationResult>();
            var errors = new List<TranslationError>();

            foreach (var group in directory.Groups)
            {
                var resourceFiles = group.Files.Select(f => _backend.Reader.Read(f)).ToList();
                var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
                if (defaultFile == null) continue;

                // Get keys to translate for this group.
                var keysToTranslate = new List<string>();
                if (request.Keys?.Any() == true)
                {
                    // Restrict explicit keys to those that exist in this group's default.
                    var groupKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    keysToTranslate = request.Keys.Where(k => groupKeys.Contains(k)).ToList();
                }
                else if (request.OnlyMissing)
                {
                    foreach (var file in resourceFiles.Where(f => !f.Language.IsDefault))
                    {
                        var defaultKeys = defaultFile.Entries.Select(e => e.Key).ToHashSet();
                        var fileKeys = file.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToHashSet();
                        var missingKeys = defaultKeys.Except(fileKeys);
                        keysToTranslate.AddRange(missingKeys);
                    }
                    keysToTranslate = keysToTranslate.Distinct().ToList();
                }
                else
                {
                    keysToTranslate = defaultFile.Entries.Select(e => e.Key).Distinct().ToList();
                }

                if (keysToTranslate.Count == 0) continue;

                // Translate each key for each target language within this group.
                foreach (var targetFile in resourceFiles.Where(f => !f.Language.IsDefault))
                {
                    var targetLang = targetFile.Language.Code ?? "en";

                    foreach (var key in keysToTranslate)
                    {
                        var sourceEntry = defaultFile.Entries.FirstOrDefault(e => e.Key == key);
                        if (sourceEntry == null || string.IsNullOrWhiteSpace(sourceEntry.Value))
                            continue;

                    try
                    {
                        // Check if this is a plural key
                        if (sourceEntry.IsPlural && sourceEntry.PluralForms != null && sourceEntry.PluralForms.Count > 0)
                        {
                            // Translate each plural form
                            var translatedForms = new Dictionary<string, string>();

                            foreach (var (formName, formValue) in sourceEntry.PluralForms)
                            {
                                if (string.IsNullOrWhiteSpace(formValue))
                                    continue;

                                var translationRequest = new Core.Translation.TranslationRequest
                                {
                                    SourceText = formValue,
                                    SourceLanguage = request.SourceLanguage ?? "en",
                                    TargetLanguage = targetLang,
                                    Context = $"Plural form '{formName}' of key '{key}'"
                                };

                                var response = await provider.TranslateAsync(translationRequest);
                                translatedForms[formName] = response.TranslatedText;
                            }

                            // Update the entry with plural forms
                            var existingEntry = targetFile.Entries.FirstOrDefault(e => e.Key == key);
                            if (existingEntry != null)
                            {
                                existingEntry.IsPlural = true;
                                existingEntry.PluralForms = translatedForms;
                                existingEntry.Value = translatedForms.GetValueOrDefault("other") ?? translatedForms.Values.FirstOrDefault() ?? "";
                            }
                            else
                            {
                                targetFile.Entries.Add(new Core.Models.ResourceEntry
                                {
                                    Key = key,
                                    Value = translatedForms.GetValueOrDefault("other") ?? translatedForms.Values.FirstOrDefault() ?? "",
                                    Comment = sourceEntry.Comment,
                                    IsPlural = true,
                                    PluralForms = translatedForms
                                });
                            }

                            results.Add(new TranslationResult
                            {
                                Key = key,
                                Language = targetLang,
                                TranslatedValue = $"[plural] {translatedForms.Count} forms",
                                Success = true
                            });
                        }
                        else
                        {
                            // Regular (non-plural) translation
                            var translationRequest = new Core.Translation.TranslationRequest
                            {
                                SourceText = sourceEntry.Value,
                                SourceLanguage = request.SourceLanguage ?? "en",
                                TargetLanguage = targetLang,
                                Context = key
                            };

                            var response = await provider.TranslateAsync(translationRequest);
                            var translatedText = response.TranslatedText;

                            // Update the entry
                            var existingEntry = targetFile.Entries.FirstOrDefault(e => e.Key == key);
                            if (existingEntry != null)
                            {
                                existingEntry.Value = translatedText;
                            }
                            else
                            {
                                targetFile.Entries.Add(new Core.Models.ResourceEntry
                                {
                                    Key = key,
                                    Value = translatedText,
                                    Comment = sourceEntry.Comment
                                });
                            }

                            results.Add(new TranslationResult
                            {
                                Key = key,
                                Language = targetLang,
                                TranslatedValue = translatedText,
                                Success = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Sanitize error message - only include generic translation failure info
                        var sanitizedError = ex is HttpRequestException
                            ? "Translation service unavailable"
                            : "Translation failed";

                        errors.Add(new TranslationError
                        {
                            Key = key,
                            Language = targetLang,
                            Error = sanitizedError
                        });
                    }
                    }

                    // Save the file if not dry run
                    if (!request.DryRun)
                    {
                        _backend.Writer.Write(targetFile);
                    }
                }
            }

            return Ok(new Models.Api.TranslationResponse
            {
                Success = true,
                TranslatedCount = results.Count,
                ErrorCount = errors.Count,
                Results = results,
                Errors = errors,
                DryRun = request.DryRun
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Error = "An error occurred while processing your request" });
        }
    }
}
