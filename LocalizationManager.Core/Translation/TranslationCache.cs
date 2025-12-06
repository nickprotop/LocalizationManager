// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LocalizationManager.Core.Configuration;
using Microsoft.Data.Sqlite;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Caches translation results using SQLite to avoid redundant API calls.
/// </summary>
public class TranslationCache : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Creates a new translation cache.
    /// </summary>
    public TranslationCache()
    {
        var cacheDir = AppDataPaths.GetCredentialsDirectory();
        var cachePath = Path.Combine(cacheDir, "translations.db");

        _connection = new SqliteConnection($"Data Source={cachePath}");
        _connection.Open();

        InitializeDatabase();
    }

    /// <summary>
    /// Tries to get a cached translation.
    /// </summary>
    /// <param name="request">The translation request.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="cachedResponse">The cached response, if found.</param>
    /// <returns>True if found in cache, otherwise false.</returns>
    public bool TryGet(TranslationRequest request, string provider, out TranslationResponse? cachedResponse)
    {
        cachedResponse = null;

        try
        {
            var cacheKey = GenerateCacheKey(request, provider);

            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT translated_text, detected_source_language, confidence
                FROM translations
                WHERE cache_key = @cacheKey
                  AND created_at > @expirationDate";

            command.Parameters.AddWithValue("@cacheKey", cacheKey);
            command.Parameters.AddWithValue("@expirationDate", DateTime.UtcNow.AddDays(-30)); // Cache expires after 30 days

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                cachedResponse = new TranslationResponse
                {
                    TranslatedText = reader.GetString(0),
                    DetectedSourceLanguage = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Confidence = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    Provider = provider,
                    FromCache = true
                };

                return true;
            }

            return false;
        }
        catch
        {
            // If cache lookup fails, return false (will fetch from API)
            return false;
        }
    }

    /// <summary>
    /// Stores a translation in the cache.
    /// </summary>
    /// <param name="request">The translation request.</param>
    /// <param name="response">The translation response.</param>
    public void Store(TranslationRequest request, TranslationResponse response)
    {
        try
        {
            var cacheKey = GenerateCacheKey(request, response.Provider);

            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO translations
                (cache_key, source_text, source_language, target_language, provider,
                 translated_text, detected_source_language, confidence, created_at)
                VALUES
                (@cacheKey, @sourceText, @sourceLanguage, @targetLanguage, @provider,
                 @translatedText, @detectedSourceLanguage, @confidence, @createdAt)";

            command.Parameters.AddWithValue("@cacheKey", cacheKey);
            command.Parameters.AddWithValue("@sourceText", request.SourceText);
            command.Parameters.AddWithValue("@sourceLanguage", request.SourceLanguage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@targetLanguage", request.TargetLanguage);
            command.Parameters.AddWithValue("@provider", response.Provider);
            command.Parameters.AddWithValue("@translatedText", response.TranslatedText);
            command.Parameters.AddWithValue("@detectedSourceLanguage", response.DetectedSourceLanguage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@confidence", response.Confidence ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            command.ExecuteNonQuery();
        }
        catch
        {
            // If caching fails, silently ignore (translation still succeeded)
        }
    }

    /// <summary>
    /// Clears all cached translations.
    /// </summary>
    public void Clear()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM translations";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes expired translations from the cache.
    /// </summary>
    /// <param name="olderThan">Remove translations older than this duration.</param>
    public void RemoveExpired(TimeSpan olderThan)
    {
        var expirationDate = DateTime.UtcNow - olderThan;

        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM translations WHERE created_at < @expirationDate";
        command.Parameters.AddWithValue("@expirationDate", expirationDate);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets the number of cached translations.
    /// </summary>
    /// <returns>The count of cached translations.</returns>
    public int GetCount()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM translations";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS translations (
                cache_key TEXT PRIMARY KEY,
                source_text TEXT NOT NULL,
                source_language TEXT,
                target_language TEXT NOT NULL,
                provider TEXT NOT NULL,
                translated_text TEXT NOT NULL,
                detected_source_language TEXT,
                confidence REAL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_created_at ON translations(created_at);
            CREATE INDEX IF NOT EXISTS idx_provider ON translations(provider);";

        command.ExecuteNonQuery();
    }

    private static string GenerateCacheKey(TranslationRequest request, string provider)
    {
        // Generate a unique cache key based on request parameters
        var keyComponents = $"{provider}|{request.SourceText}|{request.SourceLanguage ?? "auto"}|{request.TargetLanguage}";
        var keyBytes = Encoding.UTF8.GetBytes(keyComponents);
        var hashBytes = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hashBytes);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
