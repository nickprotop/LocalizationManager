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
    /// Translate keys using specified provider
    /// </summary>
    [HttpPost("translate")]
    public async Task<ActionResult<Models.Api.TranslationResponse>> Translate([FromBody] TranslateRequest request)
    {
        try
        {
            var languages = _backend.Discovery.DiscoverLanguages(_resourcePath);
            var resourceFiles = languages.Select(l => _backend.Reader.Read(l)).ToList();

            var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);
            if (defaultFile == null)
            {
                return StatusCode(500, new ErrorResponse { Error = "No default language file found" });
            }

            // Load configuration
            var (config, _) = Core.Configuration.ConfigurationManager.LoadConfiguration(null, _resourcePath);

            // Get provider
            var provider = TranslationProviderFactory.Create(
                request.Provider ?? "google",
                config
            );

            var results = new List<TranslationResult>();
            var errors = new List<TranslationError>();

            // Get keys to translate
            var keysToTranslate = new List<string>();
            if (request.Keys?.Any() == true)
            {
                keysToTranslate = request.Keys;
            }
            else if (request.OnlyMissing)
            {
                // Get all keys that are missing translations
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

            // Translate each key for each target language
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
