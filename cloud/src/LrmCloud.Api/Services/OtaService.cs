// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Security.Cryptography;
using System.Text;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Ota;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for OTA (Over-The-Air) localization bundle generation.
/// </summary>
public class OtaService : IOtaService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OtaService> _logger;

    public OtaService(AppDbContext db, ILogger<OtaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OtaBundleDto?> GetBundleAsync(
        int projectId,
        string projectPath,
        IEnumerable<string>? languages = null,
        DateTime? since = null,
        CancellationToken ct = default)
    {
        // Get project
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            return null;
        }

        // Build query for resource keys with translations
        // Note: In the database, translations for the default language are stored with
        // LanguageCode = "" (empty string). This is resolved to the project's DefaultLanguage
        // in the response. This convention allows the default language to be changed without
        // updating all translation records.
        var query = _db.ResourceKeys
            .Include(k => k.Translations)
            .Where(k => k.ProjectId == projectId);

        // Track deleted keys for delta updates
        var deletedKeys = new List<string>();

        // Apply delta filter if 'since' is provided
        if (since.HasValue)
        {
            // Get keys deleted since the timestamp
            // Note: We need a SyncHistory query to find deleted keys
            var deletedSince = await GetDeletedKeysSinceAsync(projectId, since.Value, ct);
            deletedKeys.AddRange(deletedSince);

            // Filter to only modified keys
            query = query.Where(k => k.UpdatedAt > since.Value ||
                k.Translations.Any(t => t.UpdatedAt > since.Value));
        }

        // Get all resource keys
        var keys = await query.OrderBy(k => k.KeyName).ToListAsync(ct);

        // Get all unique languages in the project
        var allLanguages = await _db.Translations
            .Where(t => t.ResourceKey!.ProjectId == projectId)
            .Select(t => t.LanguageCode ?? "")
            .Distinct()
            .ToListAsync(ct);

        // Resolve empty string to default language for display
        var languageList = allLanguages
            .Select(l => string.IsNullOrEmpty(l) ? project.DefaultLanguage : l)
            .Distinct()
            .Where(l => !string.IsNullOrEmpty(l))
            .OrderBy(l => l)
            .ToList();

        // Apply language filter if specified
        var languageSet = languages?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build translations by language
        var translations = new Dictionary<string, Dictionary<string, object>>();

        foreach (var key in keys)
        {
            foreach (var translation in key.Translations)
            {
                // Resolve language code
                var langCode = string.IsNullOrEmpty(translation.LanguageCode)
                    ? project.DefaultLanguage
                    : translation.LanguageCode;

                // Skip if language filter is applied and doesn't match
                if (languageSet != null && !languageSet.Contains(langCode))
                {
                    continue;
                }

                // Ensure language dictionary exists
                if (!translations.TryGetValue(langCode, out var langDict))
                {
                    langDict = new Dictionary<string, object>();
                    translations[langCode] = langDict;
                }

                // Handle plural vs regular keys
                if (key.IsPlural && !string.IsNullOrEmpty(translation.PluralForm))
                {
                    // For plural keys, build a dictionary of plural forms
                    if (!langDict.TryGetValue(key.KeyName, out var existing) ||
                        existing is not Dictionary<string, string> pluralDict)
                    {
                        pluralDict = new Dictionary<string, string>();
                        langDict[key.KeyName] = pluralDict;
                    }

                    pluralDict[translation.PluralForm] = translation.Value ?? "";
                }
                else if (!key.IsPlural)
                {
                    // Regular key - just store the value
                    langDict[key.KeyName] = translation.Value ?? "";
                }
            }
        }

        // Get the latest update timestamp for version
        var latestUpdate = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .SelectMany(k => k.Translations)
            .MaxAsync(t => (DateTime?)t.UpdatedAt, ct);

        var keyLatestUpdate = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .MaxAsync(k => (DateTime?)k.UpdatedAt, ct);

        var version = new[] { latestUpdate, keyLatestUpdate, project.UpdatedAt }
            .Where(d => d.HasValue)
            .Max() ?? DateTime.UtcNow;

        // Filter language list by filter if specified
        if (languageSet != null)
        {
            languageList = languageList.Where(l => languageSet.Contains(l)).ToList();
        }

        return new OtaBundleDto
        {
            Version = version.ToString("O"),
            Project = projectPath,
            DefaultLanguage = project.DefaultLanguage,
            Languages = languageList,
            Deleted = deletedKeys,
            Translations = translations
        };
    }

    /// <inheritdoc/>
    public async Task<OtaVersionDto?> GetVersionAsync(int projectId, CancellationToken ct = default)
    {
        // Get project
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null)
        {
            return null;
        }

        // Get the latest update timestamp
        var latestUpdate = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .SelectMany(k => k.Translations)
            .MaxAsync(t => (DateTime?)t.UpdatedAt, ct);

        var keyLatestUpdate = await _db.ResourceKeys
            .Where(k => k.ProjectId == projectId)
            .MaxAsync(k => (DateTime?)k.UpdatedAt, ct);

        var version = new[] { latestUpdate, keyLatestUpdate, project.UpdatedAt }
            .Where(d => d.HasValue)
            .Max() ?? DateTime.UtcNow;

        return new OtaVersionDto
        {
            Version = version.ToString("O")
        };
    }

    /// <inheritdoc/>
    public string ComputeETag(string version)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(version));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Gets keys deleted since a timestamp from sync history.
    /// </summary>
    private async Task<List<string>> GetDeletedKeysSinceAsync(
        int projectId,
        DateTime since,
        CancellationToken ct)
    {
        // Query sync history for entries since the timestamp
        var historyEntries = await _db.SyncHistory
            .Where(h => h.ProjectId == projectId && h.CreatedAt > since && h.ChangesJson != null)
            .Select(h => h.ChangesJson)
            .ToListAsync(ct);

        var deletedKeys = new HashSet<string>();

        foreach (var changesJson in historyEntries)
        {
            if (string.IsNullOrEmpty(changesJson))
                continue;

            try
            {
                var changesData = System.Text.Json.JsonSerializer.Deserialize<LrmCloud.Shared.Entities.SyncChangesData>(
                    changesJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (changesData?.Changes == null)
                    continue;

                foreach (var change in changesData.Changes.Where(c => c.ChangeType == "deleted"))
                {
                    deletedKeys.Add(change.Key);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Skip malformed JSON entries
                continue;
            }
        }

        return deletedKeys.ToList();
    }
}
