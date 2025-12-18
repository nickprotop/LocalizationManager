using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Glossary;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for managing glossary terms and translations.
/// </summary>
public class GlossaryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<GlossaryService> _logger;

    public GlossaryService(AppDbContext db, ILogger<GlossaryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Project-Level Glossary

    /// <summary>
    /// Get all glossary terms for a project, including inherited organization terms.
    /// </summary>
    public async Task<GlossaryListResponse> GetProjectGlossaryAsync(int projectId, bool includeInherited = true)
    {
        // Get the project to find its organization
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            throw new ArgumentException("Project not found", nameof(projectId));

        var query = _db.GlossaryTerms
            .Include(t => t.Translations)
            .Include(t => t.Creator)
            .Where(t => t.ProjectId == projectId);

        // Include organization-level terms if project has opted-in to inherit
        if (includeInherited && project.InheritOrganizationGlossary && project.OrganizationId.HasValue)
        {
            query = _db.GlossaryTerms
                .Include(t => t.Translations)
                .Include(t => t.Creator)
                .Where(t => t.ProjectId == projectId || t.OrganizationId == project.OrganizationId);
        }

        var terms = await query.OrderBy(t => t.SourceTerm).ToListAsync();

        // Get all unique languages
        var languages = terms
            .SelectMany(t => t.Translations.Select(tr => tr.TargetLanguage))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        // Add source languages
        var sourceLanguages = terms.Select(t => t.SourceLanguage).Distinct();
        languages = languages.Union(sourceLanguages).Distinct().OrderBy(l => l).ToList();

        var projectTermsCount = terms.Count(t => t.ProjectId == projectId);
        var inheritedCount = terms.Count(t => t.OrganizationId.HasValue);

        return new GlossaryListResponse
        {
            Terms = terms.Select(MapToDto).ToList(),
            Languages = languages,
            TotalCount = terms.Count,
            ProjectTermsCount = projectTermsCount,
            InheritedTermsCount = inheritedCount
        };
    }

    /// <summary>
    /// Create a project-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto> CreateProjectTermAsync(int projectId, int userId, CreateGlossaryTermRequest request)
    {
        // Check for duplicate
        var exists = await _db.GlossaryTerms
            .AnyAsync(t => t.ProjectId == projectId &&
                          t.SourceTerm == request.SourceTerm &&
                          t.SourceLanguage == request.SourceLanguage);

        if (exists)
            throw new InvalidOperationException($"Term '{request.SourceTerm}' already exists for language '{request.SourceLanguage}'");

        var term = new GlossaryTerm
        {
            ProjectId = projectId,
            OrganizationId = null,
            SourceTerm = request.SourceTerm,
            SourceLanguage = request.SourceLanguage,
            Description = request.Description,
            CaseSensitive = request.CaseSensitive,
            CreatedBy = userId
        };

        _db.GlossaryTerms.Add(term);
        await _db.SaveChangesAsync();

        // Add translations
        foreach (var (lang, translation) in request.Translations)
        {
            var glossaryTranslation = new GlossaryTranslation
            {
                TermId = term.Id,
                TargetLanguage = lang,
                TranslatedTerm = translation
            };
            _db.GlossaryTranslations.Add(glossaryTranslation);
        }

        await _db.SaveChangesAsync();

        // Reload with translations
        await _db.Entry(term).Collection(t => t.Translations).LoadAsync();
        await _db.Entry(term).Reference(t => t.Creator).LoadAsync();

        _logger.LogInformation("Created glossary term '{Term}' for project {ProjectId}", request.SourceTerm, projectId);

        return MapToDto(term);
    }

    #endregion

    #region Organization-Level Glossary

    /// <summary>
    /// Get all glossary terms for an organization.
    /// </summary>
    public async Task<GlossaryListResponse> GetOrganizationGlossaryAsync(int organizationId)
    {
        var terms = await _db.GlossaryTerms
            .Include(t => t.Translations)
            .Include(t => t.Creator)
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.SourceTerm)
            .ToListAsync();

        var languages = terms
            .SelectMany(t => t.Translations.Select(tr => tr.TargetLanguage))
            .Union(terms.Select(t => t.SourceLanguage))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        return new GlossaryListResponse
        {
            Terms = terms.Select(MapToDto).ToList(),
            Languages = languages,
            TotalCount = terms.Count,
            ProjectTermsCount = 0,
            InheritedTermsCount = 0
        };
    }

    /// <summary>
    /// Create an organization-level glossary term.
    /// </summary>
    public async Task<GlossaryTermDto> CreateOrganizationTermAsync(int organizationId, int userId, CreateGlossaryTermRequest request)
    {
        // Check for duplicate
        var exists = await _db.GlossaryTerms
            .AnyAsync(t => t.OrganizationId == organizationId &&
                          t.SourceTerm == request.SourceTerm &&
                          t.SourceLanguage == request.SourceLanguage);

        if (exists)
            throw new InvalidOperationException($"Term '{request.SourceTerm}' already exists for language '{request.SourceLanguage}'");

        var term = new GlossaryTerm
        {
            ProjectId = null,
            OrganizationId = organizationId,
            SourceTerm = request.SourceTerm,
            SourceLanguage = request.SourceLanguage,
            Description = request.Description,
            CaseSensitive = request.CaseSensitive,
            CreatedBy = userId
        };

        _db.GlossaryTerms.Add(term);
        await _db.SaveChangesAsync();

        // Add translations
        foreach (var (lang, translation) in request.Translations)
        {
            var glossaryTranslation = new GlossaryTranslation
            {
                TermId = term.Id,
                TargetLanguage = lang,
                TranslatedTerm = translation
            };
            _db.GlossaryTranslations.Add(glossaryTranslation);
        }

        await _db.SaveChangesAsync();

        // Reload with translations
        await _db.Entry(term).Collection(t => t.Translations).LoadAsync();
        await _db.Entry(term).Reference(t => t.Creator).LoadAsync();

        _logger.LogInformation("Created glossary term '{Term}' for organization {OrganizationId}", request.SourceTerm, organizationId);

        return MapToDto(term);
    }

    #endregion

    #region Common Operations

    /// <summary>
    /// Get a single term by ID.
    /// </summary>
    public async Task<GlossaryTermDto?> GetTermAsync(int termId)
    {
        var term = await _db.GlossaryTerms
            .Include(t => t.Translations)
            .Include(t => t.Creator)
            .FirstOrDefaultAsync(t => t.Id == termId);

        return term == null ? null : MapToDto(term);
    }

    /// <summary>
    /// Update a glossary term.
    /// </summary>
    public async Task<GlossaryTermDto> UpdateTermAsync(int termId, UpdateGlossaryTermRequest request)
    {
        var term = await _db.GlossaryTerms
            .Include(t => t.Translations)
            .FirstOrDefaultAsync(t => t.Id == termId);

        if (term == null)
            throw new ArgumentException("Term not found", nameof(termId));

        // Check for duplicate (if source term changed)
        if (term.SourceTerm != request.SourceTerm || term.SourceLanguage != request.SourceLanguage)
        {
            var exists = await _db.GlossaryTerms
                .AnyAsync(t => t.Id != termId &&
                              ((t.ProjectId == term.ProjectId && term.ProjectId.HasValue) ||
                               (t.OrganizationId == term.OrganizationId && term.OrganizationId.HasValue)) &&
                              t.SourceTerm == request.SourceTerm &&
                              t.SourceLanguage == request.SourceLanguage);

            if (exists)
                throw new InvalidOperationException($"Term '{request.SourceTerm}' already exists for language '{request.SourceLanguage}'");
        }

        term.SourceTerm = request.SourceTerm;
        term.SourceLanguage = request.SourceLanguage;
        term.Description = request.Description;
        term.CaseSensitive = request.CaseSensitive;

        // Remove existing translations
        _db.GlossaryTranslations.RemoveRange(term.Translations);

        // Add new translations
        foreach (var (lang, translation) in request.Translations)
        {
            var glossaryTranslation = new GlossaryTranslation
            {
                TermId = term.Id,
                TargetLanguage = lang,
                TranslatedTerm = translation
            };
            _db.GlossaryTranslations.Add(glossaryTranslation);
        }

        await _db.SaveChangesAsync();

        // Reload with new translations
        await _db.Entry(term).Collection(t => t.Translations).LoadAsync();
        await _db.Entry(term).Reference(t => t.Creator).LoadAsync();

        _logger.LogInformation("Updated glossary term {TermId}", termId);

        return MapToDto(term);
    }

    /// <summary>
    /// Delete a glossary term.
    /// </summary>
    public async Task DeleteTermAsync(int termId)
    {
        var term = await _db.GlossaryTerms.FindAsync(termId);
        if (term == null)
            throw new ArgumentException("Term not found", nameof(termId));

        _db.GlossaryTerms.Remove(term);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted glossary term {TermId}", termId);
    }

    #endregion

    #region Translation Flow Support

    /// <summary>
    /// Get glossary entries for a specific language pair.
    /// Returns both project-level and inherited organization terms.
    /// Project terms override organization terms with the same source.
    /// </summary>
    public async Task<List<GlossaryEntryDto>> GetEntriesForLanguagePairAsync(
        int projectId,
        string sourceLanguage,
        string targetLanguage)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
            return new List<GlossaryEntryDto>();

        // Get all matching terms (project + org if inheritance enabled)
        var query = _db.GlossaryTerms
            .Include(t => t.Translations)
            .Where(t => t.SourceLanguage == sourceLanguage);

        if (project.InheritOrganizationGlossary && project.OrganizationId.HasValue)
        {
            query = query.Where(t => t.ProjectId == projectId || t.OrganizationId == project.OrganizationId);
        }
        else
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        var terms = await query.ToListAsync();

        // Build entries, with project terms overriding org terms
        var entries = new Dictionary<string, GlossaryEntryDto>(StringComparer.OrdinalIgnoreCase);

        // First add org terms
        foreach (var term in terms.Where(t => t.OrganizationId.HasValue))
        {
            var translation = term.Translations.FirstOrDefault(tr => tr.TargetLanguage == targetLanguage);
            if (translation != null)
            {
                var key = term.CaseSensitive ? term.SourceTerm : term.SourceTerm.ToLowerInvariant();
                entries[key] = new GlossaryEntryDto
                {
                    SourceTerm = term.SourceTerm,
                    TranslatedTerm = translation.TranslatedTerm,
                    CaseSensitive = term.CaseSensitive
                };
            }
        }

        // Then project terms (override org)
        foreach (var term in terms.Where(t => t.ProjectId.HasValue))
        {
            var translation = term.Translations.FirstOrDefault(tr => tr.TargetLanguage == targetLanguage);
            if (translation != null)
            {
                var key = term.CaseSensitive ? term.SourceTerm : term.SourceTerm.ToLowerInvariant();
                entries[key] = new GlossaryEntryDto
                {
                    SourceTerm = term.SourceTerm,
                    TranslatedTerm = translation.TranslatedTerm,
                    CaseSensitive = term.CaseSensitive
                };
            }
        }

        return entries.Values.ToList();
    }

    /// <summary>
    /// Find glossary terms that appear in the given source text.
    /// </summary>
    public async Task<GlossaryUsageSummary> FindMatchingTermsAsync(
        int projectId,
        string sourceLanguage,
        string targetLanguage,
        string sourceText)
    {
        var entries = await GetEntriesForLanguagePairAsync(projectId, sourceLanguage, targetLanguage);

        var matchedEntries = new List<GlossaryEntryDto>();

        foreach (var entry in entries)
        {
            var comparison = entry.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (sourceText.Contains(entry.SourceTerm, comparison))
            {
                matchedEntries.Add(entry);
            }
        }

        return new GlossaryUsageSummary
        {
            GlossaryApplied = matchedEntries.Count > 0,
            TermsMatched = matchedEntries.Count,
            MatchedEntries = matchedEntries,
            Message = matchedEntries.Count > 0
                ? $"{matchedEntries.Count} glossary term(s) will be applied"
                : null
        };
    }

    /// <summary>
    /// Build AI context string for glossary terms.
    /// </summary>
    public string BuildGlossaryContext(List<GlossaryEntryDto> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        var lines = new List<string>
        {
            "Use these glossary terms consistently in your translation:"
        };

        foreach (var entry in entries)
        {
            lines.Add($"- \"{entry.SourceTerm}\" must be translated as \"{entry.TranslatedTerm}\"");
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Private Helpers

    private static GlossaryTermDto MapToDto(GlossaryTerm term)
    {
        return new GlossaryTermDto
        {
            Id = term.Id,
            ProjectId = term.ProjectId,
            OrganizationId = term.OrganizationId,
            SourceTerm = term.SourceTerm,
            SourceLanguage = term.SourceLanguage,
            Description = term.Description,
            CaseSensitive = term.CaseSensitive,
            Translations = term.Translations.ToDictionary(t => t.TargetLanguage, t => t.TranslatedTerm),
            CreatedBy = term.CreatedBy,
            CreatedByName = term.Creator?.DisplayName ?? term.Creator?.Username,
            CreatedAt = term.CreatedAt,
            UpdatedAt = term.UpdatedAt
        };
    }

    #endregion
}
