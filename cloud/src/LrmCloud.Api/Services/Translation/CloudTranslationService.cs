using System.Diagnostics;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Translation;
using LrmCloud.Shared.Entities;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services.Translation;

/// <summary>
/// Cloud translation service that wraps LocalizationManager.Core translation providers.
/// Handles API key resolution, usage tracking, and caching.
/// </summary>
public class CloudTranslationService : ICloudTranslationService
{
    private readonly AppDbContext _db;
    private readonly IApiKeyHierarchyService _keyHierarchy;
    private readonly ILrmTranslationProvider _lrmProvider;
    private readonly TranslationMemoryService _tmService;
    private readonly GlossaryService _glossaryService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<CloudTranslationService> _logger;

    public CloudTranslationService(
        AppDbContext db,
        IApiKeyHierarchyService keyHierarchy,
        ILrmTranslationProvider lrmProvider,
        TranslationMemoryService tmService,
        GlossaryService glossaryService,
        CloudConfiguration config,
        ILogger<CloudTranslationService> logger)
    {
        _db = db;
        _keyHierarchy = keyHierarchy;
        _lrmProvider = lrmProvider;
        _tmService = tmService;
        _glossaryService = glossaryService;
        _config = config;
        _logger = logger;
    }

    public async Task<List<TranslationProviderDto>> GetAvailableProvidersAsync(
        int? projectId = null,
        int? userId = null,
        int? organizationId = null)
    {
        var result = new List<TranslationProviderDto>();

        // Add LRM provider first (our managed translation service)
        if (_config.LrmProvider.Enabled && userId.HasValue)
        {
            var (available, reason) = await _lrmProvider.IsAvailableAsync(userId.Value);
            var remaining = userId.HasValue ? await _lrmProvider.GetRemainingCharsAsync(userId.Value) : 0;

            result.Add(new TranslationProviderDto
            {
                Name = "lrm",
                DisplayName = "LRM Translation",
                RequiresApiKey = false, // User doesn't need to provide API key
                IsConfigured = available,
                Type = "managed",
                IsManagedProvider = true, // This is our managed service, not free
                IsAiProvider = false,
                Description = available
                    ? $"LRM managed translation ({remaining:N0} chars remaining)"
                    : reason ?? "LRM translation unavailable",
                ApiKeySource = "lrm" // LRM managed provider, not user BYOK
            });
        }

        // Add BYOK providers (user's own API keys)
        var providers = TranslationProviderFactory.GetProviderInfos();
        var configuredProviders = await _keyHierarchy.GetConfiguredProvidersAsync(projectId, userId, organizationId);

        foreach (var provider in providers)
        {
            var isConfigured = configuredProviders.TryGetValue(provider.Name, out var source);

            result.Add(new TranslationProviderDto
            {
                Name = provider.Name,
                DisplayName = provider.DisplayName,
                RequiresApiKey = provider.RequiresApiKey,
                IsConfigured = isConfigured || !provider.RequiresApiKey,
                Type = IsLocalProvider(provider.Name) ? "local" : "api",
                IsAiProvider = IsAiProvider(provider.Name),
                Description = GetProviderDescription(provider.Name),
                ApiKeySource = isConfigured ? source : null
            });
        }

        return result;
    }

