using System.Security.Cryptography;
using System.Text;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.TranslationMemory;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for Translation Memory (TM) operations.
/// Provides exact and fuzzy matching for translation reuse.
/// </summary>
public class TranslationMemoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TranslationMemoryService> _logger;

    public TranslationMemoryService(AppDbContext db, ILogger<TranslationMemoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Looks up TM matches for a source text.
    /// Returns exact matches first, then fuzzy matches.
    /// </summary>
    public async Task<TmLookupResponse> LookupAsync(int userId, TmLookupRequest request)
    {
        var response = new TmLookupResponse();
        var sourceHash = ComputeHash(NormalizeText(request.SourceText));

        // First, try exact match
        var exactMatch = await _db.TranslationMemories
            .Where(tm => tm.UserId == userId
                && tm.SourceLanguage == request.SourceLanguage
                && tm.TargetLanguage == request.TargetLanguage
                && tm.SourceHash == sourceHash)
            .FirstOrDefaultAsync();

        // Also check organization TM if specified
        if (exactMatch == null && request.OrganizationId.HasValue)
        {
            exactMatch = await _db.TranslationMemories
                .Where(tm => tm.OrganizationId == request.OrganizationId
                    && tm.SourceLanguage == request.SourceLanguage
                    && tm.TargetLanguage == request.TargetLanguage
                    && tm.SourceHash == sourceHash)
                .FirstOrDefaultAsync();
        }

        if (exactMatch != null)
        {
            response.Matches.Add(new TmMatchDto
            {
                Id = exactMatch.Id,
                SourceText = exactMatch.SourceText,
                TranslatedText = exactMatch.TranslatedText,
                SourceLanguage = exactMatch.SourceLanguage,
                TargetLanguage = exactMatch.TargetLanguage,
                MatchPercent = 100,
                UseCount = exactMatch.UseCount,
                Context = exactMatch.Context,
                UpdatedAt = exactMatch.UpdatedAt
            });

            // Return early if exact match found (most common case)
            return response;
        }

        // Fuzzy matching: get recent entries for this language pair and compare
        var candidates = await _db.TranslationMemories
            .Where(tm => (tm.UserId == userId || tm.OrganizationId == request.OrganizationId)
                && tm.SourceLanguage == request.SourceLanguage
                && tm.TargetLanguage == request.TargetLanguage)
            .OrderByDescending(tm => tm.UseCount)
            .ThenByDescending(tm => tm.UpdatedAt)
            .Take(500) // Limit candidates for performance
            .ToListAsync();

        var normalizedSource = NormalizeText(request.SourceText);
        var fuzzyMatches = candidates
            .Select(tm => new
            {
                Entry = tm,
                MatchPercent = CalculateSimilarity(normalizedSource, NormalizeText(tm.SourceText))
            })
            .Where(m => m.MatchPercent >= request.MinMatchPercent)
            .OrderByDescending(m => m.MatchPercent)
            .ThenByDescending(m => m.Entry.UseCount)
            .Take(request.MaxResults)
            .ToList();

        foreach (var match in fuzzyMatches)
        {
            response.Matches.Add(new TmMatchDto
            {
                Id = match.Entry.Id,
                SourceText = match.Entry.SourceText,
                TranslatedText = match.Entry.TranslatedText,
                SourceLanguage = match.Entry.SourceLanguage,
                TargetLanguage = match.Entry.TargetLanguage,
                MatchPercent = match.MatchPercent,
                UseCount = match.Entry.UseCount,
                Context = match.Entry.Context,
                UpdatedAt = match.Entry.UpdatedAt
            });
        }

        return response;
    }

    /// <summary>
    /// Stores a translation in TM. Updates existing entry if found.
    /// </summary>
    public async Task<TranslationMemory> StoreAsync(int userId, TmStoreRequest request)
    {
        var normalizedSource = NormalizeText(request.SourceText);
        var sourceHash = ComputeHash(normalizedSource);

        // Check for existing entry
        var existing = await _db.TranslationMemories
            .FirstOrDefaultAsync(tm => tm.UserId == userId
                && tm.SourceLanguage == request.SourceLanguage
                && tm.TargetLanguage == request.TargetLanguage
                && tm.SourceHash == sourceHash);

        if (existing != null)
        {
            // Update existing entry
            existing.TranslatedText = request.TranslatedText;
            existing.UseCount++;
            existing.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Context))
            {
                existing.Context = request.Context;
            }

            await _db.SaveChangesAsync();
            _logger.LogDebug("Updated TM entry {Id} for {SourceLang}->{TargetLang}",
                existing.Id, request.SourceLanguage, request.TargetLanguage);
            return existing;
        }

        // Create new entry
        var entry = new TranslationMemory
        {
            UserId = userId,
            OrganizationId = request.OrganizationId,
            SourceLanguage = request.SourceLanguage,
            TargetLanguage = request.TargetLanguage,
            SourceText = request.SourceText,
            TranslatedText = request.TranslatedText,
            SourceHash = sourceHash,
            Context = request.Context,
            UseCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TranslationMemories.Add(entry);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Created TM entry {Id} for {SourceLang}->{TargetLang}",
            entry.Id, request.SourceLanguage, request.TargetLanguage);
        return entry;
    }

    /// <summary>
    /// Batch store multiple translations in TM (e.g., after a translation job).
    /// </summary>
    public async Task StoreBatchAsync(int userId, IEnumerable<TmStoreRequest> requests)
    {
        foreach (var request in requests)
        {
            await StoreAsync(userId, request);
        }
    }

    /// <summary>
    /// Increments use count for a TM entry (when user accepts a suggestion).
    /// </summary>
    public async Task IncrementUseCountAsync(int tmEntryId)
    {
        var entry = await _db.TranslationMemories.FindAsync(tmEntryId);
        if (entry != null)
        {
            entry.UseCount++;
            entry.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets TM statistics for a user.
    /// </summary>
    public async Task<TmStatsDto> GetStatsAsync(int userId, int? organizationId = null)
    {
        var query = _db.TranslationMemories
            .Where(tm => tm.UserId == userId || tm.OrganizationId == organizationId);

        var totalEntries = await query.CountAsync();
        var totalUseCount = await query.SumAsync(tm => tm.UseCount);

        var languagePairs = await query
            .GroupBy(tm => new { tm.SourceLanguage, tm.TargetLanguage })
            .Select(g => new TmLanguagePairStats
            {
                SourceLanguage = g.Key.SourceLanguage,
                TargetLanguage = g.Key.TargetLanguage,
                EntryCount = g.Count(),
                UseCount = g.Sum(tm => tm.UseCount)
            })
            .OrderByDescending(lp => lp.EntryCount)
            .ToListAsync();

        return new TmStatsDto
        {
            TotalEntries = totalEntries,
            TotalUseCount = totalUseCount,
            LanguagePairs = languagePairs
        };
    }

    /// <summary>
    /// Deletes a specific TM entry.
    /// </summary>
    public async Task<bool> DeleteAsync(int userId, int tmEntryId)
    {
        var entry = await _db.TranslationMemories
            .FirstOrDefaultAsync(tm => tm.Id == tmEntryId && tm.UserId == userId);

        if (entry == null)
            return false;

        _db.TranslationMemories.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Clears all TM entries for a user (optional: for specific language pair).
    /// </summary>
    public async Task<int> ClearAsync(int userId, string? sourceLanguage = null, string? targetLanguage = null)
    {
        var query = _db.TranslationMemories.Where(tm => tm.UserId == userId);

        if (!string.IsNullOrEmpty(sourceLanguage))
            query = query.Where(tm => tm.SourceLanguage == sourceLanguage);

        if (!string.IsNullOrEmpty(targetLanguage))
            query = query.Where(tm => tm.TargetLanguage == targetLanguage);

        var entries = await query.ToListAsync();
        _db.TranslationMemories.RemoveRange(entries);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cleared {Count} TM entries for user {UserId}", entries.Count, userId);
        return entries.Count;
    }

    /// <summary>
    /// Normalizes text for comparison (lowercase, trim, normalize whitespace).
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Normalize whitespace and trim
        var normalized = string.Join(" ", text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries));

        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of text for exact match lookup.
    /// </summary>
    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates similarity percentage between two strings using Levenshtein distance.
    /// </summary>
    private static int CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
            return 100;
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0;

        var distance = LevenshteinDistance(source, target);
        var maxLen = Math.Max(source.Length, target.Length);

        if (maxLen == 0)
            return 100;

        return (int)Math.Round((1.0 - (double)distance / maxLen) * 100);
    }

    /// <summary>
    /// Computes Levenshtein edit distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        var sourceLen = source.Length;
        var targetLen = target.Length;

        // Optimize for common cases
        if (sourceLen == 0) return targetLen;
        if (targetLen == 0) return sourceLen;
        if (source == target) return 0;

        // Use single array optimization
        var prevRow = new int[targetLen + 1];
        var currRow = new int[targetLen + 1];

        // Initialize first row
        for (var j = 0; j <= targetLen; j++)
            prevRow[j] = j;

        for (var i = 1; i <= sourceLen; i++)
        {
            currRow[0] = i;

            for (var j = 1; j <= targetLen; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currRow[j] = Math.Min(
                    Math.Min(currRow[j - 1] + 1, prevRow[j] + 1),
                    prevRow[j - 1] + cost);
            }

            // Swap rows
            (prevRow, currRow) = (currRow, prevRow);
        }

        return prevRow[targetLen];
    }
}
