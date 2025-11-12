// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using LocalizationManager.Core.Translation;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Translation;

public class TranslationModelsTests
{
    #region TranslationRequest Tests

    [Fact]
    public void TranslationRequest_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var request = new TranslationRequest
        {
            SourceText = "Hello World",
            TargetLanguage = "fr"
        };

        // Assert
        Assert.Equal("Hello World", request.SourceText);
        Assert.Equal("fr", request.TargetLanguage);
        Assert.Null(request.SourceLanguage);
        Assert.Null(request.Context);
    }

    [Fact]
    public void TranslationRequest_WithAllProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var request = new TranslationRequest
        {
            SourceText = "Hello World",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            Context = "Greeting message"
        };

        // Assert
        Assert.Equal("Hello World", request.SourceText);
        Assert.Equal("en", request.SourceLanguage);
        Assert.Equal("fr", request.TargetLanguage);
        Assert.Equal("Greeting message", request.Context);
    }

    [Fact]
    public void TranslationRequest_WithNullSourceLanguage_AllowsAutoDetection()
    {
        // Arrange & Act
        var request = new TranslationRequest
        {
            SourceText = "Hello",
            SourceLanguage = null,
            TargetLanguage = "fr"
        };

        // Assert
        Assert.Null(request.SourceLanguage);
    }

    #endregion

    #region TranslationResponse Tests

    [Fact]
    public void TranslationResponse_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour le monde",
            Provider = "google"
        };

        // Assert
        Assert.Equal("Bonjour le monde", response.TranslatedText);
        Assert.Equal("google", response.Provider);
        Assert.Null(response.DetectedSourceLanguage);
        Assert.Null(response.Confidence);
        Assert.False(response.FromCache);
    }

    [Fact]
    public void TranslationResponse_WithAllProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour le monde",
            Provider = "google",
            DetectedSourceLanguage = "en",
            Confidence = 0.95,
            FromCache = true
        };

        // Assert
        Assert.Equal("Bonjour le monde", response.TranslatedText);
        Assert.Equal("google", response.Provider);
        Assert.Equal("en", response.DetectedSourceLanguage);
        Assert.Equal(0.95, response.Confidence);
        Assert.True(response.FromCache);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(null)]
    public void TranslationResponse_WithVariousConfidence_CreatesSuccessfully(double? confidence)
    {
        // Arrange & Act
        var response = new TranslationResponse
        {
            TranslatedText = "Bonjour",
            Provider = "deepl",
            Confidence = confidence
        };

        // Assert
        Assert.Equal(confidence, response.Confidence);
    }

    #endregion

    #region TranslationException Tests

    [Fact]
    public void TranslationException_WithMinimalParameters_CreatesSuccessfully()
    {
        // Arrange & Act
        var exception = new TranslationException(
            TranslationErrorCode.NetworkError,
            "Network error occurred"
        );

        // Assert
        Assert.Equal(TranslationErrorCode.NetworkError, exception.ErrorCode);
        Assert.Equal("Network error occurred", exception.Message);
        Assert.Null(exception.Provider);
        Assert.False(exception.IsRetryable);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void TranslationException_WithAllParameters_CreatesSuccessfully()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new TranslationException(
            TranslationErrorCode.RateLimitExceeded,
            "Rate limit exceeded",
            provider: "google",
            isRetryable: true,
            innerException: innerException
        );

        // Assert
        Assert.Equal(TranslationErrorCode.RateLimitExceeded, exception.ErrorCode);
        Assert.Equal("Rate limit exceeded", exception.Message);
        Assert.Equal("google", exception.Provider);
        Assert.True(exception.IsRetryable);
        Assert.Same(innerException, exception.InnerException);
    }

    [Theory]
    [InlineData(TranslationErrorCode.Unknown)]
    [InlineData(TranslationErrorCode.InvalidApiKey)]
    [InlineData(TranslationErrorCode.RateLimitExceeded)]
    [InlineData(TranslationErrorCode.NetworkError)]
    [InlineData(TranslationErrorCode.ServiceUnavailable)]
    [InlineData(TranslationErrorCode.UnsupportedLanguage)]
    [InlineData(TranslationErrorCode.TextTooLong)]
    [InlineData(TranslationErrorCode.Timeout)]
    [InlineData(TranslationErrorCode.QuotaExceeded)]
    [InlineData(TranslationErrorCode.InvalidRequest)]
    public void TranslationException_WithVariousErrorCodes_StoresCorrectly(TranslationErrorCode errorCode)
    {
        // Arrange & Act
        var exception = new TranslationException(errorCode, "Test error");

        // Assert
        Assert.Equal(errorCode, exception.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TranslationException_IsRetryable_StoresCorrectly(bool isRetryable)
    {
        // Arrange & Act
        var exception = new TranslationException(
            TranslationErrorCode.NetworkError,
            "Test error",
            isRetryable: isRetryable
        );

        // Assert
        Assert.Equal(isRetryable, exception.IsRetryable);
    }

    #endregion

    #region TranslationErrorCode Tests

    [Fact]
    public void TranslationErrorCode_AllValuesAreDefined()
    {
        // Arrange
        var expectedCodes = new[]
        {
            TranslationErrorCode.Unknown,
            TranslationErrorCode.InvalidApiKey,
            TranslationErrorCode.RateLimitExceeded,
            TranslationErrorCode.NetworkError,
            TranslationErrorCode.ServiceUnavailable,
            TranslationErrorCode.UnsupportedLanguage,
            TranslationErrorCode.TextTooLong,
            TranslationErrorCode.Timeout,
            TranslationErrorCode.QuotaExceeded,
            TranslationErrorCode.InvalidRequest
        };

        // Act & Assert
        foreach (var code in expectedCodes)
        {
            Assert.True(Enum.IsDefined(typeof(TranslationErrorCode), code));
        }
    }

    #endregion
}
