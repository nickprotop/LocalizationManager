// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core;
using LocalizationManager.Core.Translation;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly string _resourcePath;
    private readonly ResourceFileParser _parser;
    private readonly ResourceDiscovery _discovery;

    public TranslationController(IConfiguration configuration)
    {
        _resourcePath = configuration["ResourcePath"] ?? Directory.GetCurrentDirectory();
        _parser = new ResourceFileParser();
        _discovery = new ResourceDiscovery();
    }

    /// <summary>
    /// Get list of available translation providers
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<object> GetProviders()
    {
        var providers = new[]
        {
            new { name = "google", displayName = "Google Cloud Translation", requiresApiKey = true },
            new { name = "deepl", displayName = "DeepL", requiresApiKey = true },
            new { name = "libretranslate", displayName = "LibreTranslate", requiresApiKey = false },
            new { name = "ollama", displayName = "Ollama (Local LLM)", requiresApiKey = false },
            new { name = "openai", displayName = "OpenAI", requiresApiKey = true },
            new { name = "claude", displayName = "Anthropic Claude", requiresApiKey = true },
            new { name = "azureopenai", displayName = "Azure OpenAI", requiresApiKey = true },
            new { name = "azuretranslator", displayName = "Azure Translator", requiresApiKey = true }
        };

        return Ok(new { providers });
    }

    /// <summary>
    /// Translate keys using specified provider
    /// </summary>
    [HttpPost("translate")]
    public async Task<ActionResult<object>> Translate([FromBody] TranslateRequest request)
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

            // Load configuration
            var (config, _) = Core.Configuration.ConfigurationManager.LoadConfiguration(null, _resourcePath);

            // Get provider
            var provider = TranslationProviderFactory.Create(
                request.Provider ?? "google",
                config
            );

            var results = new List<object>();
            var errors = new List<object>();

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

                        results.Add(new
                        {
                            key,
                            language = targetLang,
                            translatedValue = translatedText,
                            success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new
                        {
                            key,
                            language = targetLang,
                            error = ex.Message
                        });
                    }
                }

                // Save the file if not dry run
                if (!request.DryRun)
                {
                    _parser.Write(targetFile);
                }
            }

            return Ok(new
            {
                success = true,
                translatedCount = results.Count,
                errorCount = errors.Count,
                results,
                errors,
                dryRun = request.DryRun
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class TranslateRequest
{
    public string? Provider { get; set; }
    public string? SourceLanguage { get; set; }
    public List<string>? TargetLanguages { get; set; }
    public List<string>? Keys { get; set; }
    public bool OnlyMissing { get; set; }
    public bool DryRun { get; set; }
}