    public async Task<TranslateResponseDto> TranslateKeysAsync(
        int projectId,
        int userId,
        TranslateRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new TranslateResponseDto();

        try
        {
            // Get project for organization context
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                response.Errors.Add("Project not found");
                return response;
            }

            // Determine provider
            var providerName = request.Provider ?? await GetBestAvailableProviderAsync(
                projectId, userId, project.OrganizationId);

            if (string.IsNullOrEmpty(providerName))
            {
                response.Errors.Add("No translation provider configured. Please configure an API key.");
                return response;
            }

            // Check if using LRM provider
            var isLrmProvider = providerName.Equals("lrm", StringComparison.OrdinalIgnoreCase);

            ITranslationProvider? provider = null;
            if (!isLrmProvider)
            {
                // Create BYOK provider instance
                provider = await CreateProviderAsync(providerName, projectId, userId, project.OrganizationId);
                if (provider == null)
                {
                    response.Errors.Add($"Failed to initialize provider: {providerName}");
                    return response;
                }
            }

            response.Provider = providerName;

            // Get source language
            var sourceLanguage = request.SourceLanguage ?? project.DefaultLanguage;

            // Pre-fetch glossary entries for all target languages (only for AI providers)
            var glossaryContextByLang = new Dictionary<string, string>();
            var isAiProvider = IsAiProvider(providerName);
            if (isAiProvider)
            {
                foreach (var targetLang in request.TargetLanguages)
                {
                    if (targetLang == sourceLanguage) continue;

                    var entries = await _glossaryService.GetEntriesForLanguagePairAsync(
                        projectId, sourceLanguage, targetLang);

                    if (entries.Any())
                    {
                        var context = _glossaryService.BuildGlossaryContext(entries);
                        if (!string.IsNullOrEmpty(context))
                        {
                            glossaryContextByLang[targetLang] = context;
                        }
                    }
                }
            }

            // Get keys to translate
            var keysQuery = _db.ResourceKeys
                .Include(k => k.Translations)
                .Where(k => k.ProjectId == projectId);

            if (request.Keys.Any())
            {
                keysQuery = keysQuery.Where(k => request.Keys.Contains(k.KeyName));
            }

            var keys = await keysQuery.ToListAsync();

            // Process each key and target language
            foreach (var key in keys)
            {
                // Determine if key is plural: use KeyMetadata from client if provided (for unsaved UI changes),
                // otherwise fall back to database value
                var isPlural = request.KeyMetadata?.TryGetValue(key.KeyName, out var metadata) == true
                    ? metadata.IsPlural
                    : key.IsPlural;

                // Get plural forms to process (empty string for non-plural keys)
                var pluralForms = isPlural
                    ? new[] { "one", "other", "zero", "two", "few", "many" }
                    : new[] { "" };

                foreach (var pluralForm in pluralForms)
                {
                    // Build lookup key for SourceTexts: "keyName" or "keyName:pluralForm"
                    var sourceTextKey = string.IsNullOrEmpty(pluralForm)
                        ? key.KeyName
                        : $"{key.KeyName}:{pluralForm}";

                    // Use provided source text if available, otherwise fall back to database value
                    string? sourceText = null;
                    if (request.SourceTexts?.TryGetValue(sourceTextKey, out var providedText) == true)
                    {
                        sourceText = providedText;
                    }
                    else
                    {
                        var sourceTranslation = key.Translations
                            .FirstOrDefault(t => t.LanguageCode == sourceLanguage && t.PluralForm == pluralForm);
                        sourceText = sourceTranslation?.Value;
                    }

                    // Skip empty source texts (but don't skip the whole key for plurals)
                    if (string.IsNullOrEmpty(sourceText))
                    {
                        continue;
                    }

                    foreach (var targetLang in request.TargetLanguages)
                    {
                        if (targetLang == sourceLanguage)
                        {
                            continue;
                        }

                        var existingTranslation = key.Translations
                            .FirstOrDefault(t => t.LanguageCode == targetLang && t.PluralForm == pluralForm);

                    // Skip if translation exists and we're not overwriting
                    if (!request.Overwrite && existingTranslation != null &&
                        !string.IsNullOrEmpty(existingTranslation.Value))
                    {
                        if (request.OnlyMissing)
                        {
                            response.SkippedCount++;
                            continue;
                        }
                    }

                    var result = new TranslationResultDto
                    {
                        Key = key.KeyName,
                        TargetLanguage = targetLang,
                        PluralForm = pluralForm,
                        SourceText = sourceText
                    };

                    try
                    {
                        string? translatedText = null;
                        bool fromCache = false;
                        bool fromTm = false;

                        // Check Translation Memory first (if enabled)
                        var useTm = request.TranslationMemory?.UseTm ?? true;
                        var minMatchPercent = request.TranslationMemory?.MinMatchPercent ?? 100;

                        if (useTm)
                        {
                            var tmLookup = await _tmService.LookupAsync(userId, new Shared.DTOs.TranslationMemory.TmLookupRequest
                            {
                                SourceText = sourceText,
                                SourceLanguage = sourceLanguage,
                                TargetLanguage = targetLang,
                                MinMatchPercent = minMatchPercent,
                                MaxResults = 1,
                                OrganizationId = project.OrganizationId
                            });

                            if (tmLookup.HasExactMatch || (minMatchPercent < 100 && tmLookup.Matches.Any()))
                            {
                                // Use TM match - no API call needed!
                                var tmMatch = tmLookup.Matches.First();
                                translatedText = tmMatch.TranslatedText;
                                fromCache = true;
                                fromTm = true;

                                // Increment use count - must await to avoid DbContext concurrency issues
                                await _tmService.IncrementUseCountAsync(tmMatch.Id);

                                _logger.LogDebug("TM match ({MatchPercent}%) for {Key} ({SourceLang}->{TargetLang})",
                                    tmMatch.MatchPercent, key.KeyName, sourceLanguage, targetLang);
                            }
                        }

                        // Only call provider if TM didn't have a match
                        if (!fromTm)
                        {
                            if (isLrmProvider)
                            {
                                // Determine billable user for LRM quota check
                                var lrmBillableUserId = await GetBillableUserIdAsync(project, userId);

                                // Use LRM managed provider
                                var lrmResult = await _lrmProvider.TranslateAsync(
                                    lrmBillableUserId,
                                    sourceText,
                                    sourceLanguage,
                                    targetLang,
                                    request.Context ?? key.Comment);

                                if (!lrmResult.Success)
                                {
                                    result.Success = false;
                                    result.Error = lrmResult.Error;
                                    response.FailedCount++;
                                    response.Results.Add(result);
                                    continue;
                                }

                                translatedText = lrmResult.TranslatedText;
                                fromCache = lrmResult.FromCache;
                            }
                            else
                            {
                                // Use BYOK provider
                                // Build context with glossary for AI providers
                                var baseContext = request.Context ?? key.Comment;
                                var contextWithGlossary = baseContext;

                                if (glossaryContextByLang.TryGetValue(targetLang, out var glossaryContext))
                                {
                                    // Prepend glossary context to existing context
                                    contextWithGlossary = string.IsNullOrEmpty(baseContext)
                                        ? glossaryContext
                                        : $"{glossaryContext}\n\n{baseContext}";
                                }

                                var translationRequest = new TranslationRequest
                                {
                                    SourceText = sourceText,
                                    SourceLanguage = sourceLanguage,
                                    TargetLanguage = targetLang,
                                    Context = contextWithGlossary
                                };

                                var translationResponse = await provider!.TranslateAsync(translationRequest);
                                translatedText = translationResponse.TranslatedText;
                                fromCache = translationResponse.FromCache;
                            }
                        }

                        result.TranslatedText = translatedText ?? string.Empty;
                        result.Success = true;
                        result.FromCache = fromCache;
                        result.FromTm = fromTm;

                        // Track TM usage separately
                        if (fromTm)
                        {
                            response.TmCount++;
                        }

                        // Only save to database if requested (default for CLI, skip for UI preview)
                        if (request.SaveToDatabase)
                        {
                            if (existingTranslation != null)
                            {
                                existingTranslation.Value = translatedText;
                                existingTranslation.Status = "translated";
                                existingTranslation.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                key.Translations.Add(new Shared.Entities.Translation
                                {
                                    ResourceKeyId = key.Id,
                                    LanguageCode = targetLang,
                                    PluralForm = pluralForm,
                                    Value = translatedText,
                                    Status = "translated"
                                });
                            }
                        }

                        response.TranslatedCount++;
                        response.CharactersTranslated += sourceText.Length;

                        // Store to Translation Memory (unless it came from TM or storage is disabled)
                        var storeInTm = request.TranslationMemory?.StoreInTm ?? true;
                        if (storeInTm && !fromTm && !string.IsNullOrEmpty(translatedText))
                        {
                            try
                            {
                                await _tmService.StoreAsync(userId, new Shared.DTOs.TranslationMemory.TmStoreRequest
                                {
                                    SourceText = sourceText,
                                    TranslatedText = translatedText,
                                    SourceLanguage = sourceLanguage,
                                    TargetLanguage = targetLang,
                                    Context = $"{project.Name}:{key.KeyName}",
                                    OrganizationId = project.OrganizationId
                                });
                            }
                            catch (Exception tmEx)
                            {
                                // Log but don't fail the translation
                                _logger.LogWarning(tmEx, "Failed to store translation in TM for {Key}", key.KeyName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.Error = ex.Message;
                        response.FailedCount++;
                        _logger.LogWarning(ex, "Translation failed for key {Key} to {Language}",
                            key.KeyName, targetLang);
                    }

                    response.Results.Add(result);
                    }
                }
            }

            // Save changes only if requested
            if (request.SaveToDatabase)
            {
                await _db.SaveChangesAsync();
            }

            // Track usage against billable user (org owner for org projects)
            if (response.CharactersTranslated > 0)
            {
                var billableUserId = await GetBillableUserIdAsync(project, userId);

                if (isLrmProvider)
                {
                    await TrackLrmUsageAsync(billableUserId, response.CharactersTranslated);
                }
                else
                {
                    await TrackByokUsageAsync(billableUserId, response.CharactersTranslated);
                }

                // Log detailed usage event for analytics
                var keySource = project.OrganizationId.HasValue ? "organization" : "user";
                await LogUsageEventAsync(
                    userId,
                    billableUserId,
                    projectId,
                    project.OrganizationId,
                    response.CharactersTranslated,
                    providerName,
                    isLrmProvider,
                    isLrmProvider ? "lrm" : keySource);

                // Track per-provider usage for analytics (against acting user for audit trail)
                await TrackProviderUsageAsync(userId, request.Provider ?? "auto", response.CharactersTranslated, response.TranslatedCount);
            }

            response.Success = response.FailedCount == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation batch failed for project {ProjectId}", projectId);
            response.Errors.Add($"Translation failed: {ex.Message}");
        }

        stopwatch.Stop();
        response.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return response;
    }

    public async Task<TranslateSingleResponseDto> TranslateSingleAsync(
        int userId,
        TranslateSingleRequestDto request,
        int? projectId = null)
    {
        var response = new TranslateSingleResponseDto();

        try
        {
            int? organizationId = null;
            if (projectId.HasValue)
            {
                var project = await _db.Projects.FindAsync(projectId);
                organizationId = project?.OrganizationId;
            }

            var providerName = request.Provider ?? await GetBestAvailableProviderAsync(
                projectId, userId, organizationId);

            if (string.IsNullOrEmpty(providerName))
            {
                response.Error = "No translation provider configured";
                return response;
            }

            var isLrmProvider = providerName.Equals("lrm", StringComparison.OrdinalIgnoreCase);
            response.Provider = providerName;

            // Check Translation Memory first (if enabled)
            var useTm = request.TranslationMemory?.UseTm ?? true;
            var minMatchPercent = request.TranslationMemory?.MinMatchPercent ?? 100;

            if (useTm)
            {
                var tmLookup = await _tmService.LookupAsync(userId, new Shared.DTOs.TranslationMemory.TmLookupRequest
                {
                    SourceText = request.Text,
                    SourceLanguage = request.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    MinMatchPercent = minMatchPercent,
                    MaxResults = 1,
                    OrganizationId = organizationId
                });

                if (tmLookup.HasExactMatch || (minMatchPercent < 100 && tmLookup.Matches.Any()))
                {
                    // Use TM match - no API call needed!
                    var tmMatch = tmLookup.Matches.First();
                    response.Success = true;
                    response.TranslatedText = tmMatch.TranslatedText;
                    response.FromCache = true;
                    response.FromTm = true;

                    // Increment use count (fire and forget)
                    _ = _tmService.IncrementUseCountAsync(tmMatch.Id);

                    _logger.LogDebug("TM match ({MatchPercent}%) for single translation ({SourceLang}->{TargetLang})",
                        tmMatch.MatchPercent, request.SourceLanguage, request.TargetLanguage);

                    return response;
                }
            }

            if (isLrmProvider)
            {
                // Determine billable user for LRM quota check
                int lrmBillableUserId = userId;
                if (projectId.HasValue)
                {
                    var project = await _db.Projects.FindAsync(projectId);
                    if (project != null)
                    {
                        lrmBillableUserId = await GetBillableUserIdAsync(project, userId);
                    }
                }

                // Use LRM managed provider
                var lrmResult = await _lrmProvider.TranslateAsync(
                    lrmBillableUserId,
                    request.Text,
                    request.SourceLanguage,
                    request.TargetLanguage,
                    request.Context);

                if (!lrmResult.Success)
                {
                    response.Error = lrmResult.Error;
                    return response;
                }

                response.Success = true;
                response.TranslatedText = lrmResult.TranslatedText ?? string.Empty;
                response.FromCache = lrmResult.FromCache;
            }
            else
            {
                // Use BYOK provider
                var provider = await CreateProviderAsync(providerName, projectId, userId, organizationId);
                if (provider == null)
                {
                    response.Error = $"Failed to initialize provider: {providerName}";
                    return response;
                }

                // Build context with glossary for AI providers
                var contextWithGlossary = request.Context;
                if (IsAiProvider(providerName) && projectId.HasValue)
                {
                    var entries = await _glossaryService.GetEntriesForLanguagePairAsync(
                        projectId.Value, request.SourceLanguage, request.TargetLanguage);

                    if (entries.Any())
                    {
                        var glossaryContext = _glossaryService.BuildGlossaryContext(entries);
                        if (!string.IsNullOrEmpty(glossaryContext))
                        {
                            contextWithGlossary = string.IsNullOrEmpty(request.Context)
                                ? glossaryContext
                                : $"{glossaryContext}\n\n{request.Context}";
                        }
                    }
                }

                var translationRequest = new TranslationRequest
                {
                    SourceText = request.Text,
                    SourceLanguage = request.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    Context = contextWithGlossary
                };

                var result = await provider.TranslateAsync(translationRequest);

                response.Success = true;
                response.TranslatedText = result.TranslatedText;
                response.FromCache = result.FromCache;

                // Track BYOK usage against billable user
                if (projectId.HasValue)
                {
                    var project = await _db.Projects.FindAsync(projectId);
                    if (project != null)
                    {
                        var billableUserId = await GetBillableUserIdAsync(project, userId);
                        await TrackByokUsageAsync(billableUserId, request.Text.Length);
                    }
                }
                else
                {
                    // No project context - bill acting user
                    await TrackByokUsageAsync(userId, request.Text.Length);
                }
            }

            // Track LRM usage against billable user if we used LRM
            if (isLrmProvider && response.Success)
            {
                if (projectId.HasValue)
                {
                    var project = await _db.Projects.FindAsync(projectId);
                    if (project != null)
                    {
                        var billableUserId = await GetBillableUserIdAsync(project, userId);
                        await TrackLrmUsageAsync(billableUserId, request.Text.Length);
                    }
                }
                else
                {
                    // No project context - bill acting user
                    await TrackLrmUsageAsync(userId, request.Text.Length);
                }
            }

            // Store to Translation Memory if successful (and storage is enabled)
            var storeInTm = request.TranslationMemory?.StoreInTm ?? true;
            if (storeInTm && response.Success && !string.IsNullOrEmpty(response.TranslatedText))
            {
                try
                {
                    await _tmService.StoreAsync(userId, new Shared.DTOs.TranslationMemory.TmStoreRequest
                    {
                        SourceText = request.Text,
                        TranslatedText = response.TranslatedText,
                        SourceLanguage = request.SourceLanguage,
                        TargetLanguage = request.TargetLanguage,
                        Context = request.Context,
                        OrganizationId = organizationId
                    });
                }
                catch (Exception tmEx)
                {
                    // Log but don't fail the translation
                    _logger.LogWarning(tmEx, "Failed to store single translation in TM");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Single translation failed");
            response.Error = ex.Message;
        }

        return response;
    }

    public async Task<TranslationUsageDto> GetUsageAsync(int userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.TranslationCharsUsed,
                u.TranslationCharsLimit,
                u.TranslationCharsResetAt,
                u.OtherCharsUsed,
                u.OtherCharsLimit,
                u.OtherCharsResetAt,
                u.Plan
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new TranslationUsageDto
            {
                Plan = "free",
                CharactersUsed = 0,
                CharacterLimit = _config.Limits.FreeTranslationChars,
                OtherCharactersUsed = 0,
                OtherCharacterLimit = _config.Limits.FreeOtherChars
            };
        }

        return new TranslationUsageDto
        {
            // LRM usage (counts against plan)
            CharactersUsed = user.TranslationCharsUsed,
            CharacterLimit = user.TranslationCharsLimit,
            ResetsAt = user.TranslationCharsResetAt,
            Plan = user.Plan,
            // Other providers usage (BYOK + free community)
            OtherCharactersUsed = user.OtherCharsUsed,
            OtherCharacterLimit = user.OtherCharsLimit,
            OtherResetsAt = user.OtherCharsResetAt
        };
    }

    public async Task<List<ProviderUsageDto>> GetUsageByProviderAsync(int userId)
    {
        // Get current month period
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var usageRecords = await _db.TranslationUsageHistory
            .Where(h => h.UserId == userId && h.PeriodStart == periodStart)
            .OrderByDescending(h => h.CharsUsed)
            .ToListAsync();

        return usageRecords.Select(r => new ProviderUsageDto
        {
            ProviderName = r.ProviderName,
            CharactersUsed = r.CharsUsed,
            ApiCalls = r.ApiCalls,
            LastUsedAt = r.LastUsedAt
        }).ToList();
    }

    private async Task<string?> GetBestAvailableProviderAsync(
        int? projectId, int userId, int? organizationId)
    {
        // Determine billable user for LRM quota check
        int? billableUserId = userId;
        if (organizationId.HasValue)
        {
            var org = await _db.Organizations.FindAsync(organizationId.Value);
            if (org != null)
            {
                billableUserId = org.OwnerId;
            }
        }

        // Check LRM provider first (preferred - our managed service)
        if (_config.LrmProvider.Enabled && billableUserId.HasValue)
        {
            var (available, _) = await _lrmProvider.IsAvailableAsync(billableUserId.Value);
            if (available)
            {
                return "lrm";
            }
        }

        // Fallback to BYOK providers
        var preferredOrder = new[]
        {
            "mymemory",  // Free, no API key needed
            "lingva",    // Free, no API key needed
            "google",    // Best quality
            "deepl",     // High quality
            "claude",    // AI, good quality
            "openai",    // AI
            "azuretranslator",
            "azureopenai",
            "libretranslate",
            "ollama"     // Local, needs setup
        };

        // For org projects, don't include user's personal API keys - only use org/project keys
        // This ensures org activity uses org resources, not personal resources
        int? userIdForKeyLookup = organizationId.HasValue ? null : userId;

        var configuredProviders = await _keyHierarchy.GetConfiguredProvidersAsync(
            projectId, userIdForKeyLookup, organizationId);

        // Return first available provider from priority list
        foreach (var provider in preferredOrder)
        {
            var info = TranslationProviderFactory.GetProviderInfos()
                .FirstOrDefault(p => p.Name == provider);

            if (info == null)
            {
                continue;
            }

            // Provider doesn't require API key, or has one configured
            if (!info.RequiresApiKey || configuredProviders.ContainsKey(provider))
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines who should be billed for translation on a project.
    /// If project belongs to an organization, bills the org owner.
    /// Otherwise, bills the acting user.
    /// </summary>
    private async Task<int> GetBillableUserIdAsync(Project project, int actingUserId)
    {
        // If project belongs to org → bill org owner
        if (project.OrganizationId.HasValue)
        {
            var org = await _db.Organizations.FindAsync(project.OrganizationId.Value);
            if (org != null)
            {
                _logger.LogDebug(
                    "Billing org owner {OwnerId} instead of acting user {ActingUserId} for org project {ProjectId}",
                    org.OwnerId, actingUserId, project.Id);
                return org.OwnerId;
            }
        }

        // Personal project → bill acting user
        return actingUserId;
    }

    private async Task TrackLrmUsageAsync(int billableUserId, long charsUsed)
    {
        var user = await _db.Users.FindAsync(billableUserId);
        if (user == null)
        {
            return;
        }

        user.TranslationCharsUsed += (int)charsUsed;
        user.UpdatedAt = DateTime.UtcNow;

        // Reset usage if reset date has passed
        if (user.TranslationCharsResetAt.HasValue && user.TranslationCharsResetAt < DateTime.UtcNow)
        {
            user.TranslationCharsUsed = (int)charsUsed; // Reset and add current
            user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
        }
        else if (!user.TranslationCharsResetAt.HasValue)
        {
            // Set initial reset date
            user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
        }

        await _db.SaveChangesAsync();
        _logger.LogDebug("LRM usage tracked: {Chars} chars for user {UserId}", charsUsed, billableUserId);
    }

    private async Task TrackByokUsageAsync(int billableUserId, long charsUsed)
    {
        var user = await _db.Users.FindAsync(billableUserId);
        if (user == null)
        {
            return;
        }

        user.OtherCharsUsed += charsUsed;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogDebug("BYOK usage tracked: {Chars} chars for user {UserId}", charsUsed, billableUserId);
    }

    /// <summary>
    /// Logs a detailed usage event for analytics and billing breakdown.
    /// </summary>
    private async Task LogUsageEventAsync(
        int actingUserId,
        int billableUserId,
        int? projectId,
        int? organizationId,
        long charsUsed,
        string provider,
        bool isLrm,
        string keySource)
    {
        try
        {
            var usageEvent = new UsageEvent
            {
                ActingUserId = actingUserId,
                BilledUserId = billableUserId,
                ProjectId = projectId,
                OrganizationId = organizationId,
                CharactersUsed = charsUsed,
                Provider = provider,
                IsLrmProvider = isLrm,
                KeySource = keySource,
                CreatedAt = DateTime.UtcNow
            };

            _db.UsageEvents.Add(usageEvent);
            await _db.SaveChangesAsync();

            _logger.LogDebug(
                "Usage event logged: {Chars} chars via {Provider} (acting: {ActingUserId}, billed: {BilledUserId}, project: {ProjectId}, org: {OrgId})",
                charsUsed, provider, actingUserId, billableUserId, projectId, organizationId);
        }
        catch (Exception ex)
        {
            // Don't fail the translation if event logging fails
            _logger.LogWarning(ex, "Failed to log usage event");
        }
    }

    private async Task TrackProviderUsageAsync(int userId, string providerName, long charsUsed, int apiCalls)
    {
        try
        {
            // Get current month period
            var now = DateTime.UtcNow;
            var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = periodStart.AddMonths(1).AddTicks(-1);

            // Find or create usage record for this user+provider+period
            var usage = await _db.TranslationUsageHistory
                .FirstOrDefaultAsync(h =>
                    h.UserId == userId &&
                    h.ProviderName == providerName &&
                    h.PeriodStart == periodStart);

            if (usage == null)
            {
                usage = new Shared.Entities.TranslationUsageHistory
                {
                    UserId = userId,
                    ProviderName = providerName,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    CharsUsed = 0,
                    ApiCalls = 0
                };
                _db.TranslationUsageHistory.Add(usage);
            }

            usage.CharsUsed += charsUsed;
            usage.ApiCalls += apiCalls;
            usage.LastUsedAt = now;

            await _db.SaveChangesAsync();

            _logger.LogDebug(
                "Provider usage tracked: {Provider} - {Chars} chars, {Calls} calls for user {UserId}",
                providerName, charsUsed, apiCalls, userId);
        }
        catch (Exception ex)
        {
            // Don't fail the translation if usage tracking fails
            _logger.LogWarning(ex, "Failed to track provider usage for {Provider}, user {UserId}", providerName, userId);
        }
    }

    private async Task<(bool allowed, string? reason)> CheckOtherLimitAsync(int userId, long charsToUse)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.OtherCharsUsed, u.OtherCharsLimit, u.Plan })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return (false, "User not found");
        }

        // Enterprise has unlimited
        if (user.Plan.Equals("enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        var remaining = user.OtherCharsLimit - user.OtherCharsUsed;
        if (remaining <= 0)
        {
            return (false, $"Other providers limit reached ({user.OtherCharsLimit:N0} chars/month). Upgrade your plan for more.");
        }

        if (charsToUse > remaining)
        {
            return (false, $"Request exceeds remaining limit ({remaining:N0} chars remaining)");
        }

        return (true, null);
    }

    private async Task<ITranslationProvider?> CreateProviderAsync(
        string providerName,
        int? projectId,
        int? userId,
        int? organizationId)
    {
        try
        {
            // For org projects, don't include user's personal API keys - only use org/project keys
            // This ensures org activity uses org resources, not personal resources
            int? userIdForKeyLookup = organizationId.HasValue ? null : userId;

            // Resolve API key and config from hierarchy
            var resolved = await _keyHierarchy.ResolveProviderConfigAsync(
                providerName, projectId, userIdForKeyLookup, organizationId);

            // Get the actual API key (not masked) for the provider
            var (apiKey, _) = await _keyHierarchy.ResolveApiKeyAsync(
                providerName, projectId, userIdForKeyLookup, organizationId);

            // Create config model with merged provider settings
            var config = new ConfigurationModel
            {
                Translation = new TranslationConfiguration
                {
                    DefaultProvider = providerName
                }
            };

            // Set the API key and apply provider-specific configuration
            ApplyProviderConfig(config, providerName, apiKey, resolved.Config);

            return TranslationProviderFactory.Create(providerName, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create provider {Provider}", providerName);
            return null;
        }
    }

    /// <summary>
    /// Applies the resolved API key and provider-specific configuration to the config model.
    /// </summary>
    private static void ApplyProviderConfig(
        ConfigurationModel config,
        string provider,
        string? apiKey,
        Dictionary<string, object?>? providerConfig)
    {
        config.Translation ??= new TranslationConfiguration();
        config.Translation.ApiKeys ??= new TranslationApiKeys();
        config.Translation.AIProviders ??= new AIProviderConfiguration();

        switch (provider.ToLowerInvariant())
        {
            case "google":
                config.Translation.ApiKeys.Google = apiKey;
                // Google doesn't have additional config currently
                break;

            case "deepl":
                config.Translation.ApiKeys.DeepL = apiKey;
                // DeepL doesn't have additional config currently
                break;

            case "openai":
                config.Translation.ApiKeys.OpenAI = apiKey;
                config.Translation.AIProviders.OpenAI = new OpenAISettings
                {
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "claude":
                config.Translation.ApiKeys.Claude = apiKey;
                config.Translation.AIProviders.Claude = new ClaudeSettings
                {
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "azureopenai":
                config.Translation.ApiKeys.AzureOpenAI = apiKey;
                config.Translation.AIProviders.AzureOpenAI = new AzureOpenAISettings
                {
                    Endpoint = GetConfigString(providerConfig, "endpoint"),
                    DeploymentName = GetConfigString(providerConfig, "deploymentName"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "azuretranslator":
                config.Translation.ApiKeys.AzureTranslator = apiKey;
                config.Translation.AIProviders.AzureTranslator = new AzureTranslatorSettings
                {
                    Region = GetConfigString(providerConfig, "region"),
                    Endpoint = GetConfigString(providerConfig, "endpoint"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "ollama":
                // Ollama doesn't require API key but has config
                config.Translation.AIProviders.Ollama = new OllamaSettings
                {
                    ApiUrl = GetConfigString(providerConfig, "apiUrl"),
                    Model = GetConfigString(providerConfig, "model"),
                    CustomSystemPrompt = GetConfigString(providerConfig, "customSystemPrompt"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "lingva":
                // Lingva doesn't require API key but has config
                config.Translation.AIProviders.Lingva = new LingvaSettings
                {
                    InstanceUrl = GetConfigString(providerConfig, "instanceUrl"),
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;

            case "libretranslate":
                config.Translation.ApiKeys.LibreTranslate = apiKey;
                // LibreTranslate config would be apiUrl but that's not in current settings
                // TODO: Add LibreTranslateSettings to Core if needed
                break;

            case "mymemory":
                // MyMemory is free and has optional email config
                config.Translation.AIProviders.MyMemory = new MyMemorySettings
                {
                    RateLimitPerMinute = GetConfigInt(providerConfig, "rateLimitPerMinute")
                };
                break;
        }
    }

    /// <summary>
    /// Gets a string value from the provider config dictionary.
    /// </summary>
    private static string? GetConfigString(Dictionary<string, object?>? config, string key)
    {
        if (config == null)
        {
            return null;
        }

        // Try exact key first
        if (config.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }

        // Try case-insensitive
        var matchingKey = config.Keys.FirstOrDefault(k =>
            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

        if (matchingKey != null && config[matchingKey] != null)
        {
            return config[matchingKey]?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets an integer value from the provider config dictionary.
    /// </summary>
    private static int? GetConfigInt(Dictionary<string, object?>? config, string key)
    {
        var strValue = GetConfigString(config, key);
        if (strValue != null && int.TryParse(strValue, out var intValue))
        {
            return intValue;
        }
        return null;
    }

    private static bool IsLocalProvider(string provider) =>
        provider is "ollama" or "libretranslate";

    private static bool IsAiProvider(string provider) =>
        provider is "openai" or "claude" or "azureopenai" or "ollama";

    private static string? GetProviderDescription(string provider) => provider switch
    {
        "google" => "Google Cloud Translation API with excellent language coverage",
        "deepl" => "High-quality neural machine translation, great for European languages",
        "openai" => "GPT models for context-aware translation",
        "claude" => "Anthropic's Claude for nuanced, context-aware translation",
        "azuretranslator" => "Microsoft Azure Translator with enterprise features",
        "azureopenai" => "Azure-hosted OpenAI models",
        "lingva" => "Free Google Translate proxy (no API key needed)",
        "mymemory" => "Free translation memory service (no API key needed)",
        "libretranslate" => "Self-hosted open source translation",
        "ollama" => "Local LLM for private, offline translation",
        _ => null
    };
}
