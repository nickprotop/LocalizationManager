// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Represents errors that occur during translation.
/// </summary>
public class TranslationException : Exception
{
    /// <summary>
    /// The error code.
    /// </summary>
    public TranslationErrorCode ErrorCode { get; }

    /// <summary>
    /// The provider that generated the error.
    /// </summary>
    public string? Provider { get; }

    /// <summary>
    /// Indicates whether the operation can be retried.
    /// </summary>
    public bool IsRetryable { get; }

    public TranslationException(
        TranslationErrorCode errorCode,
        string message,
        string? provider = null,
        bool isRetryable = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Provider = provider;
        IsRetryable = isRetryable;
    }
}

/// <summary>
/// Error codes for translation operations.
/// </summary>
public enum TranslationErrorCode
{
    /// <summary>
    /// Unknown or unspecified error.
    /// </summary>
    Unknown,

    /// <summary>
    /// API key is missing or invalid.
    /// </summary>
    InvalidApiKey,

    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// Network error occurred.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Translation service is unavailable.
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// Unsupported language pair.
    /// </summary>
    UnsupportedLanguage,

    /// <summary>
    /// Text is too long for the provider.
    /// </summary>
    TextTooLong,

    /// <summary>
    /// Request timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// Quota exceeded (e.g., monthly character limit).
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// Invalid request format or parameters.
    /// </summary>
    InvalidRequest
}
