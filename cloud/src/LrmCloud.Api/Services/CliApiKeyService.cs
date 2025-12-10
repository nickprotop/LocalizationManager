using System.Security.Cryptography;
using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Auth;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Implementation of CLI API key service.
/// </summary>
public class CliApiKeyService : ICliApiKeyService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CliApiKeyService> _logger;

    public CliApiKeyService(AppDbContext db, ILogger<CliApiKeyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CliApiKeyDto>> GetUserKeysAsync(int userId)
    {
        var keys = await _db.ApiKeys
            .Include(k => k.Project)
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new CliApiKeyDto
            {
                Id = k.Id,
                Name = k.Name ?? $"Key {k.KeyPrefix}",
                KeyPrefix = k.KeyPrefix,
                Scopes = k.Scopes,
                ProjectId = k.ProjectId,
                ProjectName = k.Project != null ? k.Project.Name : null,
                LastUsedAt = k.LastUsedAt,
                ExpiresAt = k.ExpiresAt,
                CreatedAt = k.CreatedAt
            })
            .ToListAsync();

        return keys;
    }

    public async Task<(string ApiKey, CliApiKeyDto KeyInfo)?> CreateKeyAsync(int userId, CreateCliApiKeyRequest request)
    {
        // Validate project if specified
        if (request.ProjectId.HasValue)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(
                p => p.Id == request.ProjectId.Value &&
                     (p.UserId == userId || p.Organization!.Members.Any(m => m.UserId == userId)));

            if (project == null)
            {
                _logger.LogWarning("User {UserId} tried to create key for inaccessible project {ProjectId}",
                    userId, request.ProjectId);
                return null;
            }
        }

        // Generate API key: lrm_<32 random chars>
        var rawKey = GenerateApiKey();
        var keyPrefix = rawKey[..10]; // "lrm_" + first 6 chars
        var keyHash = HashKey(rawKey);

        var apiKey = new ApiKey
        {
            UserId = userId,
            ProjectId = request.ProjectId,
            Name = request.Name,
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            Scopes = request.Scopes ?? "read,write",
            ExpiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();

        var keyInfo = new CliApiKeyDto
        {
            Id = apiKey.Id,
            Name = apiKey.Name ?? $"Key {keyPrefix}",
            KeyPrefix = keyPrefix,
            Scopes = apiKey.Scopes,
            ProjectId = apiKey.ProjectId,
            ProjectName = null, // Could load if needed
            LastUsedAt = apiKey.LastUsedAt,
            ExpiresAt = apiKey.ExpiresAt,
            CreatedAt = apiKey.CreatedAt
        };

        _logger.LogInformation("Created API key {KeyPrefix} for user {UserId}", keyPrefix, userId);

        return (rawKey, keyInfo);
    }

    public async Task<bool> DeleteKeyAsync(int userId, int keyId)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(
            k => k.Id == keyId && k.UserId == userId);

        if (key == null)
            return false;

        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted API key {KeyPrefix} for user {UserId}", key.KeyPrefix, userId);

        return true;
    }

    public async Task<(int UserId, int? ProjectId)?> ValidateKeyAsync(string apiKey)
    {
        var result = await ValidateKeyWithScopesAsync(apiKey);
        if (result == null)
            return null;

        return (result.UserId, result.ProjectId);
    }

    public async Task<ApiKeyValidationResult?> ValidateKeyWithScopesAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || !apiKey.StartsWith("lrm_"))
            return null;

        var keyPrefix = apiKey[..10];
        var keyHash = HashKey(apiKey);

        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix && k.KeyHash == keyHash);

        if (key == null)
            return null;

        // Check expiration
        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key used: {KeyPrefix}", keyPrefix);
            return null;
        }

        // Update last used
        key.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ApiKeyValidationResult
        {
            UserId = key.UserId,
            ProjectId = key.ProjectId,
            Scopes = key.Scopes,
            KeyPrefix = key.KeyPrefix
        };
    }

    private static string GenerateApiKey()
    {
        // Generate: lrm_<28 random base62 chars>
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = RandomNumberGenerator.GetBytes(28);
        var key = new char[32];
        key[0] = 'l';
        key[1] = 'r';
        key[2] = 'm';
        key[3] = '_';

        for (int i = 0; i < 28; i++)
        {
            key[i + 4] = chars[random[i] % chars.Length];
        }

        return new string(key);
    }

    private static string HashKey(string apiKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
