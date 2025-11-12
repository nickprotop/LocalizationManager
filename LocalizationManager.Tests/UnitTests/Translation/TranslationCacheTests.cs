// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Translation;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Translation;

public class TranslationCacheTests : IDisposable
{
    private readonly string _cacheDbPath;
    private readonly TranslationCache _cache;

    public TranslationCacheTests()
    {
        _cacheDbPath = Path.Combine(AppDataPaths.GetCredentialsDirectory(), "translations.db");
        _cache = new TranslationCache();

        // Clear any existing data for clean tests
        try
        {
            _cache.Clear();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    public void Dispose()
    {
        try
        {
            // Clear cache before disposing
            _cache.Clear();
        }
        catch
        {
            // Ignore errors
        }

        _cache.Dispose();
    }

    [Fact]
    public void Constructor_CreatesDatabaseFile()
    {
        // Assert
        Assert.True(File.Exists(_cacheDbPath));
    }

    [Fact]
    public void TryGet_EmptyCache_ReturnsFalse()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        // Act
        var found = _cache.TryGet(request, "deepl", out var response);

        // Assert
        Assert.False(found);
        Assert.Null(response);
    }

    [Fact]
    public void Store_And_TryGet_RetrievesCachedTranslation()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            SourceLanguage = "en",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl",
            DetectedSourceLanguage = "en",
            Confidence = 0.95
        };

        // Act
        _cache.Store(request, response);
        var found = _cache.TryGet(request, "deepl", out var cachedResponse);

        // Assert
        Assert.True(found);
        Assert.NotNull(cachedResponse);
        Assert.Equal("Bonjour", cachedResponse.TranslatedText);
        Assert.Equal("deepl", cachedResponse.Provider);
        Assert.Equal("en", cachedResponse.DetectedSourceLanguage);
        Assert.Equal(0.95, cachedResponse.Confidence);
        Assert.True(cachedResponse.FromCache);
    }

    [Fact]
    public void TryGet_DifferentProvider_ReturnsFalse()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl"
        };

        _cache.Store(request, response);

        // Act - Try to get with different provider
        var found = _cache.TryGet(request, "google", out _);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DifferentTargetLanguage_ReturnsFalse()
    {
        // Arrange
        var request1 = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl"
        };

        _cache.Store(request1, response);

        // Act - Try to get with different target language
        var request2 = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "de"
        };

        var found = _cache.TryGet(request2, "deepl", out _);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void Store_OverwritesExisting()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var response1 = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl"
        };

        var response2 = new TranslationResponse
        {
            TranslatedText = "Salut",
            Provider = "deepl"
        };

        // Act
        _cache.Store(request, response1);
        _cache.Store(request, response2);

        var found = _cache.TryGet(request, "deepl", out var cachedResponse);

        // Assert
        Assert.True(found);
        Assert.Equal("Salut", cachedResponse!.TranslatedText); // Should have latest value
    }

    [Fact]
    public void Clear_RemovesAllCachedTranslations()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl"
        };

        _cache.Store(request, response);

        // Act
        _cache.Clear();

        var found = _cache.TryGet(request, "deepl", out _);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void GetCount_EmptyCache_ReturnsZero()
    {
        // Act
        var count = _cache.GetCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetCount_AfterStore_ReturnsCorrectCount()
    {
        // Arrange
        var request1 = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var request2 = new TranslationRequest
        {
            SourceText = "Goodbye",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Test",
            Provider = "deepl"
        };

        // Act
        _cache.Store(request1, response);
        _cache.Store(request2, response);

        var count = _cache.GetCount();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void RemoveExpired_RemovesOldEntries()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl"
        };

        _cache.Store(request, response);

        // Act - Remove entries older than 0 seconds (should remove everything)
        _cache.RemoveExpired(TimeSpan.Zero);

        var count = _cache.GetCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void Store_NullSourceLanguage_HandledCorrectly()
    {
        // Arrange
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            SourceLanguage = null, // Auto-detect
            TargetLanguage = "fr"
        };

        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl",
            DetectedSourceLanguage = "en"
        };

        // Act
        _cache.Store(request, response);
        var found = _cache.TryGet(request, "deepl", out var cachedResponse);

        // Assert
        Assert.True(found);
        Assert.Equal("Bonjour", cachedResponse!.TranslatedText);
    }
}
