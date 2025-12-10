using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for managing CLI API keys.
/// </summary>
[Route("api/settings/cli-keys")]
[Authorize]
public class CliApiKeysController : ApiControllerBase
{
    private readonly ICliApiKeyService _apiKeyService;
    private readonly ILogger<CliApiKeysController> _logger;

    public CliApiKeysController(ICliApiKeyService apiKeyService, ILogger<CliApiKeysController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get all CLI API keys for the current user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CliApiKeyDto>>>> GetKeys()
    {
        var userId = GetUserId();
        var keys = await _apiKeyService.GetUserKeysAsync(userId);
        return Success(keys);
    }

    /// <summary>
    /// Create a new CLI API key.
    /// </summary>
    /// <remarks>
    /// The returned API key is only shown once. Store it securely.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateCliApiKeyResponse>>> CreateKey([FromBody] CreateCliApiKeyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, "Invalid request");

        var userId = GetUserId();
        var result = await _apiKeyService.CreateKeyAsync(userId, request);

        if (result == null)
        {
            return BadRequest(ErrorCodes.AUTH_FORBIDDEN, "Failed to create API key. You may not have access to the specified project.");
        }

        var (apiKey, keyInfo) = result.Value;

        return Success(new CreateCliApiKeyResponse
        {
            ApiKey = apiKey,
            KeyInfo = keyInfo
        });
    }

    /// <summary>
    /// Delete a CLI API key.
    /// </summary>
    [HttpDelete("{keyId}")]
    public async Task<ActionResult<ApiResponse>> DeleteKey(int keyId)
    {
        var userId = GetUserId();
        var deleted = await _apiKeyService.DeleteKeyAsync(userId, keyId);

        if (!deleted)
        {
            return NotFound(ErrorCodes.RES_NOT_FOUND, "API key not found");
        }

        return Success("API key deleted successfully");
    }
}
