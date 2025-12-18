using System.Security.Claims;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.TranslationMemory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for Translation Memory (TM) operations.
/// TM stores past translations for reuse with exact and fuzzy matching.
/// </summary>
[Authorize]
[Route("api/tm")]
public class TranslationMemoryController : ApiControllerBase
{
    private readonly TranslationMemoryService _tmService;
    private readonly ILogger<TranslationMemoryController> _logger;

    public TranslationMemoryController(
        TranslationMemoryService tmService,
        ILogger<TranslationMemoryController> logger)
    {
        _tmService = tmService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Looks up TM matches for a source text.
    /// Returns exact matches (100%) first, then fuzzy matches by similarity.
    /// </summary>
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(ApiResponse<TmLookupResponse>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<TmLookupResponse>>> Lookup([FromBody] TmLookupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText))
            return BadRequest("TM_INVALID_REQUEST", "Source text is required");

        var userId = GetUserId();
        var result = await _tmService.LookupAsync(userId, request);

        return Success(result);
    }

    /// <summary>
    /// Stores a translation in TM. Updates existing entry if found.
    /// </summary>
    [HttpPost("store")]
    [ProducesResponseType(typeof(ApiResponse<TmMatchDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse<TmMatchDto>>> Store([FromBody] TmStoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText))
            return BadRequest("TM_INVALID_REQUEST", "Source text is required");
        if (string.IsNullOrWhiteSpace(request.TranslatedText))
            return BadRequest("TM_INVALID_REQUEST", "Translated text is required");

        var userId = GetUserId();
        var entry = await _tmService.StoreAsync(userId, request);

        var dto = new TmMatchDto
        {
            Id = entry.Id,
            SourceText = entry.SourceText,
            TranslatedText = entry.TranslatedText,
            SourceLanguage = entry.SourceLanguage,
            TargetLanguage = entry.TargetLanguage,
            MatchPercent = 100,
            UseCount = entry.UseCount,
            Context = entry.Context,
            UpdatedAt = entry.UpdatedAt
        };

        return Success(dto);
    }

    /// <summary>
    /// Batch stores multiple translations in TM.
    /// </summary>
    [HttpPost("store-batch")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<ApiResponse>> StoreBatch([FromBody] List<TmStoreRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return BadRequest("TM_INVALID_REQUEST", "At least one translation is required");

        var userId = GetUserId();
        await _tmService.StoreBatchAsync(userId, requests);

        return Success($"Stored {requests.Count} translations in TM");
    }

    /// <summary>
    /// Increments use count for a TM entry (when user accepts a suggestion).
    /// </summary>
    [HttpPost("{tmEntryId}/use")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ApiResponse>> IncrementUseCount(int tmEntryId)
    {
        await _tmService.IncrementUseCountAsync(tmEntryId);
        return Success("Use count incremented");
    }

    /// <summary>
    /// Gets TM statistics for the current user.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<TmStatsDto>), 200)]
    public async Task<ActionResult<ApiResponse<TmStatsDto>>> GetStats([FromQuery] int? organizationId = null)
    {
        var userId = GetUserId();
        var stats = await _tmService.GetStatsAsync(userId, organizationId);
        return Success(stats);
    }

    /// <summary>
    /// Deletes a specific TM entry.
    /// </summary>
    [HttpDelete("{tmEntryId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult> Delete(int tmEntryId)
    {
        var userId = GetUserId();
        var deleted = await _tmService.DeleteAsync(userId, tmEntryId);

        if (!deleted)
            return NotFound("TM_NOT_FOUND", "TM entry not found");

        return NoContent();
    }

    /// <summary>
    /// Clears all TM entries for the current user.
    /// Optionally filter by language pair.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<ActionResult<ApiResponse>> Clear(
        [FromQuery] string? sourceLanguage = null,
        [FromQuery] string? targetLanguage = null)
    {
        var userId = GetUserId();
        var count = await _tmService.ClearAsync(userId, sourceLanguage, targetLanguage);
        return Success($"Cleared {count} TM entries");
    }
}
